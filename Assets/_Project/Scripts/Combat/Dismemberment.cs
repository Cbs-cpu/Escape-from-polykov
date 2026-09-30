using System.Collections.Generic;
using Polykov.Weapons;
using UnityEngine;

namespace Polykov.Combat
{
    /// <summary>
    /// Visual dismemberment, done at runtime on the character's own skinned meshes (no model edits needed):
    /// the triangles driven by the severed bones are cut out of the body mesh, a flesh cap closes the stump, and the
    /// removed part becomes a physical object with its own copy of the bone chain that flies off with the bullet's
    /// momentum. The decision itself (when and what) comes from <see cref="HealthComponent.Severed"/> (WoundModel).
    /// Needs readable meshes (the importer enables it for the Scav).
    /// </summary>
    public sealed class Dismemberment : MonoBehaviour
    {
        private const int MaxLimbsAlive = 8;
        private const float LimbLifetime = 45f;
        // Reduced gore: fewer, shorter-lived limbs (performance and taste).
        private static int LimbCap => Polykov.Core.UserSettings.Gore == Polykov.Core.GoreLevel.Full ? MaxLimbsAlive : 3;
        private static float LimbSeconds => Polykov.Core.UserSettings.Gore == Polykov.Core.GoreLevel.Full ? LimbLifetime : 15f;

        private static readonly Queue<GameObject> AliveLimbs = new Queue<GameObject>();

        [SerializeField] private Color fleshColor = new Color(0.52f, 0.1f, 0.09f);

        private sealed class Original
        {
            public SkinnedMeshRenderer Renderer;
            public Mesh Mesh;
            public Material[] Materials;
            public bool Enabled;
        }

        private Animator _animator;
        private HealthComponent _health;
        private SkinnedMeshRenderer[] _renderers;
        private readonly List<Original> _originals = new List<Original>();
        private readonly List<Mesh> _createdMeshes = new List<Mesh>();
        private readonly List<Collider> _disabledColliders = new List<Collider>();
        private readonly List<Rigidbody> _frozenBodies = new List<Rigidbody>();
        private readonly HashSet<BodyPart> _severed = new HashSet<BodyPart>();
        private Material _capMaterial;
        private bool _warnedUnreadable;

        public bool AnySevered => _severed.Count > 0;
        public bool IsSevered(BodyPart part) => _severed.Contains(part);

        public void Init(Animator animator, HealthComponent health)
        {
            _animator = animator;
            _health = health;
            _renderers = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            _health.Severed += OnSevered;
            _health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.Severed -= OnSevered;
                _health.Died -= OnDied;
            }
            foreach (Mesh m in _createdMeshes) if (m != null) Destroy(m);
            if (_capMaterial != null) Destroy(_capMaterial);
        }

        private void OnDied(BodyPart part)
        {
            // The ragdoll wakes every rigidbody; the ones of severed parts must stay put (their mesh is gone).
            foreach (Rigidbody rb in _frozenBodies)
                if (rb != null) rb.isKinematic = true;
        }

        private void OnSevered(BodyPart part, ShotHit hit)
        {
            if (!_severed.Add(part) || _animator == null) return;
            Transform root = RegionRoot(part, _health.LastHitBone);
            if (root == null) return;

            Transform parent = root.parent != null ? root.parent : root;
            Vector3 origin = root.position;
            Vector3 limbDirection = (root.position - parent.position).normalized;
            if (limbDirection.sqrMagnitude < 1e-6f) limbDirection = Vector3.up;

            var limbRenderers = new List<(SkinnedMeshRenderer source, Mesh limbMesh)>();
            foreach (SkinnedMeshRenderer smr in _renderers)
            {
                Mesh limbMesh = SplitRenderer(smr, root, limbDirection);
                if (limbMesh != null) limbRenderers.Add((smr, limbMesh));
            }
            if (limbRenderers.Count == 0) return;

            DisablePhysics(root);
            SpawnLimb(part, root, parent, limbRenderers, hit, limbDirection);

            WeaponEffects.SharedBlood?.Spurt(origin, limbDirection, part == BodyPart.Head ? 1.3f : 1f);
        }

        /// <summary>Puts the body back together (dummy respawn).</summary>
        public void Restore()
        {
            foreach (Original o in _originals)
            {
                if (o.Renderer == null) continue;
                o.Renderer.sharedMesh = o.Mesh;
                o.Renderer.sharedMaterials = o.Materials;
                o.Renderer.enabled = o.Enabled;
            }
            _originals.Clear();
            foreach (Collider c in _disabledColliders) if (c != null) c.enabled = true;
            _disabledColliders.Clear();
            _frozenBodies.Clear();
            _severed.Clear();
        }

        // ------------------------------------------------------------------------------------------- regions

        /// <summary>Bone whose whole subtree is cut off. Arms and legs go at the elbow/knee when the hit was below it.</summary>
        private Transform RegionRoot(BodyPart part, Transform hitBone)
        {
            bool Below(HumanBodyBones joint) => hitBone != null && _animator.GetBoneTransform(joint) is Transform t && hitBone.IsChildOf(t);
            switch (part)
            {
                case BodyPart.Head: return _animator.GetBoneTransform(HumanBodyBones.Head);
                case BodyPart.LeftArm:
                    return _animator.GetBoneTransform(Below(HumanBodyBones.LeftLowerArm) ? HumanBodyBones.LeftLowerArm : HumanBodyBones.LeftUpperArm);
                case BodyPart.RightArm:
                    return _animator.GetBoneTransform(Below(HumanBodyBones.RightLowerArm) ? HumanBodyBones.RightLowerArm : HumanBodyBones.RightUpperArm);
                case BodyPart.LeftLeg:
                    return _animator.GetBoneTransform(Below(HumanBodyBones.LeftLowerLeg) ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.LeftUpperLeg);
                case BodyPart.RightLeg:
                    return _animator.GetBoneTransform(Below(HumanBodyBones.RightLowerLeg) ? HumanBodyBones.RightLowerLeg : HumanBodyBones.RightUpperLeg);
                default: return null;
            }
        }

        private void DisablePhysics(Transform root)
        {
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            {
                if (!c.enabled) continue;
                c.enabled = false;
                _disabledColliders.Add(c);
            }
            foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.isKinematic = true;
                _frozenBodies.Add(rb);
            }
        }

        // ------------------------------------------------------------------------------------------- mesh cutting

        /// <summary>Cuts the region out of one renderer. Returns the mesh of the removed part (null if it has none).</summary>
        private Mesh SplitRenderer(SkinnedMeshRenderer smr, Transform root, Vector3 worldLimbDirection)
        {
            Mesh mesh = smr.sharedMesh;
            if (mesh == null) return null;
            if (!mesh.isReadable)
            {
                if (!_warnedUnreadable)
                {
                    _warnedUnreadable = true;
                    Debug.LogWarning("[Polykov] Dismemberment needs a readable mesh (" + mesh.name + "): enable Read/Write on the model import.");
                }
                return null;
            }

            Transform[] bones = smr.bones;
            var region = new bool[bones.Length];
            bool any = false;
            for (int i = 0; i < bones.Length; i++)
            {
                region[i] = bones[i] != null && bones[i].IsChildOf(root);
                any |= region[i];
            }
            if (!any) return null;

            Vector3[] vertices = mesh.vertices;
            BoneWeight[] unityWeights = mesh.boneWeights;
            var weights = new SkinWeight[unityWeights.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                BoneWeight w = unityWeights[i];
                weights[i] = new SkinWeight(w.boneIndex0, w.weight0, w.boneIndex1, w.weight1, w.boneIndex2, w.weight2, w.boneIndex3, w.weight3);
            }
            int submeshes = mesh.subMeshCount;
            var triangles = new List<int[]>(submeshes);
            for (int s = 0; s < submeshes; s++) triangles.Add(mesh.GetTriangles(s));

            MeshSplitResult split = MeshSplitter.Split(vertices, weights, triangles, region);
            if (split.LimbTriangleCount == 0) return null;

            // Remember the original so a respawn can undo the cut.
            _originals.Add(new Original { Renderer = smr, Mesh = mesh, Materials = smr.sharedMaterials, Enabled = smr.enabled });

            // Skinning-space direction of the limb (from the parent bone to the region root, in bind pose).
            Matrix4x4[] bindposes = mesh.bindposes;
            int rootIndex = System.Array.IndexOf(bones, root);
            int parentIndex = root.parent != null ? System.Array.IndexOf(bones, root.parent) : -1;
            Vector3 meshDirection = MeshSpaceDirection(bindposes, rootIndex, parentIndex, smr, worldLimbDirection);
            int capBone = parentIndex >= 0 ? parentIndex : Mathf.Max(0, rootIndex);

            // Cap loops from the cut edges (rest side vertex positions).
            var edges = new List<(Vector3, Vector3)>(split.CutEdges.Count);
            foreach ((int a, int b) in split.CutEdges) edges.Add((vertices[a], vertices[b]));
            List<List<Vector3>> loops = StumpCap.Loops(edges);

            Mesh rest = BuildMesh(mesh, vertices, split.Rest, loops, meshDirection, capBone, smr.sharedMaterials.Length, capFacesOutward: true);
            Mesh limb = BuildMesh(mesh, vertices, split.Limb, loops, -meshDirection, capBone, smr.sharedMaterials.Length, capFacesOutward: false);
            _createdMeshes.Add(rest);
            _createdMeshes.Add(limb);

            Material[] materials = smr.sharedMaterials;
            var withCap = new Material[materials.Length + 1];
            System.Array.Copy(materials, withCap, materials.Length);
            withCap[materials.Length] = CapMaterial(materials);
            smr.sharedMesh = rest;
            smr.sharedMaterials = withCap;
            // A renderer that lost every triangle (the head mesh) is simply hidden.
            if (split.RestTriangleCount == 0) smr.enabled = false;
            return limb;
        }

        private static Vector3 MeshSpaceDirection(Matrix4x4[] bindposes, int rootIndex, int parentIndex, SkinnedMeshRenderer smr,
            Vector3 worldDirection)
        {
            if (rootIndex >= 0 && parentIndex >= 0)
            {
                Vector3 rootPos = bindposes[rootIndex].inverse.MultiplyPoint3x4(Vector3.zero);
                Vector3 parentPos = bindposes[parentIndex].inverse.MultiplyPoint3x4(Vector3.zero);
                Vector3 d = rootPos - parentPos;
                if (d.sqrMagnitude > 1e-8f) return d.normalized;
            }
            return smr.transform.InverseTransformDirection(worldDirection).normalized;
        }

        private Mesh BuildMesh(Mesh source, Vector3[] vertices, List<int>[] trianglesPerSubmesh, List<List<Vector3>> loops,
            Vector3 capDirection, int capBone, int originalSubmeshes, bool capFacesOutward)
        {
            Vector3[] normals = source.normals;
            Vector4[] tangents = source.tangents;
            Vector2[] uv = source.uv;
            BoneWeight[] boneWeights = source.boneWeights;

            var v = new List<Vector3>(vertices);
            var n = new List<Vector3>(normals);
            var t = new List<Vector4>(tangents);
            var uvs = new List<Vector2>(uv);
            var bw = new List<BoneWeight>(boneWeights);
            var capTriangles = new List<int>();

            foreach (List<Vector3> loop in loops)
            {
                StumpCap.Cap cap = StumpCap.Build(loop, capDirection);
                int baseIndex = v.Count;
                Vector3 normal = capDirection.normalized;
                foreach (Vector3 p in cap.Vertices)
                {
                    v.Add(p);
                    n.Add(normal);
                    t.Add(new Vector4(1f, 0f, 0f, 1f));
                    uvs.Add(new Vector2(0.5f, 0.5f));
                    bw.Add(new BoneWeight { boneIndex0 = capBone, weight0 = 1f });
                }
                foreach (int index in cap.Triangles) capTriangles.Add(baseIndex + index);
            }

            var result = new Mesh { name = source.name + (capFacesOutward ? "_Stump" : "_Limb"), indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            result.SetVertices(v);
            result.SetNormals(n);
            result.SetTangents(t);
            result.SetUVs(0, uvs);
            result.boneWeights = bw.ToArray();
            result.bindposes = source.bindposes;
            result.subMeshCount = originalSubmeshes + 1;
            for (int s = 0; s < originalSubmeshes; s++) result.SetTriangles(trianglesPerSubmesh[s], s, false);
            result.SetTriangles(capTriangles, originalSubmeshes, false);
            result.RecalculateBounds();
            return result;
        }

        private Material CapMaterial(Material[] sources)
        {
            if (_capMaterial != null) return _capMaterial;
            Material template = sources.Length > 0 ? sources[0] : null;
            _capMaterial = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _capMaterial.name = "M_Flesh_Cap";
            foreach (string texture in new[] { "_BaseMap", "_MainTex", "_BumpMap", "_MetallicGlossMap" })
                if (_capMaterial.HasProperty(texture)) _capMaterial.SetTexture(texture, null);
            if (_capMaterial.HasProperty("_BaseColor")) _capMaterial.SetColor("_BaseColor", fleshColor);
            if (_capMaterial.HasProperty("_Color")) _capMaterial.SetColor("_Color", fleshColor);
            if (_capMaterial.HasProperty("_Smoothness")) _capMaterial.SetFloat("_Smoothness", 0.35f);
            if (_capMaterial.HasProperty("_Metallic")) _capMaterial.SetFloat("_Metallic", 0f);
            return _capMaterial;
        }

        // ------------------------------------------------------------------------------------------- the flying part

        private void SpawnLimb(BodyPart part, Transform root, Transform parent, List<(SkinnedMeshRenderer source, Mesh limbMesh)> renderers,
            ShotHit hit, Vector3 limbDirection)
        {
            var limbObject = new GameObject("Severed_" + part) { layer = CombatLayers.Ragdoll };
            limbObject.transform.SetPositionAndRotation(root.position, root.rotation);

            // Copy of the bone chain with the same world poses; everything else collapses to a frozen copy of the parent.
            var map = new Dictionary<Transform, Transform>();
            Transform frozenParent = new GameObject("CutParent").transform;
            frozenParent.SetParent(limbObject.transform, false);
            frozenParent.SetPositionAndRotation(parent.position, parent.rotation);
            Transform clonedRoot = CloneChain(root, limbObject.transform, map);

            foreach ((SkinnedMeshRenderer source, Mesh mesh) in renderers)
            {
                Transform[] sourceBones = source.bones;
                var bones = new Transform[sourceBones.Length];
                for (int i = 0; i < bones.Length; i++)
                    bones[i] = sourceBones[i] != null && map.TryGetValue(sourceBones[i], out Transform c) ? c : frozenParent;

                var go = new GameObject("LimbMesh") { layer = CombatLayers.Ragdoll };
                go.transform.SetParent(limbObject.transform, false);
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.sharedMaterials = source.sharedMaterials;
                smr.bones = bones;
                smr.rootBone = clonedRoot;
                smr.localBounds = new Bounds(source.localBounds.center, source.localBounds.size);
                smr.updateWhenOffscreen = true;
                smr.shadowCastingMode = source.shadowCastingMode;
            }

            AddLimbPhysics(part, clonedRoot, hit, limbDirection);

            AliveLimbs.Enqueue(limbObject);
            while (AliveLimbs.Count > LimbCap)
            {
                GameObject oldest = AliveLimbs.Dequeue();
                if (oldest != null) Destroy(oldest);
            }
            Destroy(limbObject, LimbSeconds);
        }

        private static Transform CloneChain(Transform source, Transform parent, Dictionary<Transform, Transform> map)
        {
            var clone = new GameObject(source.name) { layer = parent.gameObject.layer }.transform;
            clone.SetParent(parent, false);
            clone.SetPositionAndRotation(source.position, source.rotation);
            clone.localScale = source.lossyScale;
            map[source] = clone;
            foreach (Transform child in source)
            {
                // Only bones: skip hitbox helpers and other non-skeleton children.
                if (child.GetComponent<Hitbox>() != null || child.name.StartsWith("Hitbox_")) continue;
                CloneChain(child, clone, map);
            }
            return clone;
        }

        private static void AddLimbPhysics(BodyPart part, Transform root, ShotHit hit, Vector3 limbDirection)
        {
            float radius = part == BodyPart.Head ? 0.11f : part == BodyPart.LeftLeg || part == BodyPart.RightLeg ? 0.075f : 0.06f;
            // Compound collider: a sphere at each bone that has a child (upper arm, forearm, hand...) plus one at the tip.
            foreach (Transform bone in root.GetComponentsInChildren<Transform>())
            {
                if (bone.childCount == 0 && bone != root && bone.parent != root && bone.parent.childCount > 3) continue; // fingers
                if (bone.name.Contains("Thumb") || bone.name.Contains("Index") || bone.name.Contains("Middle")
                    || bone.name.Contains("Ring") || bone.name.Contains("Little")) continue;
                var sphere = bone.gameObject.AddComponent<SphereCollider>();
                sphere.radius = radius;
                if (bone.childCount > 0 && !bone.GetChild(0).name.Contains("Thumb"))
                    sphere.center = bone.InverseTransformPoint(Vector3.Lerp(bone.position, bone.GetChild(0).position, 0.5f));
            }

            var body = root.gameObject.AddComponent<Rigidbody>();
            body.mass = part == BodyPart.Head ? 4.5f : part == BodyPart.LeftLeg || part == BodyPart.RightLeg ? 9f : 3.5f;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.4f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // Thrown with the bullet's momentum (heavier limbs fly less) plus a tumble.
            float speed = Mathf.Clamp(hit.Impulse * 2.2f / Mathf.Sqrt(body.mass), 1.5f, 7f);
            Vector3 shot = hit.Direction.sqrMagnitude > 1e-6f ? hit.Direction.normalized : limbDirection;
            body.linearVelocity = shot * speed + limbDirection * 1.2f + Vector3.up * 1.4f;
            body.angularVelocity = Random.onUnitSphere * Random.Range(3f, 9f);
        }
    }
}
