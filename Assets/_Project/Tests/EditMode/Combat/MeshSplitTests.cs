using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Polykov.Combat.Tests
{
    public class MeshSplitTests
    {
        /// <summary>A tube along +X: <paramref name="rings"/> rings of <paramref name="sides"/> vertices, quads split in two triangles.</summary>
        private static void Tube(int rings, int sides, bool duplicateSeam, out Vector3[] positions, out int[] triangles, out int[] ringOf)
        {
            var pos = new List<Vector3>();
            var ringIndex = new List<int>();
            int columns = duplicateSeam ? sides + 1 : sides;
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < columns; s++)
                {
                    float a = (s % sides) / (float)sides * Mathf.PI * 2f;
                    pos.Add(new Vector3(r, Mathf.Cos(a) * 0.3f, Mathf.Sin(a) * 0.3f));
                    ringIndex.Add(r);
                }
            var tris = new List<int>();
            for (int r = 0; r < rings - 1; r++)
                for (int s = 0; s < sides; s++)
                {
                    int s2 = duplicateSeam ? s + 1 : (s + 1) % sides;
                    int a = r * columns + s, b = r * columns + s2, c = (r + 1) * columns + s, d = (r + 1) * columns + s2;
                    tris.AddRange(new[] { a, b, c, b, d, c });
                }
            positions = pos.ToArray();
            triangles = tris.ToArray();
            ringOf = ringIndex.ToArray();
        }

        private static SkinWeight[] WeightsByRing(int[] ringOf, int cutRing)
        {
            var w = new SkinWeight[ringOf.Length];
            for (int i = 0; i < w.Length; i++) w[i] = new SkinWeight(ringOf[i] >= cutRing ? 1 : 0, 1f);
            return w;
        }

        private static readonly bool[] Region1 = { false, true };

        [Test]
        public void EveryTriangleEndsUpOnExactlyOneSide()
        {
            Tube(6, 8, false, out Vector3[] p, out int[] t, out int[] ring);
            MeshSplitResult r = MeshSplitter.Split(p, WeightsByRing(ring, 3), new List<int[]> { t }, Region1);
            Assert.AreEqual(t.Length / 3, r.LimbTriangleCount + r.RestTriangleCount);
            Assert.Greater(r.LimbTriangleCount, 0);
            Assert.Greater(r.RestTriangleCount, 0);
        }

        [Test]
        public void SplitFollowsTheWeightBoundary()
        {
            Tube(6, 8, false, out Vector3[] p, out int[] t, out int[] ring);
            MeshSplitResult r = MeshSplitter.Split(p, WeightsByRing(ring, 3), new List<int[]> { t }, Region1);
            // Quads between rings 3-4 and 4-5 are fully inside: 2 bands of 8 quads = 32 triangles; the band 2-3 is mixed.
            foreach (int v in r.Limb[0]) Assert.GreaterOrEqual(ring[v], 2);
            foreach (int v in r.Rest[0]) Assert.LessOrEqual(ring[v], 3);
        }

        [Test]
        public void CutEdges_FormOneClosedRing()
        {
            Tube(6, 8, false, out Vector3[] p, out int[] t, out int[] ring);
            MeshSplitResult r = MeshSplitter.Split(p, WeightsByRing(ring, 3), new List<int[]> { t }, Region1);
            // The boundary runs along the diagonals of the mixed band: two edges per quad, still one closed ring.
            Assert.AreEqual(16, r.CutEdges.Count);

            var edges = new List<(Vector3, Vector3)>();
            foreach ((int a, int b) in r.CutEdges) edges.Add((p[a], p[b]));
            List<List<Vector3>> loops = StumpCap.Loops(edges);
            Assert.AreEqual(1, loops.Count);
            Assert.AreEqual(16, loops[0].Count);
        }

        [Test]
        public void DuplicatedSeamVertices_DoNotBreakTheCut()
        {
            Tube(6, 8, true, out Vector3[] p, out int[] t, out int[] ring);
            MeshSplitResult r = MeshSplitter.Split(p, WeightsByRing(ring, 3), new List<int[]> { t }, Region1);
            Assert.AreEqual(16, r.CutEdges.Count, "welding by position must close the loop across the UV seam");
        }

        [Test]
        public void NoRegionWeight_NothingIsSevered()
        {
            Tube(4, 6, false, out Vector3[] p, out int[] t, out int[] ring);
            MeshSplitResult r = MeshSplitter.Split(p, WeightsByRing(ring, 99), new List<int[]> { t }, Region1);
            Assert.AreEqual(0, r.LimbTriangleCount);
            Assert.AreEqual(0, r.CutEdges.Count);
        }

        [Test]
        public void SharedBones_AreWeightedByRegionShare()
        {
            var w = new SkinWeight(0, 0.3f, 1, 0.7f);
            Assert.AreEqual(0.7f, w.RegionWeight(Region1), 1e-5f);
            Assert.AreEqual(0.3f, w.RegionWeight(new[] { true, false }), 1e-5f);
            Assert.AreEqual(0f, new SkinWeight(0, 0f).RegionWeight(Region1));
        }

        [Test]
        public void SecondSubmesh_IsSplitTheSameWay()
        {
            Tube(6, 8, false, out Vector3[] p, out int[] t, out int[] ring);
            MeshSplitResult r = MeshSplitter.Split(p, WeightsByRing(ring, 3), new List<int[]> { t, t }, Region1);
            Assert.AreEqual(r.Limb[0].Count, r.Limb[1].Count);
            Assert.AreEqual(r.Rest[0].Count, r.Rest[1].Count);
        }

        [Test]
        public void Cap_FansAroundTheCentroid_FacingOutward()
        {
            var loop = new List<Vector3>();
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                loop.Add(new Vector3(0f, Mathf.Cos(a) * 0.3f, Mathf.Sin(a) * 0.3f));
            }
            StumpCap.Cap cap = StumpCap.Build(loop, Vector3.right);
            Assert.AreEqual(13, cap.Vertices.Length);
            Assert.AreEqual(36, cap.Triangles.Length);
            Assert.AreEqual(0f, cap.Vertices[cap.CentroidIndex].magnitude, 1e-5f);
            for (int i = 0; i < cap.Triangles.Length; i += 3)
            {
                Vector3 n = Vector3.Cross(cap.Vertices[cap.Triangles[i + 1]] - cap.Vertices[cap.Triangles[i]],
                    cap.Vertices[cap.Triangles[i + 2]] - cap.Vertices[cap.Triangles[i]]);
                Assert.Greater(Vector3.Dot(n, Vector3.right), 0f);
            }
            StumpCap.Cap flipped = StumpCap.Build(loop, Vector3.left);
            Vector3 fn = Vector3.Cross(flipped.Vertices[flipped.Triangles[1]] - flipped.Vertices[flipped.Triangles[0]],
                flipped.Vertices[flipped.Triangles[2]] - flipped.Vertices[flipped.Triangles[0]]);
            Assert.Less(Vector3.Dot(fn, Vector3.right), 0f);
        }
    }
}
