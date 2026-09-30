using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>
    /// Builds a low-poly Colt M1911A1 placeholder out of boxes, with the named parts <see cref="WeaponModel"/>
    /// expects. Real proportions (~216 mm long). Replaced by the Blender model in a later step; keep the names.
    /// </summary>
    public static class M1911Builder
    {
        public static WeaponModel Build(Transform parent, Material steel, Material grip, int layer)
        {
            var root = new GameObject("M1911");
            root.layer = layer;
            root.transform.SetParent(parent, false);
            Transform r = root.transform;

            // Slide (moves back when firing, locks back when empty).
            Transform slide = Group("Slide", r, Vector3.zero, Quaternion.identity, layer);
            Box("SlideBody", slide, new Vector3(0f, 0.016f, 0.045f), new Vector3(0.024f, 0.032f, 0.21f), steel, layer);
            Box("SlideTop", slide, new Vector3(0f, 0.033f, 0.045f), new Vector3(0.016f, 0.003f, 0.2f), steel, layer);
            Box("Bushing", slide, new Vector3(0f, 0.012f, 0.151f), new Vector3(0.017f, 0.017f, 0.004f), steel, layer);
            Box("FrontSight", slide, new Vector3(0f, 0.037f, 0.142f), new Vector3(0.004f, 0.006f, 0.008f), steel, layer);
            Box("RearSightL", slide, new Vector3(-0.0055f, 0.0365f, -0.052f), new Vector3(0.006f, 0.006f, 0.008f), steel, layer);
            Box("RearSightR", slide, new Vector3(0.0055f, 0.0365f, -0.052f), new Vector3(0.006f, 0.006f, 0.008f), steel, layer);
            for (int i = 0; i < 5; i++)
            {
                float z = -0.05f + i * 0.0055f;
                Box("Serration", slide, new Vector3(0.0122f, 0.017f, z), new Vector3(0.0012f, 0.02f, 0.0025f), grip, layer);
                Box("Serration", slide, new Vector3(-0.0122f, 0.017f, z), new Vector3(0.0012f, 0.02f, 0.0025f), grip, layer);
            }
            Box("EjectionPort", slide, new Vector3(0.0122f, 0.024f, 0.028f), new Vector3(0.0012f, 0.01f, 0.03f), grip, layer);

            // Frame.
            Box("DustCover", r, new Vector3(0f, -0.007f, 0.078f), new Vector3(0.022f, 0.014f, 0.114f), steel, layer);
            Box("FrameBody", r, new Vector3(0f, -0.011f, -0.028f), new Vector3(0.024f, 0.022f, 0.1f), steel, layer);
            Box("GuardBottom", r, new Vector3(0f, -0.037f, 0.02f), new Vector3(0.008f, 0.004f, 0.054f), steel, layer);
            Box("GuardFront", r, new Vector3(0f, -0.022f, 0.045f), new Vector3(0.008f, 0.03f, 0.004f), steel, layer);
            Box("Beavertail", r, new Vector3(0f, -0.001f, -0.074f), new Vector3(0.026f, 0.007f, 0.03f), steel, layer);
            Box("Safety", r, new Vector3(-0.0135f, 0.004f, -0.05f), new Vector3(0.003f, 0.005f, 0.016f), steel, layer);
            Box("SlideStop", r, new Vector3(-0.0135f, -0.004f, 0.022f), new Vector3(0.003f, 0.006f, 0.02f), steel, layer);

            Transform trigger = Group("Trigger", r, new Vector3(0f, -0.01f, 0.006f), Quaternion.identity, layer);
            Box("Blade", trigger, new Vector3(0f, -0.009f, 0f), new Vector3(0.006f, 0.016f, 0.005f), steel, layer);

            Transform hammer = Group("Hammer", r, new Vector3(0f, 0.004f, -0.062f), Quaternion.identity, layer);
            Box("Spur", hammer, new Vector3(0f, 0.01f, -0.004f), new Vector3(0.008f, 0.016f, 0.008f), steel, layer);

            // Grip, raked ~18 degrees like the real frame.
            Transform gripFrame = Group("GripFrame", r, new Vector3(0f, -0.02f, -0.044f), Quaternion.Euler(18f, 0f, 0f), layer);
            Box("Frame", gripFrame, new Vector3(0f, -0.045f, 0f), new Vector3(0.026f, 0.09f, 0.042f), steel, layer);
            Box("PanelL", gripFrame, new Vector3(-0.0148f, -0.046f, 0.001f), new Vector3(0.004f, 0.074f, 0.036f), grip, layer);
            Box("PanelR", gripFrame, new Vector3(0.0148f, -0.046f, 0.001f), new Vector3(0.004f, 0.074f, 0.036f), grip, layer);
            Group("GripCenter", gripFrame, new Vector3(0f, -0.045f, 0f), Quaternion.identity, layer);

            Transform magazine = Group("Magazine", gripFrame, Vector3.zero, Quaternion.identity, layer);
            Box("Body", magazine, new Vector3(0f, -0.05f, 0.002f), new Vector3(0.02f, 0.098f, 0.032f), steel, layer);
            Box("BasePlate", magazine, new Vector3(0f, -0.0985f, 0.002f), new Vector3(0.025f, 0.005f, 0.036f), steel, layer);
            // Support-hand wrist when holding the magazine by its base (fingers wrap it).
            Transform grab = Group("MagazineGrab", magazine, new Vector3(-0.03f, -0.13f, -0.01f), Quaternion.identity, layer);
            grab.localRotation = Quaternion.LookRotation(new Vector3(0.5f, 0.8f, 0.3f), new Vector3(0f, 0.3f, 1f));

            Group("Muzzle", r, new Vector3(0f, 0.012f, 0.154f), Quaternion.identity, layer);
            Group("SightLine", r, new Vector3(0f, 0.0395f, -0.052f), Quaternion.identity, layer);

            var model = root.AddComponent<WeaponModel>();
            model.Initialize();
            return model;
        }

        private static Transform Group(string name, Transform parent, Vector3 position, Quaternion rotation, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(position, rotation);
            return go.transform;
        }

        private static void Box(string name, Transform parent, Vector3 center, Vector3 size, Material material, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = PrimitiveMeshes.Cube;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
        }
    }
}
