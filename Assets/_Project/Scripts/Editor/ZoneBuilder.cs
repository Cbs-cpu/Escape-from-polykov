using System.Collections.Generic;
using System.IO;
using Polykov.Raid;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Polykov.EditorTools
{
    /// <summary>
    /// Builds "Zona A" (Scenes/Zone_A.unity) from the Blender exports of ArtSource/Tools/build_zone_a.py:
    /// one FBX per landmark (Art/Environment/ZoneA/ZoneA_*.fbx) + ZoneA_Markers.fbx (PROP_/SPAWN_/EXTRACT_/FX_/SCAV_ empties).
    /// Materials: every M_Zone_&lt;key&gt; slot becomes a Polykov/Stylized material with the Blender colour, M_Outline the ink.
    /// Adds colliders, Tripo props at their markers, the player, scavs, the helipad extraction, cold overcast lighting,
    /// fog, the post-processing volume and the zone particles, then bakes the NavMesh. Re-run to regenerate.
    /// </summary>
    public static class ZoneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Zone_A.unity";
        private const string ArtDir = "Assets/_Project/Art/Environment/ZoneA";
        private const string MaterialDir = "Assets/_Project/Materials/ZoneA";
        private const string VolumePath = "Assets/_Project/Materials/ZoneA/ZoneA_Volume.asset";
        private const string PlayerPrefab = "Assets/_Project/Prefabs/Player.prefab";
        private const string ScavPrefab = "Assets/_Project/Prefabs/Enemies/Scav.prefab";

        private static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

        // Same palette as ArtSource/Tools/build_zone_a.py and process_tripo_zone_props.py (the FBX exporter does not
        // carry node-less Blender colours). Key = material name without the M_Zone_ / M_ZoneProp_ prefix.
        private static readonly Dictionary<string, string> Palette = new Dictionary<string, string>
        {
            { "concrete", "#8b908d" }, { "concrete_dark", "#6a706e" }, { "concrete_light", "#a3a7a3" }, { "brick", "#8c5039" },
            { "rust", "#7b4a2e" }, { "corrugated", "#6f7876" }, { "roof", "#596160" }, { "steel", "#3b4044" }, { "window", "#27302f" },
            { "grass", "#56673a" }, { "asphalt", "#4b4f50" }, { "hazard", "#c9a227" }, { "tank", "#9aa09d" }, { "boxcar", "#6e3a2b" },
            { "wagon_green", "#4c5a3c" }, { "grate", "#4a3a2e" }, { "wood", "#7d6444" }, { "door", "#474d4e" }, { "helipad", "#c8643c" },
            { "white", "#d9d6cc" }, { "moss", "#4f6135" }, { "ground", "#6f6f66" },
            { "machine", "#56654a" }, { "machine_dark", "#3f4a37" }, { "barrel_green", "#4c5a3c" },
        };

        [MenuItem("Polykov/Build Zona A")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
            AddToBuildSettings();
            Debug.Log("[ZoneBuilder] Zona A construida: " + ScenePath);
        }

        /// <summary>Entry point for automation (no save prompt).</summary>
        public static void BuildHeadless()
        {
            BuildScene();
            AddToBuildSettings();
        }

        private static void BuildScene()
        {
            Mats.Clear();
            Directory.CreateDirectory(MaterialDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var zone = new GameObject("ZoneA").transform;

            foreach (string guid in AssetDatabase.FindAssets("t:Model ZoneA_", new[] { ArtDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (name == "ZoneA_Markers" || Path.GetDirectoryName(path).Replace('\\', '/') != ArtDir) continue;
                GameObject part = Place(path, zone, name != "ZoneA_Background");
                if (name == "ZoneA_Background") part.isStatic = true;
            }

            var markers = new Dictionary<string, Transform>();
            var markerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ArtDir + "/ZoneA_Markers.fbx");
            GameObject markerRoot = null;
            if (markerAsset != null)
            {
                markerRoot = (GameObject)Object.Instantiate(markerAsset);
                foreach (Transform t in markerRoot.GetComponentsInChildren<Transform>(true)) markers[t.name] = t;
            }

            Props(zone, markers);
            Lighting();
            Volume();
            Effects(zone, markers);
            Extraction(zone, markers);

            var surface = zone.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            Actors(markers);
            if (markerRoot != null) Object.DestroyImmediate(markerRoot);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ------------------------------------------------------------------ geometry + materials
        private static GameObject Place(string assetPath, Transform parent, bool collide)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = Stylize(slots[i]);
                r.sharedMaterials = slots;
                r.shadowCastingMode = ShadowCastingMode.On;
                if (collide && r.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null && r.GetComponent<Collider>() == null)
                    r.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic
                    | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
            return go;
        }

        /// <summary>Imported FBX material -> project material (Polykov/Stylized with the Blender colour, or the ink).</summary>
        private static Material Stylize(Material imported)
        {
            if (imported == null) return null;
            string key = imported.name.Replace(" (Instance)", "");
            if (Mats.TryGetValue(key, out Material cached)) return cached;
            bool ink = key.Contains("Outline");
            string path = MaterialDir + "/" + (ink ? "M_ZoneA_Outline" : key) + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find(ink ? "Polykov/Outline" : "Polykov/Stylized"));
                AssetDatabase.CreateAsset(m, path);
            }
            if (!ink)
            {
                string pk = key.Replace("M_ZoneProp_", "").Replace("M_Zone_", "");
                Color c = Palette.TryGetValue(pk, out string hex) && ColorUtility.TryParseHtmlString(hex, out Color pc) ? pc
                    : imported.HasProperty("_BaseColor") ? imported.GetColor("_BaseColor") : Color.gray;
                c.a = 1f;
                m.SetColor("_BaseColor", c);
                bool glass = key.Contains("window");
                bool ground = key.Contains("asphalt") || key.Contains("ground") || key.Contains("grass");
                // Glass stays clean and dark; ground gets more moss/grime; roofs and metal fewer streaks.
                m.SetFloat("_GrimeStrength", glass ? 0f : ground ? 0.25f : 0.55f);
                m.SetFloat("_StreakStrength", glass ? 0f : key.Contains("roof") ? 0.15f : 0.4f);
                m.SetFloat("_MossAmount", glass ? 0f : ground ? 0.45f : key.Contains("roof") ? 0.4f : 0.25f);
                m.SetFloat("_Variation", glass ? 0.02f : 0.14f);
                m.SetFloat("_AmbientStrength", 1.55f);   // overcast day: shadowed faces stay readable (concept sheets)
            }
            EditorUtility.SetDirty(m);
            Mats[key] = m;
            return m;
        }

        private static void Props(Transform zone, Dictionary<string, Transform> markers)
        {
            var root = new GameObject("Props").transform;
            root.SetParent(zone, false);
            foreach (KeyValuePair<string, Transform> kv in markers)
            {
                if (!kv.Key.StartsWith("PROP_")) continue;
                string kind = kv.Key.Split('_')[1];
                string path = ArtDir + "/Props/ZA_" + kind + ".fbx";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                GameObject prop = Place(path, root, true);
                // Keep the importer's axis conversion; add the marker's yaw (Blender Z rotation, mirrored by the X flip).
                prop.transform.SetPositionAndRotation(kv.Value.position, Quaternion.Euler(0f, -MarkerYaw(kv.Value), 0f) * prop.transform.rotation);
            }
        }

        /// <summary>Blender Z rotation (degrees) of an exported empty: angle of its Blender X axis (Unity -X) on the ground.</summary>
        private static float MarkerYaw(Transform marker)
        {
            // An unrotated Blender empty maps its local X to Unity -X; measure how far the marker turned it.
            Vector3 bx = marker.rotation * Quaternion.Inverse(marker.parent != null ? marker.parent.rotation : Quaternion.identity)
                         * Vector3.right;
            Vector3 flat = Vector3.ProjectOnPlane(bx, Vector3.up);
            return flat.sqrMagnitude < 1e-6f ? 0f : Vector3.SignedAngle(Vector3.right, flat, Vector3.up);
        }

        // ------------------------------------------------------------------ lighting / post
        private static void Lighting()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(38f, -42f, 0f);
            sun.color = new Color(0.86f, 0.9f, 0.98f);   // cold, overcast (concept: grey sky, soft shadows)
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.7f;
            RenderSettings.sun = sun;

            var sky = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/M_ZoneA_Sky.mat");
            if (sky == null)
            {
                sky = new Material(Shader.Find("Polykov/SkyGradient"));
                AssetDatabase.CreateAsset(sky, MaterialDir + "/M_ZoneA_Sky.mat");
            }
            sky.shader = Shader.Find("Polykov/SkyGradient");
            sky.SetColor("_Top", new Color(0.58f, 0.64f, 0.68f));      // concept: flat grey-blue overcast
            sky.SetColor("_Horizon", new Color(0.78f, 0.81f, 0.81f));
            sky.SetColor("_Bottom", new Color(0.55f, 0.57f, 0.57f));
            RenderSettings.skybox = sky;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.63f, 0.67f);
            RenderSettings.ambientEquatorColor = new Color(0.48f, 0.5f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.21f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0042f;
            RenderSettings.fogColor = new Color(0.66f, 0.7f, 0.72f);
        }

        private static void Volume()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumePath);
            }
            foreach (VolumeComponent c in new List<VolumeComponent>(profile.components)) Object.DestroyImmediate(c, true);
            profile.components.Clear();
            T Add<T>() where T : VolumeComponent
            {
                T c = profile.Add<T>(true);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }
            var tone = Add<Tonemapping>(); tone.mode.value = TonemappingMode.ACES;
            var ca = Add<ColorAdjustments>();
            ca.postExposure.value = 0.45f; ca.contrast.value = 10f; ca.saturation.value = -18f;
            ca.colorFilter.value = new Color(0.95f, 1f, 1f);
            var smh = Add<ShadowsMidtonesHighlights>();
            smh.shadows.value = new Vector4(0.9f, 1f, 1.06f, 0f);      // teal shadows like the sheets
            smh.highlights.value = new Vector4(1.02f, 1f, 0.97f, 0f);
            var bloom = Add<Bloom>(); bloom.threshold.value = 1.1f; bloom.intensity.value = 0.35f; bloom.scatter.value = 0.6f;
            var vig = Add<Vignette>(); vig.intensity.value = 0.26f; vig.smoothness.value = 0.45f;
            var grain = Add<FilmGrain>(); grain.type.value = FilmGrainLookup.Medium2; grain.intensity.value = 0.2f;
            var chroma = Add<ChromaticAberration>(); chroma.intensity.value = 0.04f;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            var v = new GameObject("PostProcess").AddComponent<Volume>();
            v.isGlobal = true;
            v.sharedProfile = profile;
        }

        // ------------------------------------------------------------------ particles
        private static Material ParticleMat(string name, Color tint, bool additive)
        {
            string path = MaterialDir + "/M_FX_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetTexture("_BaseMap", SoftDot());
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Texture2D SoftDot()
        {
            string path = MaterialDir + "/T_FX_SoftDot.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static ParticleSystem Particles(string name, Transform parent, Vector3 pos, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;
            main.loop = true;
            return ps;
        }

        private static void FadeInOut(ParticleSystem ps, Color? tint = null)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            Color c = tint ?? Color.white;
            g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        private static void Effects(Transform zone, Dictionary<string, Transform> markers)
        {
            var fx = new GameObject("Effects").transform;
            fx.SetParent(zone, false);
            foreach (KeyValuePair<string, Transform> kv in markers)
            {
                if (!kv.Key.StartsWith("FX_")) continue;
                Vector3 p = kv.Value.position;
                switch (kv.Key.Split('_')[1])
                {
                    case "ChimneySmoke": ChimneySmoke(fx, p); break;
                    case "HallDust": HallDust(fx, p); SkylightLights(fx, p); break;
                    case "Sparks": Sparks(fx, p); break;
                    case "Drip": Drip(fx, p); break;
                    case "BurnBarrel": BurnBarrel(fx, p); break;
                }
            }
        }

        private static void ChimneySmoke(Transform parent, Vector3 p)
        {
            var ps = Particles("ChimneySmoke", parent, p, ParticleMat("Smoke", new Color(0.62f, 0.64f, 0.65f, 0.5f), false));
            var m = ps.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(10f, 15f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            m.startSize = new ParticleSystem.MinMaxCurve(3f, 5f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            m.maxParticles = 160;
            var em = ps.emission; em.rateOverTime = 9f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1f; sh.rotation = new Vector3(-90f, 0f, 0f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(2f, 3f); vel.y = new ParticleSystem.MinMaxCurve(0.3f, 0.6f); vel.z = new ParticleSystem.MinMaxCurve(0.5f, 1f);
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 3.5f));
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            FadeInOut(ps);
        }

        private static void HallDust(Transform parent, Vector3 p)
        {
            var ps = Particles("HallDust", parent, p, ParticleMat("Dust", new Color(1f, 0.97f, 0.9f, 0.4f), false));
            var m = ps.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(12f, 20f); m.startSpeed = 0.02f;
            m.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f); m.maxParticles = 900; m.prewarm = true;
            var em = ps.emission; em.rateOverTime = 55f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(44f, 16f, 52f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.15f; noise.frequency = 0.15f; noise.scrollSpeed = 0.05f;
            FadeInOut(ps);
            // Light shafts through the skylights: a few tall, very faint additive columns.
            var shafts = Particles("SkylightShafts", parent, p + Vector3.up * 2f, ParticleMat("Shaft", new Color(1f, 0.98f, 0.92f, 0.05f), true));
            m = shafts.main; m.startLifetime = 20f; m.startSpeed = 0f; m.maxParticles = 10; m.prewarm = true;
            m.startSize3D = true; m.startSizeX = 4f; m.startSizeY = 18f; m.startSizeZ = 1f;
            em = shafts.emission; em.rateOverTime = 0.5f;
            sh = shafts.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(30f, 1f, 40f);
            var rend = shafts.GetComponent<ParticleSystemRenderer>(); rend.renderMode = ParticleSystemRenderMode.VerticalBillboard;
            FadeInOut(shafts);
        }

        /// <summary>Daylight falling through the roof skylights (phase 2 sheet): soft cold spots over the hall floor.</summary>
        private static void SkylightLights(Transform parent, Vector3 hallCentre)
        {
            var group = new GameObject("SkylightLights").transform;
            group.SetParent(parent, false);
            for (int ix = -1; ix <= 1; ix += 2)
                for (int iz = -2; iz <= 2; iz++)
                {
                    var l = new GameObject("Skylight").AddComponent<Light>();
                    l.transform.SetParent(group, false);
                    l.transform.position = new Vector3(hallCentre.x + ix * 11f, 19f, hallCentre.z + iz * 10.5f);
                    l.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    l.type = LightType.Spot;
                    l.spotAngle = 80f;
                    l.innerSpotAngle = 30f;
                    l.range = 30f;
                    l.intensity = 9f;
                    l.color = new Color(0.82f, 0.88f, 0.95f);
                    l.shadows = LightShadows.None;
                }
        }

        private static void Sparks(Transform parent, Vector3 p)
        {
            var ps = Particles("CableSparks", parent, p, ParticleMat("Spark", new Color(1f, 0.75f, 0.35f, 1f), true));
            var m = ps.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f); m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f); m.gravityModifier = 1f; m.maxParticles = 80;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 12, 22, 1, 3.2f) { probability = 0.8f } });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.05f;
            var col = ps.collision; col.enabled = true; col.type = ParticleSystemCollisionType.World; col.bounce = 0.3f; col.dampen = 0.4f;
            var trails = ps.trails; trails.enabled = true; trails.lifetime = 0.15f; trails.widthOverTrail = 0.4f;
            ps.GetComponent<ParticleSystemRenderer>().trailMaterial = ParticleMat("Spark", new Color(1f, 0.75f, 0.35f, 1f), true);
            var light = new GameObject("SparkLight").AddComponent<Light>();
            light.transform.SetParent(ps.transform, false);
            light.type = LightType.Point; light.color = new Color(1f, 0.7f, 0.4f); light.range = 5f; light.intensity = 2.5f;
            var f = light.gameObject.AddComponent<LightFlicker>();
            var so = new SerializedObject(f);
            so.FindProperty("dropoutsPerSecond").floatValue = 2.5f;
            so.FindProperty("amount").floatValue = 0.5f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Drip(Transform parent, Vector3 p)
        {
            var ps = Particles("RoofDrip", parent, p, ParticleMat("Drip", new Color(0.75f, 0.82f, 0.88f, 0.6f), false));
            var m = ps.main;
            m.startLifetime = 2f; m.startSpeed = 0f; m.gravityModifier = 1f; m.maxParticles = 30;
            m.startSize3D = true; m.startSizeX = 0.02f; m.startSizeY = 0.12f; m.startSizeZ = 0.02f;
            var em = ps.emission; em.rateOverTime = 1.6f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(0.2f, 0f, 0.2f);
            ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.Stretch;
            var col = ps.collision; col.enabled = true; col.type = ParticleSystemCollisionType.World; col.lifetimeLoss = 1f;
            var sub = ps.subEmitters; sub.enabled = true;
            var splash = Particles("Splash", ps.transform, p, ParticleMat("Drip", new Color(0.75f, 0.82f, 0.88f, 0.6f), false));
            var sm = splash.main; sm.loop = false; sm.playOnAwake = false; sm.startLifetime = 0.35f; sm.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            sm.startSize = 0.03f; sm.gravityModifier = 1.2f; sm.maxParticles = 60;
            var sem = splash.emission; sem.rateOverTime = 0f; sem.SetBursts(new[] { new ParticleSystem.Burst(0f, 4, 7) });
            var ssh = splash.shape; ssh.shapeType = ParticleSystemShapeType.Hemisphere; ssh.radius = 0.02f; ssh.rotation = new Vector3(-90f, 0f, 0f);
            sub.AddSubEmitter(splash, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing);
        }

        private static void BurnBarrel(Transform parent, Vector3 p)
        {
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = "BurnBarrel";
            barrel.transform.SetParent(parent, false);
            barrel.transform.position = p + Vector3.up * 0.45f;
            barrel.transform.localScale = new Vector3(0.6f, 0.45f, 0.6f);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/M_Zone_rust.mat");
            if (mat != null) barrel.GetComponent<Renderer>().sharedMaterial = mat;
            var fire = Particles("Fire", parent, p + Vector3.up * 0.95f, ParticleMat("Fire", new Color(1f, 0.55f, 0.18f, 0.9f), true));
            var m = fire.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f); m.maxParticles = 80;
            var em = fire.emission; em.rateOverTime = 40f;
            var sh = fire.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.22f; sh.rotation = new Vector3(-90f, 0f, 0f);
            var size = fire.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
            var col = fire.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(0.9f, 0.25f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var smoke = Particles("FireSmoke", parent, p + Vector3.up * 1.3f, ParticleMat("Smoke", new Color(0.3f, 0.3f, 0.3f, 0.4f), false));
            m = smoke.main; m.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.8f); m.maxParticles = 60;
            em = smoke.emission; em.rateOverTime = 8f;
            var ssize = smoke.sizeOverLifetime; ssize.enabled = true; ssize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2.5f));
            FadeInOut(smoke);
            var light = new GameObject("FireLight").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.position = p + Vector3.up * 1.6f;
            light.type = LightType.Point; light.color = new Color(1f, 0.55f, 0.25f); light.intensity = 3f; light.range = 8f;
            light.gameObject.AddComponent<LightFlicker>();
        }

        // ------------------------------------------------------------------ extraction + actors
        private static void Extraction(Transform zone, Dictionary<string, Transform> markers)
        {
            if (!markers.TryGetValue("EXTRACT_Helipad", out Transform m)) return;
            var g = new GameObject("Extraction_Helipad").transform;
            g.SetParent(zone, false);
            g.position = m.position;
            var box = g.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            Vector3 s = m.lossyScale;
            box.size = new Vector3(Mathf.Abs(s.x) * 2f, Mathf.Abs(s.y) * 2f, Mathf.Abs(s.z) * 2f);
            var zoneComp = g.gameObject.AddComponent<ExtractionZone>();
            var so = new SerializedObject(zoneComp);
            so.FindProperty("displayName").stringValue = "Helipuerto";
            so.ApplyModifiedPropertiesWithoutUndo();
            var smoke = Particles("GreenFlare", g, m.position + new Vector3(3f, -m.position.y + 0.1f, 3f),
                ParticleMat("GreenSmoke", new Color(0.35f, 0.85f, 0.35f, 0.5f), false));
            var mm = smoke.main; mm.startLifetime = new ParticleSystem.MinMaxCurve(5f, 7f); mm.startSpeed = new ParticleSystem.MinMaxCurve(1f, 1.6f);
            mm.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.4f); mm.maxParticles = 200;
            var em = smoke.emission; em.rateOverTime = 22f;
            var sh = smoke.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 8f; sh.radius = 0.05f; sh.rotation = new Vector3(-90f, 0f, 0f);
            var size = smoke.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 4f));
            var vel = smoke.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.6f, 1.2f); vel.y = new ParticleSystem.MinMaxCurve(0f, 0f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            FadeInOut(smoke);
            var light = new GameObject("FlareLight").AddComponent<Light>();
            light.transform.SetParent(g, false);
            light.transform.position = smoke.transform.position + Vector3.up * 0.5f;
            light.type = LightType.Point; light.color = new Color(0.5f, 1f, 0.45f); light.intensity = 3f; light.range = 10f;
            light.gameObject.AddComponent<LightFlicker>();
        }

        private static void Actors(Dictionary<string, Transform> markers)
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            if (player != null)
            {
                var p = (GameObject)PrefabUtility.InstantiatePrefab(player);
                if (markers.TryGetValue("SPAWN_MainEntry", out Transform s))
                    p.transform.SetPositionAndRotation(s.position + Vector3.up * 0.1f, Quaternion.Euler(0f, s.eulerAngles.y, 0f));
                foreach (Camera cam in p.GetComponentsInChildren<Camera>(true))
                {
                    var data = cam.GetUniversalAdditionalCameraData();
                    data.renderPostProcessing = true;
                    data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    cam.farClipPlane = Mathf.Max(cam.farClipPlane, 600f);
                }
            }
            var scav = AssetDatabase.LoadAssetAtPath<GameObject>(ScavPrefab);
            if (scav == null) return;
            var root = new GameObject("Scavs").transform;
            foreach (KeyValuePair<string, Transform> kv in markers)
            {
                if (!kv.Key.StartsWith("SCAV_")) continue;
                var s = (GameObject)PrefabUtility.InstantiatePrefab(scav, root);
                s.transform.SetPositionAndRotation(kv.Value.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            }
        }

        private static void AddToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
