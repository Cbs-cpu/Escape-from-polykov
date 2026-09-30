using System.Collections.Generic;
using UnityEngine;

namespace Polykov.Combat
{
    /// <summary>Physics layers used by combat, plus the collision rules between them.</summary>
    public static class CombatLayers
    {
        public const string EnemyName = "Enemy";
        public const string RagdollName = "Ragdoll";

        /// <summary>Layer of hitboxes (raycast only). 0 when the layer is not defined.</summary>
        public static int Enemy => Mathf.Max(0, LayerMask.NameToLayer(EnemyName));
        /// <summary>Layer of ragdoll physics colliders. 0 when the layer is not defined.</summary>
        public static int Ragdoll => Mathf.Max(0, LayerMask.NameToLayer(RagdollName));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyCollisionRules()
        {
            int enemy = LayerMask.NameToLayer(EnemyName);
            int ragdoll = LayerMask.NameToLayer(RagdollName);
            int player = LayerMask.NameToLayer("Player");
            int debris = LayerMask.NameToLayer("Debris");
            if (enemy >= 0)
                for (int i = 0; i < 32; i++) Physics.IgnoreLayerCollision(enemy, i, true); // hitboxes never collide
            if (ragdoll < 0) return;
            if (player >= 0) Physics.IgnoreLayerCollision(ragdoll, player, true);
            if (debris >= 0) Physics.IgnoreLayerCollision(ragdoll, debris, true);
        }
    }

    /// <summary>
    /// Physical ragdoll built from a humanoid skeleton at the moment of death (so joint limits are relative to the
    /// current pose): 11 bodies with capsule/box colliders sized from the bone lengths, ~75 kg, CharacterJoints with
    /// anatomical limits, interpolation and joint projection. At most <see cref="MaxSimultaneous"/> ragdolls keep
    /// simulating; older ones are frozen where they lie.
    /// </summary>
    public sealed class Ragdoll : MonoBehaviour
    {
        public const int MaxSimultaneous = 6;

        private static RagdollBudget Budget = new RagdollBudget(MaxSimultaneous);
        private static readonly Dictionary<int, Ragdoll> Live = new Dictionary<int, Ragdoll>();
        private static PhysicsMaterial _material;

        private readonly Rigidbody[] _bodies = new Rigidbody[RagdollProfile.Count];
        private readonly Collider[] _shapes = new Collider[RagdollProfile.Count];
        private readonly List<GameObject> _colliderObjects = new List<GameObject>(16);
        private readonly List<Joint> _joints = new List<Joint>(16);
        private Transform[] _skeleton;
        private Vector3[] _restPositions;
        private Quaternion[] _restRotations;
        private static int _nextId;
        private readonly int _id = ++_nextId;
        private Animator _animator;

        public bool Active { get; private set; }

        public Rigidbody Body(RagdollBone bone) => _bodies[(int)bone];

        public void Init(Animator animator)
        {
            _animator = animator;
            _skeleton = animator.GetComponentsInChildren<Transform>(true);
            _restPositions = new Vector3[_skeleton.Length];
            _restRotations = new Quaternion[_skeleton.Length];
            for (int i = 0; i < _skeleton.Length; i++)
            {
                _restPositions[i] = _skeleton[i].localPosition;
                _restRotations[i] = _skeleton[i].localRotation;
            }
        }

        /// <summary>Turns the animated character into a ragdoll moving with <paramref name="velocity"/>.</summary>
        public void Activate(Vector3 velocity)
        {
            if (Active || _animator == null) return;
            Active = true;
            _animator.enabled = false;
            Build();
            foreach (Rigidbody body in _bodies)
            {
                if (body == null) continue;
                body.linearVelocity = velocity;
                body.angularVelocity = Vector3.zero;
            }

            int id = _id;
            Live[id] = this;
            int evicted = Budget.Register(id);
            if (evicted >= 0 && Live.TryGetValue(evicted, out Ragdoll old))
            {
                Live.Remove(evicted);
                if (old != null) old.Freeze();
            }
        }

        /// <summary>Pushes the body: <paramref name="hitTransform"/> tells which body was hit.</summary>
        public void AddImpulse(Transform hitTransform, Vector3 point, Vector3 direction, float impulse)
        {
            if (!Active || hitTransform == null) return;
            Rigidbody body = hitTransform.GetComponentInParent<Rigidbody>();
            if (body == null || body.isKinematic) return;
            body.AddForceAtPosition(direction * impulse, point, ForceMode.Impulse);
            // A bullet also moves the whole body a little, not only the limb it struck.
            Rigidbody hips = _bodies[(int)RagdollBone.Hips];
            if (hips != null && hips != body) hips.AddForce(direction * (impulse * 0.25f), ForceMode.Impulse);
        }

        /// <summary>Stops simulating and keeps the body where it lies (ragdoll budget).</summary>
        public void Freeze()
        {
            foreach (Rigidbody body in _bodies)
            {
                if (body == null) continue;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
        }

        /// <summary>Back to the animated character (respawn): removes bodies and joints, restores the pose.</summary>
        public void Deactivate()
        {
            if (!Active) return;
            Active = false;
            int id = _id;
            Budget.Remove(id);
            Live.Remove(id);
            foreach (Joint joint in _joints) if (joint != null) Destroy(joint);
            _joints.Clear();
            foreach (GameObject go in _colliderObjects) if (go != null) Destroy(go);
            _colliderObjects.Clear();
            for (int i = 0; i < _bodies.Length; i++)
            {
                if (_bodies[i] != null) Destroy(_bodies[i]);
                _bodies[i] = null;
                _shapes[i] = null;
            }
            for (int i = 0; i < _skeleton.Length; i++)
                if (_skeleton[i] != null) _skeleton[i].SetLocalPositionAndRotation(_restPositions[i], _restRotations[i]);
            _animator.enabled = true;
        }

        private void OnDestroy()
        {
            int id = _id;
            Budget.Remove(id);
            Live.Remove(id);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Live.Clear();
            Budget = new RagdollBudget(MaxSimultaneous);
            _material = null;
        }

        // ---- construction ----

        private void Build()
        {
            Animator a = _animator;
            Transform root = a.transform;
            Transform hips = a.GetBoneTransform(HumanBodyBones.Hips);
            Transform spineBone = a.GetBoneTransform(HumanBodyBones.Spine);
            Transform chest = a.GetBoneTransform(HumanBodyBones.UpperChest) ??
                              a.GetBoneTransform(HumanBodyBones.Chest) ?? spineBone;
            Transform neck = a.GetBoneTransform(HumanBodyBones.Neck);
            Transform head = a.GetBoneTransform(HumanBodyBones.Head);
            if (neck == null) neck = head;
            Transform lua = a.GetBoneTransform(HumanBodyBones.LeftUpperArm), lla = a.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Transform lh = a.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform rua = a.GetBoneTransform(HumanBodyBones.RightUpperArm), rla = a.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Transform rh = a.GetBoneTransform(HumanBodyBones.RightHand);
            Transform lul = a.GetBoneTransform(HumanBodyBones.LeftUpperLeg), lll = a.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            Transform lf = a.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rul = a.GetBoneTransform(HumanBodyBones.RightUpperLeg), rll = a.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            Transform rf = a.GetBoneTransform(HumanBodyBones.RightFoot);

            float hipsWidth = Mathf.Max(0.2f, Vector3.Distance(lul.position, rul.position) * 1.25f);
            float shoulderWidth = Mathf.Max(0.25f, Vector3.Distance(lua.position, rua.position) * 0.85f);
            float footHeight = Mathf.Max(0.06f, lf.position.y - root.position.y);
            Vector3 up = root.up, fwd = root.forward;

            // Bodies (rigidbodies live on the animated bones so the skin follows them).
            Vector3 hipsCenter = hips.position + up * 0.02f;
            AddBox(RagdollBone.Hips, hips, hipsCenter, new Vector3(hipsWidth, 0.2f, 0.2f), root);
            Vector3 torsoBottom = Vector3.Lerp(hips.position, spineBone.position, 0.5f);
            Vector3 torsoTop = neck.position;
            AddBox(RagdollBone.Spine, chest, (torsoBottom + torsoTop) * 0.5f,
                new Vector3(shoulderWidth, Vector3.Distance(torsoBottom, torsoTop) + 0.06f, 0.2f), root);
            AddSphere(RagdollBone.Head, head, head.position + up * 0.1f, 0.105f);
            AddLimb(RagdollBone.LeftUpperArm, lua, lla.position, 0.048f, 0f);
            AddLimb(RagdollBone.LeftLowerArm, lla, lh.position, 0.04f, 0.09f);
            AddLimb(RagdollBone.RightUpperArm, rua, rla.position, 0.048f, 0f);
            AddLimb(RagdollBone.RightLowerArm, rla, rh.position, 0.04f, 0.09f);
            AddLimb(RagdollBone.LeftUpperLeg, lul, lll.position, 0.075f, 0f);
            AddLimb(RagdollBone.LeftLowerLeg, lll, lf.position, 0.055f, footHeight);
            AddLimb(RagdollBone.RightUpperLeg, rul, rll.position, 0.075f, 0f);
            AddLimb(RagdollBone.RightLowerLeg, rll, rf.position, 0.055f, footHeight);

            // Joints (hinge axes computed from the current pose: elbows fold forward, knees fold back).
            AddJoint(RagdollBone.Spine, up, Vector3.Cross(up, fwd), chest.position);
            AddJoint(RagdollBone.Head, up, Vector3.Cross(up, fwd), head.position);
            JointLimb(RagdollBone.LeftUpperArm, lua, lla, fwd);
            JointLimb(RagdollBone.RightUpperArm, rua, rla, fwd);
            JointLimb(RagdollBone.LeftLowerArm, lla, lh, fwd);
            JointLimb(RagdollBone.RightLowerArm, rla, rh, fwd);
            JointLimb(RagdollBone.LeftUpperLeg, lul, lll, fwd);
            JointLimb(RagdollBone.RightUpperLeg, rul, rll, fwd);
            JointLimb(RagdollBone.LeftLowerLeg, lll, lf, fwd);
            JointLimb(RagdollBone.RightLowerLeg, rll, rf, fwd);

            // Overlapping neighbours two joints apart would push each other apart at the first step.
            Ignore(RagdollBone.Head, RagdollBone.Hips);
            Ignore(RagdollBone.LeftUpperArm, RagdollBone.Hips);
            Ignore(RagdollBone.RightUpperArm, RagdollBone.Hips);
            Ignore(RagdollBone.LeftLowerArm, RagdollBone.Spine);
            Ignore(RagdollBone.RightLowerArm, RagdollBone.Spine);
            Ignore(RagdollBone.LeftUpperLeg, RagdollBone.Spine);
            Ignore(RagdollBone.RightUpperLeg, RagdollBone.Spine);
            Ignore(RagdollBone.LeftLowerLeg, RagdollBone.Hips);
            Ignore(RagdollBone.RightLowerLeg, RagdollBone.Hips);
            Ignore(RagdollBone.LeftUpperLeg, RagdollBone.RightUpperLeg);
            Ignore(RagdollBone.LeftLowerLeg, RagdollBone.RightLowerLeg);
        }

        private Rigidbody Body(RagdollBone bone, Transform boneTransform)
        {
            var rb = boneTransform.gameObject.AddComponent<Rigidbody>();
            rb.mass = RagdollProfile.Mass(bone);
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.35f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.solverIterations = 12;
            rb.solverVelocityIterations = 4;
            rb.maxAngularVelocity = 16f;
            rb.sleepThreshold = 0.08f;
            _bodies[(int)bone] = rb;
            return rb;
        }

        private GameObject ColliderHost(RagdollBone bone, Transform boneTransform)
        {
            var host = new GameObject("Ragdoll_" + bone) { layer = CombatLayers.Ragdoll };
            host.transform.SetParent(boneTransform, false);
            _colliderObjects.Add(host);
            return host;
        }

        private static PhysicsMaterial SharedMaterial()
        {
            if (_material == null)
            {
                _material = new PhysicsMaterial("Ragdoll")
                {
                    dynamicFriction = 0.7f,
                    staticFriction = 0.8f,
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Average,
                    bounceCombine = PhysicsMaterialCombine.Minimum,
                };
            }
            return _material;
        }

        private void AddBox(RagdollBone bone, Transform t, Vector3 worldCenter, Vector3 rootSize, Transform root)
        {
            Body(bone, t);
            GameObject host = ColliderHost(bone, t);
            var box = host.AddComponent<BoxCollider>();
            box.sharedMaterial = SharedMaterial();
            _shapes[(int)bone] = box;
            box.center = t.InverseTransformPoint(worldCenter);
            Vector3 size = Vector3.zero;
            Vector3 scale = t.lossyScale;
            for (int i = 0; i < 3; i++)
            {
                Vector3 axis = i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
                Vector3 d = root.InverseTransformDirection(t.TransformDirection(axis));
                size[i] = (Mathf.Abs(d.x) * rootSize.x + Mathf.Abs(d.y) * rootSize.y + Mathf.Abs(d.z) * rootSize.z) /
                          Mathf.Max(1e-4f, Mathf.Abs(scale[i]));
            }
            box.size = size;
        }

        private void AddSphere(RagdollBone bone, Transform t, Vector3 worldCenter, float radius)
        {
            Body(bone, t);
            GameObject host = ColliderHost(bone, t);
            var sphere = host.AddComponent<SphereCollider>();
            sphere.sharedMaterial = SharedMaterial();
            _shapes[(int)bone] = sphere;
            sphere.center = t.InverseTransformPoint(worldCenter);
            sphere.radius = radius / Mathf.Max(1e-4f, MaxAbs(t.lossyScale));
        }

        private void AddLimb(RagdollBone bone, Transform t, Vector3 endWorld, float radius, float extend)
        {
            Body(bone, t);
            GameObject host = ColliderHost(bone, t);
            Vector3 dirWorld = (endWorld - t.position).normalized;
            Vector3 end = endWorld + dirWorld * extend;
            Vector3 local = t.InverseTransformPoint(end);
            int axis = 0;
            for (int i = 1; i < 3; i++) if (Mathf.Abs(local[i]) > Mathf.Abs(local[axis])) axis = i;
            float length = Mathf.Abs(local[axis]);
            var capsule = host.AddComponent<CapsuleCollider>();
            capsule.sharedMaterial = SharedMaterial();
            _shapes[(int)bone] = capsule;
            capsule.direction = axis;
            capsule.radius = radius / Mathf.Max(1e-4f, MaxAbs(t.lossyScale));
            capsule.height = length + capsule.radius;
            Vector3 center = Vector3.zero;
            center[axis] = local[axis] * 0.5f;
            capsule.center = center;
        }

        private void JointLimb(RagdollBone bone, Transform t, Transform child, Vector3 forward)
        {
            Vector3 d = (child.position - t.position).normalized;
            bool lower = bone == RagdollBone.LeftLowerArm || bone == RagdollBone.RightLowerArm;
            if (RagdollProfile.IsHinge(bone))
            {
                // Positive twist folds the limb: forearm forward, shin backward.
                Vector3 hinge = lower ? Vector3.Cross(d, forward) : Vector3.Cross(forward, d);
                if (hinge.sqrMagnitude < 1e-4f) hinge = Vector3.right;
                AddJoint(bone, hinge.normalized, d, t.position);
            }
            else
            {
                Vector3 swing = Vector3.Cross(d, forward);
                if (swing.sqrMagnitude < 1e-4f) swing = Vector3.Cross(d, Vector3.up);
                AddJoint(bone, d, swing.normalized, t.position);
            }
        }

        private void AddJoint(RagdollBone bone, Vector3 axisWorld, Vector3 swingWorld, Vector3 anchorWorld)
        {
            Rigidbody body = _bodies[(int)bone];
            Rigidbody parent = _bodies[(int)RagdollProfile.Parent(bone)];
            if (body == null || parent == null) return;
            Transform t = body.transform;
            var joint = t.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent;
            joint.autoConfigureConnectedAnchor = true;
            joint.anchor = t.InverseTransformPoint(anchorWorld);
            joint.axis = t.InverseTransformDirection(axisWorld.normalized);
            joint.swingAxis = t.InverseTransformDirection(swingWorld.normalized);
            JointLimits l = RagdollProfile.Limits(bone);
            joint.lowTwistLimit = new SoftJointLimit { limit = l.TwistLow, bounciness = 0f };
            joint.highTwistLimit = new SoftJointLimit { limit = l.TwistHigh, bounciness = 0f };
            joint.swing1Limit = new SoftJointLimit { limit = l.Swing1, bounciness = 0f };
            joint.swing2Limit = new SoftJointLimit { limit = l.Swing2, bounciness = 0f };
            joint.enableProjection = true;
            joint.projectionDistance = 0.02f;
            joint.projectionAngle = 10f;
            joint.enablePreprocessing = false;
            _joints.Add(joint);
        }

        private void Ignore(RagdollBone a, RagdollBone b)
        {
            Collider ca = _shapes[(int)a], cb = _shapes[(int)b];
            if (ca != null && cb != null) Physics.IgnoreCollision(ca, cb, true);
        }

        private static float MaxAbs(Vector3 v) => Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(v.y), Mathf.Abs(v.z)));
    }
}
