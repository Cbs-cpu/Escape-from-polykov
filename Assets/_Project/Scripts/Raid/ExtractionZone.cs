using UnityEngine;
using UnityEngine.SceneManagement;

namespace Polykov.Raid
{
    /// <summary>
    /// Extraction point: a trigger volume that runs <see cref="ExtractionTimer"/> while the player (a CharacterController)
    /// stands inside, shows the countdown and returns to the lobby when it completes.
    /// LOCAL presentation over pure logic; in multiplayer the server steps the timer and the client only displays it.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ExtractionZone : MonoBehaviour
    {
        [SerializeField] private string displayName = "Portón de carga";
        [SerializeField, Min(0f)] private float duration = 7f;
        [SerializeField] private string lobbyScene = "Lobby";
        [Tooltip("Seconds the zone name is listed on screen when the raid starts.")]
        [SerializeField, Min(0f)] private float announceSeconds = 8f;

        private ExtractionTimer _timer;
        private int _inside;
        private float _leaveAt = -1f;
        private GUIStyle _big, _small;

        public ExtractionPhase Phase => _timer.Phase;

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            _timer = ExtractionTimer.Create(duration);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other is CharacterController) _inside++;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other is CharacterController) _inside = Mathf.Max(0, _inside - 1);
        }

        private void FixedUpdate()
        {
            ExtractionPhase before = _timer.Phase;
            _timer = ExtractionTimer.Step(_timer, _inside > 0, Time.fixedDeltaTime);
            if (before != ExtractionPhase.Extracted && _timer.Phase == ExtractionPhase.Extracted) _leaveAt = Time.time + 1.5f;
        }

        private void Update()
        {
            if (_leaveAt > 0f && Time.time >= _leaveAt)
            {
                _leaveAt = -1f;
                if (Application.CanStreamedLevelBeLoaded(lobbyScene)) SceneManager.LoadScene(lobbyScene);
                else Debug.Log("[Extraction] Extraído (la escena '" + lobbyScene + "' no está en Build Settings).");
            }
        }

        private void OnGUI()
        {
            if (_big == null)
            {
                Font font = Resources.Load<Font>("Fonts/PolykovGrid-Bold");
                _big = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter, font = font };
                _small = new GUIStyle(_big) { fontSize = 18 };
                _big.normal.textColor = new Color(0.62f, 0.9f, 0.45f);
                _small.normal.textColor = new Color(0.85f, 0.85f, 0.8f);
            }
            float w = Screen.width;
            if (Time.timeSinceLevelLoad < announceSeconds)
                GUI.Label(new Rect(w - 420f, 20f, 400f, 30f), "EXTRACCIÓN: " + displayName.ToUpperInvariant(), _small);
            switch (_timer.Phase)
            {
                case ExtractionPhase.Counting:
                    GUI.Label(new Rect(0f, 90f, w, 40f), "EXTRAYENDO EN " + _timer.Remaining.ToString("0.0"), _big);
                    GUI.Label(new Rect(0f, 128f, w, 26f), displayName.ToUpperInvariant(), _small);
                    break;
                case ExtractionPhase.Extracted:
                    GUI.Label(new Rect(0f, 90f, w, 40f), "EXTRAÍDO", _big);
                    break;
            }
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}
