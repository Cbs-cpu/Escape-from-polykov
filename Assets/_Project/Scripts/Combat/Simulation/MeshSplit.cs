using System.Collections.Generic;
using UnityEngine;

namespace Polykov.Combat
{
    /// <summary>Up to four bone influences of a skinned vertex (same meaning as UnityEngine.BoneWeight).</summary>
    public struct SkinWeight
    {
        public int Bone0, Bone1, Bone2, Bone3;
        public float Weight0, Weight1, Weight2, Weight3;

        public SkinWeight(int b0, float w0, int b1 = 0, float w1 = 0f, int b2 = 0, float w2 = 0f, int b3 = 0, float w3 = 0f)
        {
            Bone0 = b0; Weight0 = w0;
            Bone1 = b1; Weight1 = w1;
            Bone2 = b2; Weight2 = w2;
            Bone3 = b3; Weight3 = w3;
        }

        /// <summary>Share of this vertex driven by the bones flagged in <paramref name="region"/> (0..1).</summary>
        public float RegionWeight(bool[] region)
        {
            float w = 0f;
            if (Weight0 > 0f && Bone0 < region.Length && region[Bone0]) w += Weight0;
            if (Weight1 > 0f && Bone1 < region.Length && region[Bone1]) w += Weight1;
            if (Weight2 > 0f && Bone2 < region.Length && region[Bone2]) w += Weight2;
            if (Weight3 > 0f && Bone3 < region.Length && region[Bone3]) w += Weight3;
            float total = Weight0 + Weight1 + Weight2 + Weight3;
            return total > 1e-6f ? w / total : 0f;
        }
    }

    public sealed class MeshSplitResult
    {
        /// <summary>Per submesh: triangles (vertex index triples) that belong to the severed part.</summary>
        public List<int>[] Limb;
        /// <summary>Per submesh: triangles that stay on the body.</summary>
        public List<int>[] Rest;
        /// <summary>Undirected cut edges (vertex indices of submesh 0) between limb and rest triangles.</summary>
        public List<(int a, int b)> CutEdges = new List<(int, int)>();
        public int LimbTriangleCount;
        public int RestTriangleCount;
    }

    /// <summary>
    /// Splits a skinned mesh into "the region driven by some bones" and "the rest", triangle by triangle, and finds
    /// the open loop where they were joined. Pure data in, pure data out (no UnityEngine.Mesh), so it is testable and
    /// can run anywhere. Vertices at the same position (UV seams, hard edges) are welded when finding the cut, so the
    /// loop is closed even if the artist duplicated vertices.
    /// </summary>
    public static class MeshSplitter
    {
        /// <summary>Position weld tolerance in meters (0.1 mm).</summary>
        public const float WeldGrid = 1e-4f;

        /// <param name="submeshTriangles">Index buffers of every submesh. Submesh 0 defines the cut loop.</param>
        /// <param name="regionBones">Flags per bone index: true = part of the severed region.</param>
        /// <param name="threshold">A triangle is severed when its vertices average at least this region weight.</param>
        public static MeshSplitResult Split(Vector3[] positions, SkinWeight[] weights, IReadOnlyList<int[]> submeshTriangles,
            bool[] regionBones, float threshold = 0.5f)
        {
            int submeshes = submeshTriangles.Count;
            var result = new MeshSplitResult
            {
                Limb = new List<int>[submeshes],
                Rest = new List<int>[submeshes],
            };

            var regionWeight = new float[positions.Length];
            for (int v = 0; v < positions.Length; v++) regionWeight[v] = weights[v].RegionWeight(regionBones);

            for (int s = 0; s < submeshes; s++)
            {
                result.Limb[s] = new List<int>();
                result.Rest[s] = new List<int>();
                int[] tris = submeshTriangles[s];
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    float mean = (regionWeight[tris[i]] + regionWeight[tris[i + 1]] + regionWeight[tris[i + 2]]) / 3f;
                    List<int> target = mean >= threshold ? result.Limb[s] : result.Rest[s];
                    target.Add(tris[i]);
                    target.Add(tris[i + 1]);
                    target.Add(tris[i + 2]);
                }
                result.LimbTriangleCount += result.Limb[s].Count / 3;
                result.RestTriangleCount += result.Rest[s].Count / 3;
            }

            FindCut(positions, result);
            return result;
        }

        private static void FindCut(Vector3[] positions, MeshSplitResult result)
        {
            if (result.Limb.Length == 0) return;
            var canonical = Weld(positions);

            // An edge on the cut is used by limb triangles and by rest triangles (after welding).
            var limbEdges = new HashSet<long>();
            AddEdges(result.Limb[0], canonical, limbEdges);
            var restEdges = new HashSet<long>();
            AddEdges(result.Rest[0], canonical, restEdges);

            var seen = new HashSet<long>();
            List<int> rest = result.Rest[0];
            for (int i = 0; i + 2 < rest.Count; i += 3)
            {
                for (int e = 0; e < 3; e++)
                {
                    int a = rest[i + e];
                    int b = rest[i + (e + 1) % 3];
                    long key = EdgeKey(canonical[a], canonical[b]);
                    if (limbEdges.Contains(key) && seen.Add(key)) result.CutEdges.Add((a, b));
                }
            }
        }

        private static void AddEdges(List<int> tris, int[] canonical, HashSet<long> set)
        {
            for (int i = 0; i + 2 < tris.Count; i += 3)
                for (int e = 0; e < 3; e++)
                    set.Add(EdgeKey(canonical[tris[i + e]], canonical[tris[i + (e + 1) % 3]]));
        }

        private static long EdgeKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        /// <summary>Maps every vertex to the first vertex at (almost) the same position.</summary>
        public static int[] Weld(Vector3[] positions)
        {
            var canonical = new int[positions.Length];
            var first = new Dictionary<(int, int, int), int>(positions.Length);
            for (int v = 0; v < positions.Length; v++)
            {
                Vector3 p = positions[v];
                var key = (Mathf.RoundToInt(p.x / WeldGrid), Mathf.RoundToInt(p.y / WeldGrid), Mathf.RoundToInt(p.z / WeldGrid));
                if (!first.TryGetValue(key, out int index)) first[key] = index = v;
                canonical[v] = index;
            }
            return canonical;
        }
    }

    /// <summary>Closes the open ends of a cut with a triangle fan around the loop's centroid.</summary>
    public static class StumpCap
    {
        public sealed class Cap
        {
            /// <summary>Loop vertices first, then the centroid as the last vertex.</summary>
            public Vector3[] Vertices;
            public int[] Triangles;
            public int CentroidIndex => Vertices.Length - 1;
        }

        /// <summary>
        /// Chains cut edges (given as vertex positions) into closed loops. Edges that do not close are returned as
        /// open chains too; caps over them are still fine visually.
        /// </summary>
        public static List<List<Vector3>> Loops(IReadOnlyList<(Vector3 a, Vector3 b)> edges)
        {
            var loops = new List<List<Vector3>>();
            var byPoint = new Dictionary<(int, int, int), List<int>>();
            for (int i = 0; i < edges.Count; i++)
            {
                Add(byPoint, Key(edges[i].a), i);
                Add(byPoint, Key(edges[i].b), i);
            }

            var used = new bool[edges.Count];
            for (int start = 0; start < edges.Count; start++)
            {
                if (used[start]) continue;
                var loop = new List<Vector3>();
                int current = start;
                Vector3 point = edges[start].a;
                loop.Add(point);
                while (current >= 0 && !used[current])
                {
                    used[current] = true;
                    Vector3 next = Key(edges[current].a) == Key(point) ? edges[current].b : edges[current].a;
                    point = next;
                    int following = -1;
                    foreach (int candidate in byPoint[Key(next)])
                        if (!used[candidate]) { following = candidate; break; }
                    if (following >= 0) loop.Add(next);
                    current = following;
                }
                if (loop.Count >= 3) loops.Add(loop);
            }
            return loops;
        }

        /// <param name="outward">Direction the cap faces (away from the material that was removed).</param>
        public static Cap Build(IReadOnlyList<Vector3> loop, Vector3 outward)
        {
            int n = loop.Count;
            var vertices = new Vector3[n + 1];
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                vertices[i] = loop[i];
                centroid += loop[i];
            }
            centroid /= Mathf.Max(n, 1);
            vertices[n] = centroid;

            var triangles = new int[n * 3];
            for (int i = 0; i < n; i++)
            {
                int a = i, b = (i + 1) % n;
                triangles[i * 3] = n;
                triangles[i * 3 + 1] = a;
                triangles[i * 3 + 2] = b;
                Vector3 normal = Vector3.Cross(vertices[a] - centroid, vertices[b] - centroid);
                if (Vector3.Dot(normal, outward) < 0f)
                {
                    triangles[i * 3 + 1] = b;
                    triangles[i * 3 + 2] = a;
                }
            }
            return new Cap { Vertices = vertices, Triangles = triangles };
        }

        private static (int, int, int) Key(Vector3 p)
            => (Mathf.RoundToInt(p.x / MeshSplitter.WeldGrid), Mathf.RoundToInt(p.y / MeshSplitter.WeldGrid), Mathf.RoundToInt(p.z / MeshSplitter.WeldGrid));

        private static void Add(Dictionary<(int, int, int), List<int>> map, (int, int, int) key, int edge)
        {
            if (!map.TryGetValue(key, out List<int> list)) map[key] = list = new List<int>(2);
            list.Add(edge);
        }
    }
}
