using UnityEngine;

namespace Polykov.Animation
{
    /// <summary>
    /// Analytic two-bone IK (shoulder-elbow-wrist, hip-knee-ankle) on world-space positions and rotations.
    /// Pure math, no Transforms, so it is unit-tested outside Unity. Based on the law of cosines
    /// ("Simple Two Joint IK", D. Holden) plus a pole/hint twist so the elbow points where we want.
    /// </summary>
    public static class TwoBoneIKSolver
    {
        private const float Epsilon = 1e-4f;

        /// <param name="a">Root joint position (upper arm).</param>
        /// <param name="aRotation">Root joint world rotation.</param>
        /// <param name="b">Mid joint position (elbow).</param>
        /// <param name="bRotation">Mid joint world rotation.</param>
        /// <param name="c">End joint position (wrist).</param>
        /// <param name="target">Desired end position.</param>
        /// <param name="hint">A point the mid joint should bend toward.</param>
        /// <param name="newARotation">Solved world rotation of the root joint.</param>
        /// <param name="newBRotation">Solved world rotation of the mid joint.</param>
        public static void Solve(Vector3 a, Quaternion aRotation, Vector3 b, Quaternion bRotation, Vector3 c,
            Vector3 target, Vector3 hint, out Quaternion newARotation, out Quaternion newBRotation)
        {
            float lab = Vector3.Distance(a, b);
            float lcb = Vector3.Distance(c, b);
            float lat = Mathf.Clamp(Vector3.Distance(target, a), Epsilon, lab + lcb - Epsilon);

            Vector3 ac = c - a;
            Vector3 ab = b - a;
            Vector3 at = target - a;

            // Current and desired interior angles.
            float acAb0 = AngleBetween(ac, ab);
            float baBc0 = AngleBetween(a - b, c - b);
            float acAb1 = SafeAcos((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat));
            float baBc1 = SafeAcos((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb));

            // Bend plane: the current one, or the hint's when the limb is straight.
            Vector3 axis0 = Vector3.Cross(ac, ab);
            if (axis0.sqrMagnitude < 1e-8f) axis0 = Vector3.Cross(ac, hint - a);
            if (axis0.sqrMagnitude < 1e-8f) axis0 = Vector3.Cross(ac, Vector3.up);
            axis0.Normalize();

            Quaternion bendA = Quaternion.AngleAxis((acAb1 - acAb0) * Mathf.Rad2Deg, axis0);
            Quaternion bendB = Quaternion.AngleAxis((baBc1 - baBc0) * Mathf.Rad2Deg, axis0);

            // Apply the bend: rotate the root about a, the mid joint about b.
            Quaternion rootRotation = bendA * aRotation;
            Quaternion midRotation = bendA * bendB * bRotation;
            Vector3 newB = a + bendA * ab;
            Vector3 newC = newB + bendA * bendB * (c - b);

            // Swing the whole chain so the end lands on the target direction.
            Quaternion swing = Quaternion.FromToRotation(newC - a, at);
            rootRotation = swing * rootRotation;
            midRotation = swing * midRotation;
            newB = a + swing * (newB - a);

            // Twist around the a->target axis so the elbow faces the hint.
            Vector3 axis = at.normalized;
            Vector3 elbowDir = Vector3.ProjectOnPlane(newB - a, axis);
            Vector3 hintDir = Vector3.ProjectOnPlane(hint - a, axis);
            if (elbowDir.sqrMagnitude > 1e-8f && hintDir.sqrMagnitude > 1e-8f)
            {
                Quaternion twist = Quaternion.AngleAxis(Vector3.SignedAngle(elbowDir, hintDir, axis), axis);
                rootRotation = twist * rootRotation;
                midRotation = twist * midRotation;
            }

            newARotation = rootRotation;
            newBRotation = midRotation;
        }

        /// <summary>World end-joint position after applying solved rotations (forward kinematics, for tests/debug).</summary>
        public static Vector3 EndPosition(Vector3 a, Quaternion aRotation, Quaternion newARotation, Vector3 b,
            Quaternion bRotation, Quaternion newBRotation, Vector3 c)
        {
            Vector3 newB = a + newARotation * (Quaternion.Inverse(aRotation) * (b - a));
            return newB + newBRotation * (Quaternion.Inverse(bRotation) * (c - b));
        }

        /// <summary>World mid-joint position after applying the solved root rotation.</summary>
        public static Vector3 MidPosition(Vector3 a, Quaternion aRotation, Quaternion newARotation, Vector3 b)
            => a + newARotation * (Quaternion.Inverse(aRotation) * (b - a));

        private static float AngleBetween(Vector3 u, Vector3 v)
            => SafeAcos(Vector3.Dot(u.normalized, v.normalized));

        private static float SafeAcos(float x) => Mathf.Acos(Mathf.Clamp(x, -1f, 1f));
    }
}
