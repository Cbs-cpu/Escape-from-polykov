using System.Collections.Generic;
using Polykov.Movement;
using Polykov.Player;
using Polykov.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Polykov.Netcode
{
    /// <summary>
    /// Dev tool (F6): plays the local player as if connected to a server, without networking. Commands go to an
    /// invisible server body through a fake link (latency, jitter, loss, redundancy); the server simulates them with
    /// real collisions and sends states back; the client reconciles with <see cref="ClientPrediction"/> and hides
    /// corrections with <see cref="VisualErrorSmoother"/>. F7 shoves the server body to force a visible correction.
    /// F8 shows the server position as a marker. Exercises the exact Phase 2 prediction path.
    /// </summary>
    [DefaultExecutionOrder(-45)]
    public sealed class LatencySimulator : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerLook look;
        [SerializeField] private Material markerMaterial;

        [Header("Link")]
        [SerializeField, Range(0, 400)] private int latencyMs = 100;
        [SerializeField, Range(0, 60)] private int jitterMs = 10;
        [SerializeField, Range(0f, 0.5f)] private float packetLoss = 0.05f;
        [SerializeField, Range(1, 6)] private int redundancy = 4;
        [Tooltip("Commands the server waits for before simulating (must be >= redundancy - 1 to benefit from it).")]
        [SerializeField, Range(0, 8)] private int serverBufferTicks = 3;

        private struct Packet<T>
        {
            public float ArriveAt;
            public T Payload;
        }

        private struct StateMessage
        {
            public uint Tick;
            public NetPlayerState State;
        }

        private bool _active;
        private GameObject _serverObject;
        private CharacterBody _serverBody;
        private NetPlayerState _serverState;
        private BodySimulator _clientReplay;
        private ClientPrediction _prediction;
        private ServerInputQueue _queue;
        private VisualErrorSmoother _smoother;
        private readonly List<Packet<PlayerCommand>> _toServer = new List<Packet<PlayerCommand>>(256);
        private readonly List<Packet<StateMessage>> _toClient = new List<Packet<StateMessage>>(256);
        private readonly Queue<PlayerCommand> _recent = new Queue<PlayerCommand>(8);
        private System.Random _rng;
        private int _ticksSinceStart;
        private Transform _marker;

        private int _corrections;
        private float _lastError;
        private float _maxError;
        private GUIStyle _style;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.f6Key.wasPressedThisFrame) SetActive(!_active);
                if (_active && keyboard.f7Key.wasPressedThisFrame) ShoveServer();
                if (_active && keyboard.f8Key.wasPressedThisFrame && _marker != null)
                    _marker.gameObject.SetActive(!_marker.gameObject.activeSelf);
            }
            if (!_active) return;

            DeliverToClient();
            _smoother.Update(Time.deltaTime);
            motor.VisualOffset = _smoother.Offset;
        }

        private void OnDisable() => SetActive(false);

        private void SetActive(bool active)
        {
            if (active == _active) return;
            _active = active;
            if (active)
            {
                _rng = new System.Random(12345);
                _prediction = new ClientPrediction(512);
                _queue = new ServerInputQueue(128);
                _smoother = new VisualErrorSmoother();
                _toServer.Clear();
                _toClient.Clear();
                _recent.Clear();
                _ticksSinceStart = 0;
                _serverStarted = false;
                _corrections = 0;
                _maxError = _lastError = 0f;
                CreateServerBody();
                _clientReplay = new BodySimulator(motor.Body, () => motor.Tuning);
                motor.InputFilter = Quantize;
                motor.Ticked += OnClientTick;
            }
            else
            {
                motor.Ticked -= OnClientTick;
                motor.InputFilter = null;
                motor.VisualOffset = Vector3.zero;
                if (_serverObject != null) Destroy(_serverObject);
                _serverObject = null;
            }
        }

        private MovementInput Quantize(MovementInput input)
            => new PlayerCommand(0, input, default(WeaponInput), look.Pitch).Quantized().Movement;

        private void CreateServerBody()
        {
            CharacterController source = motor.Body.Controller;
            _serverObject = new GameObject("ServerBody (LatencySimulator)") { layer = motor.gameObject.layer };
            _serverObject.transform.position = motor.transform.position;
            var cc = _serverObject.AddComponent<CharacterController>();
            cc.height = source.height;
            cc.radius = source.radius;
            cc.center = source.center;
            cc.slopeLimit = source.slopeLimit;
            cc.stepOffset = source.stepOffset;
            cc.skinWidth = source.skinWidth;
            cc.minMoveDistance = source.minMoveDistance;
            Physics.IgnoreCollision(cc, source, true);
            _serverBody = new CharacterBody(cc, motor.BodySettings);
            _serverState = new NetPlayerState { Position = motor.transform.position, Movement = motor.State };

            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            // Immediately: a deferred Destroy would leave a collider inside the player's capsule for a frame.
            DestroyImmediate(marker.GetComponent<Collider>());
            marker.name = "ServerMarker";
            marker.layer = motor.gameObject.layer;
            marker.transform.SetParent(_serverObject.transform, false);
            marker.transform.localPosition = Vector3.up * 2.1f;
            marker.transform.localScale = Vector3.one * 0.12f;
            if (markerMaterial != null) marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
            _marker = marker.transform;
        }

        private void ShoveServer()
        {
            // Simulates something the client could not predict (another player pushing, a server-side hit...).
            _serverBody.Controller.Move(Vector3.ProjectOnPlane(look.transform.right, Vector3.up).normalized * 0.6f);
            _serverState.Position = _serverBody.Position;
        }

        private void OnClientTick(float dt)
        {
            float now = Time.time;
            _ticksSinceStart++;

            // --- client: record prediction, send the last N commands
            var command = new PlayerCommand(motor.Tick, motor.LastInput, default, look.Pitch);
            _prediction.Record(command, new NetPlayerState
            {
                Position = motor.transform.position, Yaw = look.Yaw, Pitch = look.Pitch, Movement = motor.State,
            });
            _recent.Enqueue(command);
            while (_recent.Count > redundancy) _recent.Dequeue();
            if (_rng.NextDouble() >= packetLoss)
            {
                float arrive = now + Delay();
                foreach (PlayerCommand c in _recent) _toServer.Add(new Packet<PlayerCommand> { ArriveAt = arrive, Payload = c });
            }

            // --- server: receive, simulate one command per tick, reply
            for (int i = _toServer.Count - 1; i >= 0; i--)
            {
                if (_toServer[i].ArriveAt > now) continue;
                _queue.Enqueue(_toServer[i].Payload);
                _toServer.RemoveAt(i);
            }
            // The server starts once it holds a small buffer, then consumes one command per tick.
            if (!_serverStarted && _queue.BufferedCount < Mathf.Max(1, serverBufferTicks)) return;
            _serverStarted = true;
            if (!_queue.TryDequeue(out PlayerCommand serverCommand)) return;

            Vector3 clientPosition = motor.Body.Position;
            _serverBody.Teleport(_serverState.Position);
            MovementState next = _serverBody.Step(_serverState.Movement, serverCommand.Movement, motor.Tuning, dt);
            _serverState = new NetPlayerState
            {
                Position = _serverBody.Position, Yaw = serverCommand.Movement.Yaw, Pitch = serverCommand.Pitch, Movement = next,
            };
            if (_rng.NextDouble() >= packetLoss)
                _toClient.Add(new Packet<StateMessage>
                {
                    ArriveAt = now + Delay(),
                    Payload = new StateMessage { Tick = serverCommand.Tick, State = _serverState },
                });
            // The client capsule must not have been disturbed by the server step.
            Debug.Assert((motor.Body.Position - clientPosition).sqrMagnitude < 1e-10f);
        }

        private bool _serverStarted;

        private void DeliverToClient()
        {
            float now = Time.time;
            bool corrected = false;
            NetPlayerState latest = default;
            for (int i = 0; i < _toClient.Count; i++)
            {
                if (_toClient[i].ArriveAt > now) continue;
                StateMessage message = _toClient[i].Payload;
                ReconcileResult result = _prediction.Reconcile(message.Tick, message.State, _clientReplay,
                    motor.TickInterval, out NetPlayerState state, out float error);
                if (result == ReconcileResult.Corrected)
                {
                    corrected = true;
                    latest = state;
                    _corrections++;
                    _lastError = error;
                    _maxError = Mathf.Max(_maxError, error);
                }
            }
            _toClient.RemoveAll(p => p.ArriveAt <= now);
            if (!corrected) return;

            Vector3 before = motor.InterpolatedPosition - motor.VisualOffset;
            Vector3 delta = motor.ApplyCorrection(latest.Movement, latest.Position);
            _smoother.OnCorrection(before, before + delta);
        }

        private float Delay() => (latencyMs + (float)(_rng.NextDouble() * 2.0 - 1.0) * jitterMs) / 1000f;

        private void OnGUI()
        {
            if (!_active) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperRight };
                _style.normal.textColor = new Color(0.95f, 0.85f, 0.55f);
            }
            string text = "NETWORK SIM (F6)  latency " + latencyMs + "±" + jitterMs + " ms  loss " + (packetLoss * 100f).ToString("0") + "%"
                          + "\nredundancy x" + redundancy + "  server buffer " + serverBufferTicks + "  queued " + _queue.BufferedCount
                          + "\ncorrections " + _corrections + "  last " + (_lastError * 1000f).ToString("0") + " mm  max "
                          + (_maxError * 1000f).ToString("0") + " mm  missed cmds " + _queue.MissedCommands
                          + "\nF7 empujar servidor · F8 marcador";
            GUI.Label(new Rect(Screen.width - 520f, 12f, 500f, 80f), text, _style);
        }
    }
}
