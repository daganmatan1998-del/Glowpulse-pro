using System.Collections.Generic;
using Glowpulse.Core;
using Glowpulse.Core.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Glowpulse.World.City
{
    /// <summary>
    /// Collects the city's static geometry and, at the end, welds it into one
    /// mesh per material.
    ///
    /// A city built the obvious way is thousands of GameObjects and thousands of
    /// draw calls. Every piece here is submitted as mesh + material + transform
    /// instead of as an object, so the whole city collapses into a handful of
    /// renderers. Colliders are kept separate and lightweight, because physics
    /// wants simple boxes, not the combined visual mesh.
    /// </summary>
    public sealed class CityGeometry
    {
        private readonly Dictionary<Material, List<CombineInstance>> _batches =
            new Dictionary<Material, List<CombineInstance>>(32);

        private readonly Transform _root;
        private readonly bool _batched;
        private readonly Transform _colliderRoot;
        private readonly Transform _loose;

        /// <summary>Vertices allowed in one combined mesh before it is split.</summary>
        private const int MaxVertsPerBatch = 500000;

        public Transform Root => _root;
        public int PieceCount { get; private set; }
        public int ColliderCount { get; private set; }
        public int BatchCount { get; private set; }

        public CityGeometry(Transform root, bool batched)
        {
            _root = root;
            _batched = batched;

            _colliderRoot = new GameObject("Colliders").transform;
            _colliderRoot.SetParent(root, false);

            _loose = new GameObject("Detail").transform;
            _loose.SetParent(root, false);
        }

        /// <summary>Adds a piece of static visual geometry.</summary>
        public void Add(Mesh mesh, Material material, Vector3 position, Quaternion rotation,
            Vector3 scale)
        {
            if (mesh == null || material == null) return;
            PieceCount++;

            if (!_batched)
            {
                MeshLibrary.CreatePart("Piece", _loose, mesh, material, position, rotation, scale);
                return;
            }

            if (!_batches.TryGetValue(material, out List<CombineInstance> list))
            {
                list = new List<CombineInstance>(64);
                _batches.Add(material, list);
            }

            list.Add(new CombineInstance
            {
                mesh = mesh,
                transform = Matrix4x4.TRS(position, rotation, scale)
            });
        }

        public void AddBox(Material material, Vector3 center, Vector3 size, float yawDegrees = 0f)
        {
            Add(MeshLibrary.Cube, material, center, Quaternion.Euler(0f, yawDegrees, 0f), size);
        }

        public void AddCylinder(Material material, Vector3 center, float radius, float halfHeight,
            float yawDegrees = 0f)
        {
            Add(MeshLibrary.Cylinder, material, center, Quaternion.Euler(0f, yawDegrees, 0f),
                new Vector3(radius * 2f, halfHeight, radius * 2f));
        }

        public void AddSphere(Material material, Vector3 center, Vector3 size)
        {
            Add(MeshLibrary.Sphere, material, center, Quaternion.identity, size);
        }

        /// <summary>Adds a box collider with no renderer.</summary>
        public void AddCollider(Vector3 center, Vector3 size, float yawDegrees = 0f, int layer = -1)
        {
            var go = new GameObject("Collider");
            go.transform.SetParent(_colliderRoot, false);
            go.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, yawDegrees, 0f));
            go.layer = layer >= 0 ? layer : GameLayers.Environment;

            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            ColliderCount++;
        }

        /// <summary>
        /// Anything that cannot be welded - lights, particles, anything that
        /// moves - goes in as its own object.
        /// </summary>
        public GameObject AddLoose(string name, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_loose, false);
            go.transform.SetPositionAndRotation(position, rotation);
            return go;
        }

        /// <summary>Welds every batch into combined meshes and creates their renderers.</summary>
        public void Build()
        {
            if (!_batched) return;

            foreach (KeyValuePair<Material, List<CombineInstance>> pair in _batches)
            {
                List<CombineInstance> all = pair.Value;
                int index = 0;

                while (index < all.Count)
                {
                    int verts = 0;
                    int start = index;

                    // Fill a batch up to the vertex ceiling, always taking at
                    // least one piece so an oversized mesh cannot stall the loop.
                    while (index < all.Count)
                    {
                        int meshVerts = all[index].mesh != null ? all[index].mesh.vertexCount : 0;
                        if (index > start && verts + meshVerts > MaxVertsPerBatch) break;
                        verts += meshVerts;
                        index++;
                    }

                    CreateBatch(pair.Key, all.GetRange(start, index - start), verts);
                }
            }

            _batches.Clear();
        }

        private void CreateBatch(Material material, List<CombineInstance> pieces, int vertexCount)
        {
            if (pieces.Count == 0) return;

            var mesh = new Mesh
            {
                name = $"City_{material.name}_{BatchCount}",
                // The default 16-bit index buffer tops out at 65k vertices, which
                // a city block passes almost immediately.
                indexFormat = vertexCount > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            mesh.CombineMeshes(pieces.ToArray(), true, true);
            mesh.RecalculateBounds();

            var go = new GameObject(mesh.name);
            go.transform.SetParent(_root, false);
            go.layer = GameLayers.Environment;
            go.isStatic = true;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;

            BatchCount++;
        }
    }
}
