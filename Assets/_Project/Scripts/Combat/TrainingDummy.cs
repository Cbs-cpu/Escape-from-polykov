using System.Text;
using UnityEngine;

namespace Polykov.Combat
{
    /// <summary>
    /// Test-range Operator: idle animation, per-bone hitboxes, Tarkov-style body-part HP shown above it.
    /// Ragdolls when killed (see HumanoidCombatRig) and stands up healed after a few seconds.
    /// </summary>
    public sealed class TrainingDummy : MonoBehaviour
    {
        private HealthComponent _health;
        private HumanoidCombatRig _rig;
        private Camera _camera;
        private float _deadTimer;
        private string _lastHit = string.Empty;
        private float _lastHitUntil;
        private readonly StringBuilder _builder = new StringBuilder(256);
        private GUIStyle _style;

        public float RespawnDelay { get; set; } = 3f;

        public static TrainingDummy Spawn(GameObject model, RuntimeAnimatorController controller, Vector3 position,
            Quaternion rotation, Transform parent)
        {
            var root = new GameObject("TrainingDummy");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);
            GameObject body = Instantiate(model, root.transform, false);
            var animator = body.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
            }
            var dummy = root.AddComponent<TrainingDummy>();
            dummy._health = root.AddComponent<HealthComponent>();
            dummy._rig = root.AddComponent<HumanoidCombatRig>();
            dummy._health.Damaged += dummy.OnDamaged;
            dummy._health.Died += dummy.OnDied;
            return dummy;
        }

        private void OnDamaged(BodyPart part, DamageResult result, Weapons.ShotHit hit)
        {
            _lastHit = "-" + result.Applied.ToString("0") + " " + PartName(part) + (result.Spread ? " (se reparte)" : "");
            _lastHitUntil = Time.time + 1.5f;
        }

        private void OnDied(BodyPart part)
        {
            _deadTimer = RespawnDelay;
            _lastHit = "MUERTO (" + PartName(part) + ")";
            _lastHitUntil = Time.time + RespawnDelay;
        }

        private void Update()
        {
            bool dead = !_health.State.Alive;
            if (dead)
            {
                _deadTimer -= Time.deltaTime;
                if (_deadTimer <= 0f)
                {
                    _health.ResetHealth();
                    _rig.Revive();
                }
            }
        }

        private void OnGUI()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            Vector3 anchor = transform.position + Vector3.up * 2.05f;
            Vector3 screen = _camera.WorldToScreenPoint(anchor);
            if (screen.z <= 0f || screen.z > 40f) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.LowerCenter };
                _style.normal.textColor = new Color(0.95f, 0.95f, 0.85f);
            }

            HealthState s = _health.State;
            _builder.Clear();
            if (Time.time < _lastHitUntil) _builder.Append(_lastHit).Append('\n');
            _builder.Append("Cab ").Append(s.Head.ToString("0")).Append("  Tór ").Append(s.Thorax.ToString("0"))
                .Append("  Est ").Append(s.Stomach.ToString("0")).Append('\n')
                .Append("BrI ").Append(s.LeftArm.ToString("0")).Append("  BrD ").Append(s.RightArm.ToString("0"))
                .Append("  PiI ").Append(s.LeftLeg.ToString("0")).Append("  PiD ").Append(s.RightLeg.ToString("0"));
            float y = Screen.height - screen.y;
            GUI.Label(new Rect(screen.x - 150f, y - 60f, 300f, 60f), _builder.ToString(), _style);
        }

        public static string PartName(BodyPart part) => part switch
        {
            BodyPart.Head => "cabeza",
            BodyPart.Thorax => "tórax",
            BodyPart.Stomach => "estómago",
            BodyPart.LeftArm => "brazo izq.",
            BodyPart.RightArm => "brazo der.",
            BodyPart.LeftLeg => "pierna izq.",
            _ => "pierna der.",
        };
    }
}
