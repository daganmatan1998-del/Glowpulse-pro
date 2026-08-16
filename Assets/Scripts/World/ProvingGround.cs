using Glowpulse.Core;
using Glowpulse.Core.Rendering;
using UnityEngine;

namespace Glowpulse.World
{
    /// <summary>
    /// A compact sandbox for exercising movement and the camera: flat ground,
    /// slopes of increasing steepness, stairs, low walls to squeeze the camera
    /// against, an overhang and a pit.
    ///
    /// This is a development space, not the game world - the city arrives in the
    /// open-world phase and replaces it.
    /// </summary>
    public static class ProvingGround
    {
        public static Transform Build(Transform parent, float size = 90f)
        {
            var root = new GameObject("Proving Ground");
            if (parent != null) root.transform.SetParent(parent, false);
            Transform t = root.transform;

            Material asphalt = MaterialLibrary.Lit(Hex("3A3D42"), 0.18f);
            Material concrete = MaterialLibrary.Lit(Hex("8C8880"), 0.12f);
            Material painted = MaterialLibrary.Lit(Hex("5C6B7A"), 0.3f);
            Material accent = MaterialLibrary.Emissive(Hex("46C2C8"), 1.6f);

            BuildGround(t, size, asphalt);
            BuildGridLines(t, size);
            BuildSlopes(t, concrete);
            BuildStairs(t, concrete);
            BuildCameraTestGeometry(t, painted, accent);
            BuildPillars(t, concrete);

            GameLayers.SetLayerRecursive(root, GameLayers.Environment);

            // The floor must be on Ground so the motor's slope logic and the
            // camera's blocker mask both agree about what counts as walkable.
            Transform ground = t.Find("Ground");
            if (ground != null) GameLayers.SetLayerRecursive(ground.gameObject, GameLayers.Ground);

            return t;
        }

        private static void BuildGround(Transform parent, float size, Material material)
        {
            var go = MeshLibrary.CreatePart("Ground", parent, MeshLibrary.Cube, material,
                new Vector3(0f, -0.5f, 0f), Quaternion.identity, new Vector3(size, 1f, size),
                castShadows: false);

            var box = go.AddComponent<BoxCollider>();
            box.size = Vector3.one;
        }

        /// <summary>Faint grid stripes give a sense of speed and scale while moving.</summary>
        private static void BuildGridLines(Transform parent, float size)
        {
            var group = new GameObject("Grid");
            group.transform.SetParent(parent, false);

            Material line = MaterialLibrary.Lit(Hex("4A4E55"), 0.05f);
            const float spacing = 6f;
            int count = Mathf.FloorToInt(size / spacing);
            float half = size * 0.5f;

            for (int i = -count / 2; i <= count / 2; i++)
            {
                float p = i * spacing;
                MeshLibrary.CreatePart("LineX", group.transform, MeshLibrary.Cube, line,
                    new Vector3(p, 0.005f, 0f), Quaternion.identity, new Vector3(0.08f, 0.01f, size),
                    castShadows: false, receiveShadows: false);
                MeshLibrary.CreatePart("LineZ", group.transform, MeshLibrary.Cube, line,
                    new Vector3(0f, 0.005f, p), Quaternion.identity, new Vector3(size, 0.01f, 0.08f),
                    castShadows: false, receiveShadows: false);
            }

            // Border kerb so the play space has a readable edge.
            Material kerb = MaterialLibrary.Lit(Hex("6E6A63"), 0.1f);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                Quaternion rot = Quaternion.Euler(0f, angle, 0f);
                var go = MeshLibrary.CreatePart("Kerb", group.transform, MeshLibrary.Cube, kerb,
                    rot * new Vector3(0f, 0.35f, half), rot, new Vector3(size, 0.7f, 0.6f));
                go.AddComponent<BoxCollider>();
            }
        }

        private static void BuildSlopes(Transform parent, Material material)
        {
            var group = new GameObject("Slopes");
            group.transform.SetParent(parent, false);

            // Walkable, marginal and too-steep, so the slope limit is easy to feel.
            float[] angles = { 18f, 32f, 46f, 60f };
            for (int i = 0; i < angles.Length; i++)
            {
                float angle = angles[i];
                var go = MeshLibrary.CreatePart($"Slope_{angle:0}", group.transform, MeshLibrary.Cube,
                    material,
                    new Vector3(-30f + i * 9f, 0.9f, 22f),
                    Quaternion.Euler(-angle, 0f, 0f),
                    new Vector3(7f, 0.6f, 11f));
                go.AddComponent<BoxCollider>();
            }
        }

        private static void BuildStairs(Transform parent, Material material)
        {
            var group = new GameObject("Stairs");
            group.transform.SetParent(parent, false);

            const int steps = 12;
            const float rise = 0.22f;
            const float run = 0.42f;

            for (int i = 0; i < steps; i++)
            {
                var go = MeshLibrary.CreatePart($"Step_{i}", group.transform, MeshLibrary.Cube, material,
                    new Vector3(22f, rise * (i + 0.5f), 14f + i * run), Quaternion.identity,
                    new Vector3(6f, rise, run));
                go.AddComponent<BoxCollider>();
            }

            // Landing at the top, high enough that falling off tests hard landings.
            var landing = MeshLibrary.CreatePart("Landing", group.transform, MeshLibrary.Cube, material,
                new Vector3(22f, steps * rise - 0.15f, 14f + steps * run + 3f), Quaternion.identity,
                new Vector3(6f, 0.3f, 6f));
            landing.AddComponent<BoxCollider>();
        }

        /// <summary>
        /// Tight geometry specifically for the camera: a corridor, a doorway and a
        /// low overhang, which are where a third-person camera usually breaks.
        /// </summary>
        private static void BuildCameraTestGeometry(Transform parent, Material wallMaterial, Material accent)
        {
            var group = new GameObject("Camera Test");
            group.transform.SetParent(parent, false);

            void Wall(Vector3 position, Vector3 scale, Quaternion? rot = null)
            {
                var go = MeshLibrary.CreatePart("Wall", group.transform, MeshLibrary.Cube, wallMaterial,
                    position, rot ?? Quaternion.identity, scale);
                go.AddComponent<BoxCollider>();
            }

            // Narrow corridor.
            Wall(new Vector3(-16f, 2f, -14f), new Vector3(0.6f, 4f, 16f));
            Wall(new Vector3(-11f, 2f, -14f), new Vector3(0.6f, 4f, 16f));

            // Overhang the camera has to duck under.
            Wall(new Vector3(-13.5f, 3.1f, -8f), new Vector3(5f, 0.5f, 5f));

            // Free-standing corner to whip the camera around.
            Wall(new Vector3(8f, 2.5f, -12f), new Vector3(9f, 5f, 0.7f));
            Wall(new Vector3(12.2f, 2.5f, -16f), new Vector3(0.7f, 5f, 8f));

            MeshLibrary.CreatePart("Marker", group.transform, MeshLibrary.Cube, accent,
                new Vector3(-13.5f, 0.02f, -14f), Quaternion.identity, new Vector3(3.6f, 0.04f, 3.6f),
                castShadows: false);
        }

        private static void BuildPillars(Transform parent, Material material)
        {
            var group = new GameObject("Pillars");
            group.transform.SetParent(parent, false);

            for (int i = 0; i < 7; i++)
            {
                float angle = i / 7f * Mathf.PI * 2f;
                var position = new Vector3(Mathf.Cos(angle) * 13f, 2.2f, Mathf.Sin(angle) * 13f - 2f);

                var go = MeshLibrary.CreatePart($"Pillar_{i}", group.transform, MeshLibrary.Cylinder,
                    material, position, Quaternion.identity, new Vector3(0.9f, 2.2f, 0.9f));

                var capsule = go.AddComponent<CapsuleCollider>();
                capsule.height = 2f;
                capsule.radius = 0.5f;
            }
        }

        private static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out Color c) ? c : Color.magenta;
        }
    }
}
