using System;

namespace UnityEngine
{
    [Serializable]
    public struct Quaternion : IEquatable<Quaternion>
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public Quaternion(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public static Quaternion identity => new Quaternion(0f, 0f, 0f, 1f);

        /// <summary>Unity order: rotate around Z, then X, then Y (degrees).</summary>
        public static Quaternion Euler(float x, float y, float z)
            => AngleAxis(y, Vector3.up) * AngleAxis(x, Vector3.right) * AngleAxis(z, Vector3.forward);

        public static Quaternion Euler(Vector3 euler) => Euler(euler.x, euler.y, euler.z);

        public static Quaternion AngleAxis(float angle, Vector3 axis)
        {
            Vector3 n = axis.normalized;
            float half = angle * Mathf.Deg2Rad * 0.5f;
            float s = (float)Math.Sin(half);
            return new Quaternion(n.x * s, n.y * s, n.z * s, (float)Math.Cos(half));
        }

        public static Quaternion Inverse(Quaternion q)
        {
            float n = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            if (n < 1e-12f) return identity;
            return new Quaternion(-q.x / n, -q.y / n, -q.z / n, q.w / n);
        }

        public static float Dot(Quaternion a, Quaternion b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;

        public static float Angle(Quaternion a, Quaternion b)
        {
            float dot = Math.Min(Math.Abs(Dot(a, b)), 1f);
            return dot > 0.999999f ? 0f : (float)Math.Acos(dot) * 2f * Mathf.Rad2Deg;
        }

        public static Quaternion Normalize(Quaternion q)
        {
            float mag = (float)Math.Sqrt(Dot(q, q));
            return mag < Mathf.Epsilon ? identity : new Quaternion(q.x / mag, q.y / mag, q.z / mag, q.w / mag);
        }

        public Quaternion normalized => Normalize(this);

        public static Quaternion FromToRotation(Vector3 from, Vector3 to)
        {
            Vector3 f = from.normalized;
            Vector3 t = to.normalized;
            float dot = Vector3.Dot(f, t);
            if (dot > 0.999999f) return identity;
            if (dot < -0.999999f)
            {
                Vector3 axis = Vector3.Cross(Vector3.right, f);
                if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(Vector3.up, f);
                return AngleAxis(180f, axis);
            }
            Vector3 c = Vector3.Cross(f, t);
            return Normalize(new Quaternion(c.x, c.y, c.z, 1f + dot));
        }

        public static Quaternion LookRotation(Vector3 forward, Vector3 upwards)
        {
            Vector3 f = forward.normalized;
            if (f.sqrMagnitude < 1e-12f) return identity;
            Vector3 r = Vector3.Cross(upwards, f).normalized;
            if (r.sqrMagnitude < 1e-12f) return FromToRotation(Vector3.forward, f);
            Vector3 u = Vector3.Cross(f, r);
            // Rotation matrix columns r, u, f -> quaternion.
            float m00 = r.x, m01 = u.x, m02 = f.x;
            float m10 = r.y, m11 = u.y, m12 = f.y;
            float m20 = r.z, m21 = u.z, m22 = f.z;
            float trace = m00 + m11 + m22;
            Quaternion q;
            if (trace > 0f)
            {
                float s = (float)Math.Sqrt(trace + 1f) * 2f;
                q = new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s);
            }
            else if (m00 > m11 && m00 > m22)
            {
                float s = (float)Math.Sqrt(1f + m00 - m11 - m22) * 2f;
                q = new Quaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s);
            }
            else if (m11 > m22)
            {
                float s = (float)Math.Sqrt(1f + m11 - m00 - m22) * 2f;
                q = new Quaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s);
            }
            else
            {
                float s = (float)Math.Sqrt(1f + m22 - m00 - m11) * 2f;
                q = new Quaternion((m02 + m20) / s, (m12 + m21) / s, 0.25f * s, (m10 - m01) / s);
            }
            return Normalize(q);
        }

        public static Quaternion LookRotation(Vector3 forward) => LookRotation(forward, Vector3.up);

        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => SlerpUnclamped(a, b, Mathf.Clamp01(t));

        public static Quaternion SlerpUnclamped(Quaternion a, Quaternion b, float t)
        {
            float dot = Dot(a, b);
            if (dot < 0f)
            {
                b = new Quaternion(-b.x, -b.y, -b.z, -b.w);
                dot = -dot;
            }
            if (dot > 0.9995f)
                return Normalize(new Quaternion(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t));
            float theta = (float)Math.Acos(dot);
            float sin = (float)Math.Sin(theta);
            float wa = (float)Math.Sin((1f - t) * theta) / sin;
            float wb = (float)Math.Sin(t * theta) / sin;
            return new Quaternion(a.x * wa + b.x * wb, a.y * wa + b.y * wb, a.z * wa + b.z * wb, a.w * wa + b.w * wb);
        }

        public static Quaternion operator *(Quaternion l, Quaternion r) => new Quaternion(
            l.w * r.x + l.x * r.w + l.y * r.z - l.z * r.y,
            l.w * r.y + l.y * r.w + l.z * r.x - l.x * r.z,
            l.w * r.z + l.z * r.w + l.x * r.y - l.y * r.x,
            l.w * r.w - l.x * r.x - l.y * r.y - l.z * r.z);

        public static Vector3 operator *(Quaternion rotation, Vector3 point)
        {
            float x = rotation.x * 2f, y = rotation.y * 2f, z = rotation.z * 2f;
            float xx = rotation.x * x, yy = rotation.y * y, zz = rotation.z * z;
            float xy = rotation.x * y, xz = rotation.x * z, yz = rotation.y * z;
            float wx = rotation.w * x, wy = rotation.w * y, wz = rotation.w * z;
            return new Vector3(
                (1f - (yy + zz)) * point.x + (xy - wz) * point.y + (xz + wy) * point.z,
                (xy + wz) * point.x + (1f - (xx + zz)) * point.y + (yz - wx) * point.z,
                (xz - wy) * point.x + (yz + wx) * point.y + (1f - (xx + yy)) * point.z);
        }

        public static bool operator ==(Quaternion a, Quaternion b) => Dot(a, b) > 0.999999f;
        public static bool operator !=(Quaternion a, Quaternion b) => !(a == b);

        public bool Equals(Quaternion other) => x == other.x && y == other.y && z == other.z && w == other.w;
        public override bool Equals(object other) => other is Quaternion q && Equals(q);
        public override int GetHashCode() => HashCode.Combine(x, y, z, w);
        public override string ToString() => $"({x:F3}, {y:F3}, {z:F3}, {w:F3})";
    }
}
