using UnityEngine;

namespace Glowpulse.Core.Rendering
{
    /// <summary>
    /// Shared primitive meshes. Unity's <see cref="GameObject.CreatePrimitive"/>
    /// also attaches a collider and a renderer, which is exactly what we do not
    /// want for the hundreds of visual-only parts the world and characters are
    /// built from - so the meshes are harvested once and reused.
    /// </summary>
    public static class MeshLibrary
    {
        private static Mesh _cube, _sphere, _capsule, _cylinder, _quad, _plane;

        public static Mesh Cube => _cube != null ? _cube : _cube = Harvest(PrimitiveType.Cube);
        public static Mesh Sphere => _sphere != null ? _sphere : _sphere = Harvest(PrimitiveType.Sphere);
        public static Mesh Capsule => _capsule != null ? _capsule : _capsule = Harvest(PrimitiveType.Capsule);
        public static Mesh Cylinder => _cylinder != null ? _cylinder : _cylinder = Harvest(PrimitiveType.Cylinder);
        public static Mesh Quad => _quad != null ? _quad : _quad = Harvest(PrimitiveType.Quad);
        public static Mesh Plane => _plane != null ? _plane : _plane = Harvest(PrimitiveType.Plane);

        public static Mesh Get(PrimitiveType type)
        {
            switch (type)
            {
                case PrimitiveType.Sphere: return Sphere;
                case PrimitiveType.Capsule: return Capsule;
                case PrimitiveType.Cylinder: return Cylinder;
                case PrimitiveType.Quad: return Quad;
                case PrimitiveType.Plane: return Plane;
                default: return Cube;
            }
        }

        /// <summary>
        /// Creates a visual-only child: mesh filter + renderer, no collider.
        /// This is the workhorse used by every placeholder builder in the project.
        /// </summary>
        public static GameObject CreatePart(string name, Transform parent, Mesh mesh, Material material,
            Vector3 localPosition, Quaternion localRotation, Vector3 localScale,
            bool castShadows = true, bool receiveShadows = true)
        {
            var go = new GameObject(name);
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = localRotation;
            t.localScale = localScale;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = receiveShadows;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;

            return go;
        }

        public static GameObject CreateBox(string name, Transform parent, Material material,
            Vector3 localPosition, Vector3 size, Quaternion? rotation = null, bool castShadows = true)
        {
            return CreatePart(name, parent, Cube, material, localPosition,
                rotation ?? Quaternion.identity, size, castShadows);
        }

        private static Mesh Harvest(PrimitiveType type)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            temp.SetActive(false);
            Destroy(temp);
            return mesh;
        }

        private static void Destroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _cube = _sphere = _capsule = _cylinder = _quad = _plane = null;
        }
    }
}
