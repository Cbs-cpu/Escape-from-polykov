using UnityEngine;
using UnityEngine.InputSystem;

namespace Polykov.Weapons
{
    /// <summary>
    /// Dev tool: F3 places a row of steel targets in front of the player (5, 10, 15, 25 m) wherever you are;
    /// F4 refills spare ammo. Targets are plain primitives with colliders, so shots, impacts and the
    /// obstruction probe all work against them.
    /// </summary>
    public sealed class TestRange : MonoBehaviour
    {
        [SerializeField] private PlayerWeapon weapon;
        [SerializeField] private Material plateMaterial;
        [SerializeField] private Material standMaterial;
        [SerializeField] private float[] distances = { 5f, 10f, 15f, 25f };
        [SerializeField] private LayerMask groundMask = ~(1 << 8);

        private Transform _range;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f3Key.wasPressedThisFrame) Build();
            if (keyboard.f4Key.wasPressedThisFrame) weapon.Resupply();
        }

        private void Build()
        {
            if (_range != null) Destroy(_range.gameObject);
            _range = new GameObject("TestRange").transform;

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            for (int i = 0; i < distances.Length; i++)
            {
                float side = (i % 2 == 0 ? -1f : 1f) * (0.6f + i * 0.35f);
                Vector3 spot = transform.position + forward * distances[i] + right * side;
                if (Physics.Raycast(spot + Vector3.up * 3f, Vector3.down, out RaycastHit ground, 10f, groundMask,
                        QueryTriggerInteraction.Ignore))
                    spot = ground.point;
                CreateTarget(spot, Quaternion.LookRotation(-forward), 1f + i * 0.15f);
            }
        }

        private void CreateTarget(Vector3 position, Quaternion facing, float scale)
        {
            var stand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stand.name = "Stand";
            stand.transform.SetParent(_range, false);
            stand.transform.SetPositionAndRotation(position + Vector3.up * 0.45f, facing);
            stand.transform.localScale = new Vector3(0.06f, 0.9f, 0.06f);
            if (standMaterial != null) stand.GetComponent<Renderer>().sharedMaterial = standMaterial;

            // Hinge at the top of the stand; the plate hangs above it.
            var hinge = new GameObject("Target");
            hinge.transform.SetParent(_range, false);
            hinge.transform.SetPositionAndRotation(position + Vector3.up * 0.9f, facing);
            hinge.AddComponent<ShootingTarget>();

            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Plate";
            plate.transform.SetParent(hinge.transform, false);
            plate.transform.localPosition = new Vector3(0f, 0.25f * scale, 0f);
            plate.transform.localScale = new Vector3(0.35f * scale, 0.5f * scale, 0.015f);
            if (plateMaterial != null) plate.GetComponent<Renderer>().sharedMaterial = plateMaterial;
        }
    }
}
