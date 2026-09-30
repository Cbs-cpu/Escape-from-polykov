using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>
    /// Fixed-size ring of physics debris (spent casings, dropped magazines). The oldest piece is recycled,
    /// so there is never an allocation or an unbounded number of rigidbodies.
    /// </summary>
    public sealed class DebrisPool : MonoBehaviour
    {
        [SerializeField] private GameObject prefab;
        [SerializeField, Range(1, 64)] private int capacity = 24;
        [SerializeField] private float lifetime = 8f;

        private Rigidbody[] _bodies;
        private float[] _expireAt;
        private int _next;

        private void Awake()
        {
            transform.SetParent(null, true);
            if (prefab != null) Build();
        }

        /// <summary>For pools created from code: set the prefab, then build the ring.</summary>
        public void Initialize(GameObject debrisPrefab, int size, float lifetimeSeconds)
        {
            prefab = debrisPrefab;
            capacity = Mathf.Max(1, size);
            lifetime = lifetimeSeconds;
            Build();
        }

        private void Build()
        {
            _bodies = new Rigidbody[capacity];
            _expireAt = new float[capacity];
            for (int i = 0; i < capacity; i++)
            {
                GameObject go = Instantiate(prefab, transform);
                go.SetActive(false);
                _bodies[i] = go.GetComponent<Rigidbody>();
            }
        }

        public bool Ready => _bodies != null;

        public Rigidbody Launch(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
        {
            Rigidbody body = _bodies[_next];
            _expireAt[_next] = Time.time + lifetime;
            _next = (_next + 1) % capacity;

            GameObject go = body.gameObject;
            go.SetActive(false);
            body.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = velocity;
            body.angularVelocity = angularVelocity;
            return body;
        }

        private void Update()
        {
            if (_bodies == null) return;
            float now = Time.time;
            for (int i = 0; i < _bodies.Length; i++)
            {
                if (_expireAt[i] > 0f && now > _expireAt[i])
                {
                    _expireAt[i] = 0f;
                    _bodies[i].gameObject.SetActive(false);
                }
            }
        }
    }
}
