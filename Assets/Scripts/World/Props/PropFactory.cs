using Glowpulse.Core;
using Glowpulse.Core.Rendering;
using Glowpulse.World.City;
using UnityEngine;

namespace Glowpulse.World.Props
{
    /// <summary>
    /// The things that make a street feel used: lamps, benches, bins, trees,
    /// planters, hydrants, signs, bus shelters and parked cars.
    ///
    /// Props are what separate a city from a set of boxes, so they get real
    /// silhouettes - a bench has slats and legs, a tree has a canopy of
    /// overlapping masses - while still being cheap enough to weld into the
    /// city's batched geometry.
    /// </summary>
    public static class PropFactory
    {
        /// <summary>A street light with a real pool of light under it.</summary>
        public static void StreetLamp(CityGeometry geo, CityPalette p, Vector3 basePos, float yaw,
            bool withLight)
        {
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 arm = rot * Vector3.forward;

            geo.AddCylinder(p.DarkMetal, basePos + Vector3.up * 0.12f, 0.2f, 0.12f);
            geo.AddCylinder(p.Metal, basePos + Vector3.up * 2.4f, 0.075f, 2.3f);
            geo.Add(MeshLibrary.Cube, p.Metal, basePos + Vector3.up * 4.62f + arm * 0.55f,
                rot * Quaternion.Euler(0f, 0f, 0f), new Vector3(0.09f, 0.09f, 1.2f));

            Vector3 head = basePos + Vector3.up * 4.5f + arm * 1.1f;
            geo.Add(MeshLibrary.Cube, p.DarkMetal, head + Vector3.up * 0.1f, rot,
                new Vector3(0.42f, 0.14f, 0.72f));
            geo.Add(MeshLibrary.Cube, p.WindowLit, head - Vector3.up * 0.02f, rot,
                new Vector3(0.34f, 0.06f, 0.6f));

            geo.AddCollider(basePos + Vector3.up * 2.2f, new Vector3(0.32f, 4.4f, 0.32f));

            if (!withLight) return;

            // Only a share of lamps carry a real light: a warm pool every few
            // metres is enough, and point lights are the expensive part.
            GameObject go = geo.AddLoose("Lamp Light", head, Quaternion.identity);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.84f, 0.62f);
            light.intensity = 2.6f;
            light.range = 12f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForceVertex;
        }

        public static void Bench(CityGeometry geo, CityPalette p, Vector3 pos, float yaw)
        {
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            Material wood = p.Trunk;

            for (int i = 0; i < 3; i++)
                geo.Add(MeshLibrary.Cube, wood, pos + Vector3.up * 0.46f + rot * new Vector3(0f, 0f, -0.22f + i * 0.22f),
                    rot, new Vector3(1.7f, 0.07f, 0.18f));

            for (int i = 0; i < 2; i++)
                geo.Add(MeshLibrary.Cube, wood,
                    pos + Vector3.up * (0.72f + i * 0.2f) + rot * new Vector3(0f, 0f, -0.34f),
                    rot * Quaternion.Euler(18f, 0f, 0f), new Vector3(1.7f, 0.07f, 0.16f));

            for (int s = -1; s <= 1; s += 2)
                geo.Add(MeshLibrary.Cube, p.DarkMetal,
                    pos + Vector3.up * 0.23f + rot * new Vector3(s * 0.78f, 0f, 0f),
                    rot, new Vector3(0.1f, 0.46f, 0.6f));

            geo.AddCollider(pos + Vector3.up * 0.45f, new Vector3(1.8f, 0.9f, 0.7f), yaw);
        }

        public static void Bin(CityGeometry geo, CityPalette p, Vector3 pos, float yaw)
        {
            geo.AddCylinder(p.DarkMetal, pos + Vector3.up * 0.42f, 0.3f, 0.42f, yaw);
            geo.AddCylinder(p.Metal, pos + Vector3.up * 0.88f, 0.32f, 0.04f, yaw);
            geo.AddCollider(pos + Vector3.up * 0.45f, new Vector3(0.62f, 0.9f, 0.62f));
        }

        public static void Tree(CityGeometry geo, CityPalette p, Vector3 pos, int seed)
        {
            var rng = new System.Random(seed);
            float height = Mathf.Lerp(3.4f, 5.2f, (float)rng.NextDouble());

            geo.AddCylinder(p.Trunk, pos + Vector3.up * (height * 0.35f), 0.16f, height * 0.35f);

            // Three overlapping masses read as a canopy from any angle, which one
            // sphere never does.
            for (int i = 0; i < 3; i++)
            {
                float t = i / 2f;
                float r = Mathf.Lerp(1.5f, 0.95f, t) * Mathf.Lerp(0.9f, 1.15f, (float)rng.NextDouble());
                Vector3 offset = new Vector3(
                    Mathf.Lerp(-0.35f, 0.35f, (float)rng.NextDouble()),
                    height * 0.62f + t * 0.85f,
                    Mathf.Lerp(-0.35f, 0.35f, (float)rng.NextDouble()));
                geo.AddSphere(p.Foliage, pos + offset, new Vector3(r * 2f, r * 1.7f, r * 2f));
            }

            // Tree pit, so it does not sprout straight out of the paving.
            geo.AddBox(p.Kerb, pos + Vector3.up * 0.06f, new Vector3(1.3f, 0.12f, 1.3f));
            geo.AddCollider(pos + Vector3.up * 1f, new Vector3(0.45f, 2f, 0.45f));
        }

        public static void Planter(CityGeometry geo, CityPalette p, Vector3 pos, float yaw)
        {
            geo.AddBox(p.Concrete, pos + Vector3.up * 0.3f, new Vector3(1.5f, 0.6f, 0.8f), yaw);
            geo.AddBox(p.Foliage, pos + Vector3.up * 0.66f, new Vector3(1.3f, 0.18f, 0.62f), yaw);
            geo.AddCollider(pos + Vector3.up * 0.3f, new Vector3(1.5f, 0.6f, 0.8f), yaw);
        }

        public static void Hydrant(CityGeometry geo, CityPalette p, Vector3 pos)
        {
            Material red = MaterialLibrary.Lit(new Color(0.66f, 0.22f, 0.18f), 0.3f);
            geo.AddCylinder(red, pos + Vector3.up * 0.32f, 0.13f, 0.32f);
            geo.AddSphere(red, pos + Vector3.up * 0.68f, Vector3.one * 0.28f);
            for (int s = -1; s <= 1; s += 2)
                geo.AddCylinder(red, pos + Vector3.up * 0.46f + new Vector3(s * 0.16f, 0f, 0f), 0.06f, 0.08f, 90f);
        }

        public static void SignPost(CityGeometry geo, CityPalette p, Vector3 pos, float yaw, int seed)
        {
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            geo.AddCylinder(p.Metal, pos + Vector3.up * 1.3f, 0.045f, 1.3f);
            geo.Add(MeshLibrary.Cube, p.Shopfront(seed), pos + Vector3.up * 2.35f, rot,
                new Vector3(1.15f, 0.34f, 0.06f));
        }

        public static void BusShelter(CityGeometry geo, CityPalette p, Vector3 pos, float yaw)
        {
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            geo.Add(MeshLibrary.Cube, p.Metal, pos + Vector3.up * 2.5f, rot, new Vector3(4.2f, 0.12f, 1.7f));
            geo.Add(MeshLibrary.Cube, p.Glass, pos + Vector3.up * 1.3f + rot * new Vector3(0f, 0f, -0.8f),
                rot, new Vector3(4.1f, 2.3f, 0.06f));
            for (int s = -1; s <= 1; s += 2)
                geo.AddCylinder(p.Metal, pos + Vector3.up * 1.25f + rot * new Vector3(s * 2f, 0f, 0.7f), 0.06f, 1.25f);
            Bench(geo, p, pos + rot * new Vector3(0f, 0f, -0.45f), yaw);
            geo.AddCollider(pos + Vector3.up * 1.3f + rot * new Vector3(0f, 0f, -0.8f),
                new Vector3(4.2f, 2.6f, 0.2f), yaw);
        }

        /// <summary>A parked car. Static set dressing and, more usefully, cover.</summary>
        public static void Car(CityGeometry geo, CityPalette p, Vector3 pos, float yaw, int seed)
        {
            var rng = new System.Random(seed);
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            Material body = p.Vehicle(seed);

            geo.Add(MeshLibrary.Cube, body, pos + Vector3.up * 0.62f, rot, new Vector3(1.85f, 0.62f, 4.3f));
            geo.Add(MeshLibrary.Cube, body, pos + Vector3.up * 1.16f + rot * new Vector3(0f, 0f, -0.25f),
                rot, new Vector3(1.68f, 0.52f, 2.2f));
            geo.Add(MeshLibrary.Cube, p.Glass, pos + Vector3.up * 1.18f + rot * new Vector3(0f, 0f, -0.26f),
                rot, new Vector3(1.72f, 0.4f, 2.1f));

            // Wheels sit slightly inboard so the body overhangs them.
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    geo.AddCylinder(p.Rubber,
                        pos + Vector3.up * 0.34f + rot * new Vector3(sx * 0.82f, 0f, sz * 1.42f),
                        0.34f, 0.11f, yaw + 90f);
                }
            }

            Material lampMat = rng.NextDouble() < 0.5 ? p.WindowLit : p.Metal;
            for (int sx = -1; sx <= 1; sx += 2)
                geo.Add(MeshLibrary.Cube, lampMat,
                    pos + Vector3.up * 0.68f + rot * new Vector3(sx * 0.6f, 0f, 2.13f),
                    rot, new Vector3(0.34f, 0.18f, 0.06f));

            geo.AddCollider(pos + Vector3.up * 0.7f, new Vector3(2f, 1.4f, 4.4f), yaw, GameLayers.Prop);
        }

        /// <summary>A skip, which doubles as a waist-high obstacle to fight around.</summary>
        public static void Skip(CityGeometry geo, CityPalette p, Vector3 pos, float yaw)
        {
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            Material rust = MaterialLibrary.Lit(new Color(0.42f, 0.3f, 0.22f), 0.25f);

            geo.Add(MeshLibrary.Cube, rust, pos + Vector3.up * 0.65f, rot, new Vector3(2.1f, 1.3f, 3.4f));
            geo.Add(MeshLibrary.Cube, p.DarkMetal, pos + Vector3.up * 1.32f, rot,
                new Vector3(2.2f, 0.12f, 3.5f));
            geo.AddCollider(pos + Vector3.up * 0.65f, new Vector3(2.1f, 1.3f, 3.4f), yaw, GameLayers.Prop);
        }

        public static void Crates(CityGeometry geo, CityPalette p, Vector3 pos, int seed)
        {
            var rng = new System.Random(seed);
            int count = rng.Next(2, 5);
            for (int i = 0; i < count; i++)
            {
                float size = Mathf.Lerp(0.6f, 0.95f, (float)rng.NextDouble());
                var offset = new Vector3(
                    Mathf.Lerp(-0.7f, 0.7f, (float)rng.NextDouble()), 0f,
                    Mathf.Lerp(-0.7f, 0.7f, (float)rng.NextDouble()));
                float yaw = (float)rng.NextDouble() * 90f;
                bool stacked = i > 0 && rng.NextDouble() < 0.4;
                float y = stacked ? size * 1.5f : size * 0.5f;

                geo.AddBox(p.Trunk, pos + offset + Vector3.up * y, Vector3.one * size, yaw);
                geo.AddCollider(pos + offset + Vector3.up * y, Vector3.one * size, yaw, GameLayers.Prop);
            }
        }
    }
}
