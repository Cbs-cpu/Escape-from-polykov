using System.Collections.Generic;
using UnityEngine;

namespace Polykov.Combat
{
    /// <summary>
    /// Keeps a number of scavs alive around the spawner's children (or itself): spawns up to <c>maxAlive</c>, and after a
    /// scav dies queues a replacement after <c>respawnDelay</c>. The corpse stays (ragdoll cap handles the rest) until
    /// <c>corpseLifetime</c> expires.
    /// </summary>
    public sealed class ScavSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject scavPrefab;
        [SerializeField, Range(1, 12)] private int maxAlive = 3;
        [SerializeField] private float respawnDelay = 20f;
        [SerializeField] private float corpseLifetime = 60f;
        [SerializeField] private bool spawnOnStart = true;

        private readonly List<float> _pending = new List<float>();
        private int _alive;

        private void Start()
        {
            if (!spawnOnStart) return;
            for (int i = 0; i < maxAlive; i++) Spawn();
        }

        private void Update()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < _pending[i]) continue;
                _pending.RemoveAt(i);
                Spawn();
            }
        }

        private Vector3 PointFor(int index)
        {
            if (transform.childCount == 0) return transform.position;
            return transform.GetChild(index % transform.childCount).position;
        }

        private void Spawn()
        {
            if (scavPrefab == null || _alive >= maxAlive) return;
            Vector3 point = PointFor(_alive + _pending.Count);
            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            GameObject scav = Instantiate(scavPrefab, point, rotation);
            if (scav.GetComponent<ScavAI>() == null) scav.AddComponent<ScavAI>();
            var health = scav.GetComponent<HealthComponent>();
            if (health == null) return;
            _alive++;
            health.Died += _ => OnDied(scav);
        }

        private void OnDied(GameObject scav)
        {
            _alive--;
            _pending.Add(Time.time + respawnDelay);
            if (scav != null) Destroy(scav, corpseLifetime);
        }
    }
}
