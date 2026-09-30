using System.IO;
using Polykov.Combat;
using Polykov.Weapons;
using UnityEditor;
using UnityEngine;

namespace Polykov.EditorTools
{
    /// <summary>
    /// Regenerates the Scav enemy and the blood/gore assets: layers, procedural blood textures (dark red, chunky,
    /// point filtered), materials, pooled particle prefabs, wound/splat wiring in the weapon view data and the
    /// Scav prefab (model + Animator + HealthComponent + hitboxes + combat rig). Safe to run repeatedly.
    /// </summary>
    public static class ScavAssetBuilder
    {
        private const string ScavDir = "Assets/_Project/Art/Characters/Scav";
        private const string FxDir = "Assets/_Project/VFX";
        private const string PrefabPath = "Assets/_Project/Prefabs/Enemies/Scav.prefab";
        private const string OutlinePath = "Assets/_Project/Art/Weapons/M1911_Tripo/M_Outline.mat";
        private const string ControllerPath = "Assets/_Project/Animations/Player/AC_Operator.controller";
        private const string ViewDataPath = "Assets/_Project/ScriptableObjects/Weapons/VD_M1911.asset";

        private static readonly Color BloodDark = new Color(0.36f, 0.02f, 0.02f, 1f);
        private static readonly Color BloodBright = new Color(0.62f, 0.05f, 0.05f, 1f);

        [MenuItem("Polykov/Build Scav and Blood Assets")]
        public static void BuildAll()
        {
            EnsureLayers();
            Material scav = BuildScavMaterial();
            BuildBloodAssets();
            BuildScavPrefab(scav);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Scav and blood assets built.");
        }

        // ---- layers ----

        private static void EnsureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SetLayer(layers, 10, CombatLayers.EnemyName);
            SetLayer(layers, 11, CombatLayers.RagdollName);
            tagManager.ApplyModifiedProperties();
        }

        private static void SetLayer(SerializedProperty layers, int index, string name)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(layer.stringValue)) layer.stringValue = name;
        }

        // ---- materials and textures ----

        private static Material BuildScavMaterial()
        {
            string path = ScavDir + "/M_Scav.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ScavDir + "/T_Scav_Albedo.png"));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ScavDir + "/T_Scav_Normal.png"));
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Smoothness", 0.1f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material TransparentMaterial(string path, string shader, Texture2D texture, Color color, bool doubleSided)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = Shader.Find(shader);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", 5f);
            material.SetFloat("_DstBlend", 10f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", doubleSided ? 0f : 2f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D WriteTexture(string name, int size, System.Func<int, int, Color> pixel)
        {
            string path = FxDir + "/Textures/" + name + ".png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++) tex.SetPixel(x, y, pixel(x, y));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D DropTexture() => WriteTexture("T_BloodDrop", 16, (x, y) =>
        {
            float dx = x - 7.5f, dy = y - 7.5f;
            return dx * dx + dy * dy <= 6.3f * 6.3f ? Color.white : new Color(1f, 1f, 1f, 0f);
        });

        /// <summary>Irregular blob with satellite droplets and a couple of streaks (deterministic).</summary>
        private static Texture2D SplatTexture()
        {
            const int N = 64;
            var rng = new System.Random(7);
            var alpha = new float[N, N];
            void Disc(float cx, float cy, float r)
            {
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float dx = x - cx, dy = y - cy;
                        if (dx * dx + dy * dy <= r * r) alpha[x, y] = 1f;
                    }
            }
            Disc(32, 32, 10);
            for (int i = 0; i < 9; i++)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI * 2f), d = 8f + (float)rng.NextDouble() * 10f;
                Disc(32 + Mathf.Cos(a) * d, 32 + Mathf.Sin(a) * d, 3f + (float)rng.NextDouble() * 4f);
            }
            for (int i = 0; i < 7; i++)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI * 2f), d = 18f + (float)rng.NextDouble() * 12f;
                Disc(32 + Mathf.Cos(a) * d, 32 + Mathf.Sin(a) * d, 1.5f + (float)rng.NextDouble() * 1.5f);
            }
            for (int s = 0; s < 3; s++)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI * 2f);
                for (float d = 8f; d < 26f; d += 1f) Disc(32 + Mathf.Cos(a) * d, 32 + Mathf.Sin(a) * d, 2.2f - d * 0.05f);
            }
            return WriteTexture("T_BloodSplat", N, (x, y) =>
            {
                if (alpha[x, y] <= 0f) return new Color(1f, 1f, 1f, 0f);
                float shade = ((x * 7 + y * 13) % 5) / 5f * 0.25f;
                return new Color(1f - shade, 1f - shade, 1f - shade, 0.92f);
            });
        }

        private static Texture2D WoundTexture() => WriteTexture("T_BloodWound", 32, (x, y) =>
        {
            float dx = x - 15.5f, dy = y - 15.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float ring = 9f + Mathf.Sin(Mathf.Atan2(dy, dx) * 5f) * 1.6f + ((x * 3 + y * 5) % 3) * 0.4f;
            if (d <= 3.2f) return new Color(0.22f, 0.02f, 0.02f, 1f);
            if (d <= ring) return new Color(0.75f, 0.12f, 0.12f, Mathf.Lerp(0.95f, 0.55f, d / ring));
            return new Color(1f, 1f, 1f, 0f);
        });

        // ---- blood particle prefabs ----

        private static void BuildBloodAssets()
        {
            Texture2D drop = DropTexture();
            Texture2D splat = SplatTexture();
            Texture2D wound = WoundTexture();

            Material particle = TransparentMaterial(FxDir + "/Materials/M_FX_Blood.mat",
                "Universal Render Pipeline/Particles/Unlit", drop, Color.white, true);
            Material mist = TransparentMaterial(FxDir + "/Materials/M_FX_BloodMist.mat",
                "Universal Render Pipeline/Particles/Unlit", drop, new Color(1f, 1f, 1f, 0.6f), true);
            Material splatMat = TransparentMaterial(FxDir + "/Materials/M_FX_BloodSplat.mat",
                "Universal Render Pipeline/Unlit", splat, BloodDark, true);
            Material woundMat = TransparentMaterial(FxDir + "/Materials/M_FX_BloodWound.mat",
                "Universal Render Pipeline/Unlit", wound, Color.white, true);

            GameObject entry = SprayPrefab("FX_Blood_Entry", particle, 16, 26f, 1.6f, 4.6f, 0.022f, 0.05f, 0.28f, 0.5f, 1.6f, true);
            GameObject exit = SprayPrefab("FX_Blood_Exit", particle, 26, 36f, 2.2f, 6.5f, 0.028f, 0.065f, 0.3f, 0.6f, 1.8f, true);
            GameObject mistPrefab = MistPrefab("FX_Blood_Mist", mist);

            var view = AssetDatabase.LoadAssetAtPath<WeaponViewData>(ViewDataPath);
            if (view != null)
            {
                view.BloodEntry = entry;
                view.BloodExit = exit;
                view.BloodMist = mistPrefab;
                view.WoundMaterial = woundMat;
                view.SplatMaterial = splatMat;
                EditorUtility.SetDirty(view);
            }
        }

        private static GameObject SprayPrefab(string name, Material material, int count, float angle, float speedMin,
            float speedMax, float sizeMin, float sizeMax, float lifeMin, float lifeMax, float gravity, bool stretch)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(BloodDark, BloodBright);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 48;
            main.stopAction = ParticleSystemStopAction.Disable;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = 0.012f;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = fade;
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.35f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            if (stretch)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.025f;
                renderer.lengthScale = 1.6f;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return SavePrefab(go, name);
        }

        private static GameObject MistPrefab(string name, Material material)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.5f, 0.04f, 0.04f, 0.5f), new Color(0.35f, 0.02f, 0.02f, 0.4f));
            main.gravityModifier = 0.15f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 16;
            main.stopAction = ParticleSystemStopAction.Disable;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)10) });
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 55f;
            shape.radius = 0.03f;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = fade;
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.8f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return SavePrefab(go, name);
        }

        private static GameObject SavePrefab(GameObject go, string name)
        {
            string path = FxDir + "/Prefabs/" + name + ".prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved;
        }

        // ---- Scav prefab ----

        private static void BuildScavPrefab(Material scavMaterial)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ScavDir + "/Scav.fbx");
            var outline = AssetDatabase.LoadAssetAtPath<Material>(OutlinePath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);

            var root = new GameObject("Scav") { layer = CombatLayers.Enemy };
            var health = root.AddComponent<HealthComponent>();
            GameObject body = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            body.name = "Model";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            var animator = body.GetComponentInChildren<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterials = new[] { scavMaterial, outline };
                r.enabled = true;
            }

            // Hitboxes are baked into the prefab so shooting works in edit-time tests too.
            HumanoidHitboxes.Build(animator, health, CombatLayers.Enemy);
            root.AddComponent<HumanoidCombatRig>();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }
    }
}
