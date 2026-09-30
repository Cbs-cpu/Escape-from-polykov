using NUnit.Framework;
using UnityEngine;

namespace Polykov.Animation.Tests
{
    public class TwoBoneIKSolverTests
    {
        // A right arm hanging down: shoulder, elbow, wrist, with arbitrary bone rotations.
        private static readonly Vector3 A = new Vector3(0.2f, 1.4f, 0f);
        private static readonly Vector3 B = new Vector3(0.22f, 1.12f, -0.02f);
        private static readonly Vector3 C = new Vector3(0.23f, 0.86f, 0.01f);
        private static readonly Quaternion ARot = Quaternion.Euler(10f, 20f, 30f);
        private static readonly Quaternion BRot = Quaternion.Euler(-15f, 5f, 60f);

        private static void Solve(Vector3 target, Vector3 hint, out Vector3 end, out Vector3 mid)
        {
            TwoBoneIKSolver.Solve(A, ARot, B, BRot, C, target, hint, out Quaternion na, out Quaternion nb);
            end = TwoBoneIKSolver.EndPosition(A, ARot, na, B, BRot, nb, C);
            mid = TwoBoneIKSolver.MidPosition(A, ARot, na, B);
        }

        [TestCase(0.25f, 1.3f, 0.35f)]
        [TestCase(0.05f, 1.35f, 0.3f)]
        [TestCase(0.3f, 1.1f, 0.2f)]
        [TestCase(-0.05f, 1.5f, 0.25f)]
        [TestCase(0.35f, 1.25f, -0.1f)]
        public void ReachableTarget_IsReached_AndBoneLengthsKept(float x, float y, float z)
        {
            var target = new Vector3(x, y, z);
            Solve(target, A + new Vector3(0.3f, -0.4f, -0.1f), out Vector3 end, out Vector3 mid);
            Assert.AreEqual(0f, Vector3.Distance(end, target), 1e-3f, "end effector should reach the target");
            Assert.AreEqual(Vector3.Distance(A, B), Vector3.Distance(A, mid), 1e-3f, "upper bone length");
            Assert.AreEqual(Vector3.Distance(B, C), Vector3.Distance(mid, end), 1e-3f, "lower bone length");
        }

        [Test]
        public void UnreachableTarget_StretchesTowardIt()
        {
            var target = A + new Vector3(0f, 0f, 2f);
            Solve(target, A + Vector3.down, out Vector3 end, out _);
            float reach = Vector3.Distance(A, B) + Vector3.Distance(B, C);
            Assert.AreEqual(reach, Vector3.Distance(A, end), 2e-3f);
            Assert.Greater(Vector3.Dot((end - A).normalized, Vector3.forward), 0.999f);
        }

        [TestCase(1f)]
        [TestCase(-1f)]
        public void Elbow_BendsTowardHint(float side)
        {
            var target = new Vector3(0.2f, 1.3f, 0.35f);
            Vector3 hint = A + new Vector3(0.5f * side, -0.2f, 0f);
            Solve(target, hint, out _, out Vector3 mid);
            Vector3 axis = (target - A).normalized;
            Vector3 elbow = Vector3.ProjectOnPlane(mid - A, axis);
            Vector3 hintDir = Vector3.ProjectOnPlane(hint - A, axis);
            Assert.Greater(Vector3.Dot(elbow.normalized, hintDir.normalized), 0.99f);
        }

        [Test]
        public void TargetAtCurrentEnd_KeepsPose()
        {
            Solve(C, B + (B - (A + C) * 0.5f), out Vector3 end, out Vector3 mid);
            Assert.AreEqual(0f, Vector3.Distance(end, C), 1e-3f);
            Assert.AreEqual(0f, Vector3.Distance(mid, B), 1e-2f);
        }
    }
}
