using UnityEngine;

namespace Polykov.Weapons
{
    /// <summary>Shared built-in meshes for procedural placeholders.</summary>
    public static class PrimitiveMeshes
    {
        private static Mesh _cube;

        public static Mesh Cube
        {
            get
            {
                if (_cube != null) return _cube;
                _cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                if (_cube == null)
                {
                    // Fallback if the built-in resource name changes between Unity versions.
                    var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    _cube = temp.GetComponent<MeshFilter>().sharedMesh;
                    Object.Destroy(temp);
                }
                return _cube;
            }
        }
    }
}
