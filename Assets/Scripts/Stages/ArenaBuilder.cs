using Glowpulse.Core;
using Glowpulse.Core.Rendering;
using Glowpulse.World.City;
using UnityEngine;

namespace Glowpulse.Stages
{
    /// <summary>
    /// Builds the ground a stage is fought on.
    ///
    /// Each arena is dressed differently and, more importantly, shaped
    /// differently: the warehouse has pillars to break line of sight, the pit has
    /// a wall you can be cornered against, the rooftop has edges and a wide open
    /// middle. Layout is what makes a fight feel different - lighting and props
    /// only tell you where you are.
    ///
    /// It reuses <see cref="CityGeometry"/>, so an arena is welded into a handful
    /// of draw calls exactly like the city is, and <see cref="World.Props.PropFactory"/>
    /// for the furniture rather than growing a second set of props.
    /// </summary>
    public static class ArenaBuilder
    {
        public static Transform Build(StageDefinition stage, Transform parent, int seed)
        {
            var root = new GameObject($"Arena - {stage.Name}");
            root.transform.SetParent(parent, false);

            CityPalette palette = CityPalette.Build();
            var geometry = new CityGeometry(root.transform, batched: true);
            var rng = new System.Random(seed);

            float r = stage.ArenaRadius;

            Floor(geometry, palette, stage, r);

            switch (stage.Arena)
            {
                case ArenaKind.Warehouse: Warehouse(geometry, palette, r, rng); break;
                case ArenaKind.Docks: Docks(geometry, palette, r, rng); break;
                case ArenaKind.Underground: Underground(geometry, palette, r, rng); break;
                case ArenaKind.Rooftop: Rooftop(geometry, palette, r, rng); break;
                default: Streets(geometry, palette, r, rng); break;
            }

            geometry.Build();
            return root.transform;
        }

        private static void Floor(CityGeometry geo, CityPalette p, StageDefinition stage, float r)
        {
            Material surface = stage.Arena switch
            {
                ArenaKind.Warehouse => p.Concrete,
                ArenaKind.Docks => p.Kerb,
                ArenaKind.Underground => p.Sidewalk,
                ArenaKind.Rooftop => p.Concrete,
                _ => p.Road
            };

            // A generous slab: the fight must never run off the edge of the floor,
            // and the walls are what actually bound it.
            geo.AddBox(surface, new Vector3(0f, -0.25f, 0f),
                new Vector3(r * 2.6f, 0.5f, r * 2.6f));
            geo.AddCollider(new Vector3(0f, -0.25f, 0f),
                new Vector3(r * 2.6f, 0.5f, r * 2.6f), 0f, GameLayers.Ground);
        }

        /// <summary>Streets: buildings on all sides, a kerb line, parked cars.</summary>
        private static void Streets(CityGeometry geo, CityPalette p, float r, System.Random rng)
        {
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f + (float)rng.NextDouble() * 8f;
                float height = Mathf.Lerp(9f, 20f, (float)rng.NextDouble());
                Vector3 at = Ring(angle, r + 7f);

                geo.AddBox(p.Facade(i * 37), at + Vector3.up * (height * 0.5f),
                    new Vector3(11f, height, 11f), angle);
                geo.AddCollider(at + Vector3.up * (height * 0.5f),
                    new Vector3(11f, height, 11f), angle);
            }

            for (int i = 0; i < 5; i++)
            {
                float angle = 40f + i * 66f;
                World.Props.PropFactory.Car(geo, p, Ring(angle, r * 0.86f), angle + 90f, i * 13);
            }

            for (int i = 0; i < 8; i++)
                World.Props.PropFactory.StreetLamp(geo, p, Ring(i * 45f, r * 0.98f), i * 45f, i % 2 == 0);
        }

        /// <summary>Warehouse: pillars, stacked crates, a low roof, a shuttered wall.</summary>
        private static void Warehouse(CityGeometry geo, CityPalette p, float r, System.Random rng)
        {
            Walls(geo, p.Concrete, r, 8f, 1.2f);

            // A roof, which is what makes it read as inside rather than as a yard.
            geo.AddBox(p.DarkMetal, new Vector3(0f, 8.2f, 0f), new Vector3(r * 2.4f, 0.5f, r * 2.4f));

            // Pillars break the sightlines, so enemies arrive from behind cover.
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    var at = new Vector3(x * r * 0.5f, 4f, z * r * 0.5f);
                    geo.AddBox(p.Concrete, at, new Vector3(1.3f, 8f, 1.3f));
                    geo.AddCollider(at, new Vector3(1.3f, 8f, 1.3f));
                }
            }

            for (int i = 0; i < 7; i++)
                World.Props.PropFactory.Crates(geo, p, Ring(i * 51f, r * 0.78f), i * 91);

            World.Props.PropFactory.Skip(geo, p, Ring(200f, r * 0.6f), 24f);
        }

        /// <summary>Docks: containers, a crane leg, bollards, water beyond the edge.</summary>
        private static void Docks(CityGeometry geo, CityPalette p, float r, System.Random rng)
        {
            // Water on three sides. No wall here - the openness is the point, and
            // the containers are what the player has to work around instead.
            Material water = MaterialLibrary.Lit(new Color(0.08f, 0.13f, 0.17f), 0.85f, 0.1f);
            geo.AddBox(water, new Vector3(0f, -0.6f, 0f), new Vector3(r * 7f, 0.4f, r * 7f));

            for (int i = 0; i < 9; i++)
            {
                float angle = i * 40f + (float)rng.NextDouble() * 14f;
                float distance = r * Mathf.Lerp(0.55f, 0.92f, (float)rng.NextDouble());
                Vector3 at = Ring(angle, distance);

                bool stacked = rng.NextDouble() < 0.4;
                float height = stacked ? 5.2f : 2.6f;

                Material shell = MaterialLibrary.Lit(ContainerColour(i), 0.2f, 0.3f);
                geo.AddBox(shell, at + Vector3.up * (height * 0.5f),
                    new Vector3(2.8f, height, 6.2f), angle);
                geo.AddCollider(at + Vector3.up * (height * 0.5f),
                    new Vector3(2.8f, height, 6.2f), angle);
            }

            // A crane leg for scale, so the sky is not empty above a flat quay.
            geo.AddBox(p.Metal, new Vector3(r * 0.8f, 9f, -r * 0.7f), new Vector3(1.1f, 18f, 1.1f));
            geo.AddBox(p.Metal, new Vector3(r * 0.8f, 18f, 0f), new Vector3(1f, 0.9f, r * 1.6f));

            for (int i = 0; i < 10; i++)
                World.Props.PropFactory.StreetLamp(geo, p, Ring(i * 36f, r * 1.02f), i * 36f, i % 2 == 0);
        }

        /// <summary>Underground: a pit with a hard barrier and lights overhead.</summary>
        private static void Underground(CityGeometry geo, CityPalette p, float r, System.Random rng)
        {
            // A ring you can be cornered against - the whole personality of the pit.
            Walls(geo, p.Kerb, r, 3.2f, 1.6f);

            // Terraced seating outside the barrier, so the fight is watched.
            for (int tier = 0; tier < 4; tier++)
            {
                float radius = r + 3f + tier * 2.2f;
                float height = 1.4f + tier * 1.1f;

                for (int i = 0; i < 26; i++)
                {
                    float angle = i * (360f / 26f);
                    geo.AddBox(p.Concrete, Ring(angle, radius) + Vector3.up * (height * 0.5f),
                        new Vector3(3.2f, height, 2.2f), angle);
                }
            }

            // A hard ceiling with a ring of lamps: the light is all from above,
            // which is what makes a pit feel like a pit.
            geo.AddBox(p.DarkMetal, new Vector3(0f, 11f, 0f), new Vector3(r * 4f, 0.6f, r * 4f));

            for (int i = 0; i < 8; i++)
            {
                Vector3 at = Ring(i * 45f, r * 0.7f) + Vector3.up * 10.4f;
                geo.AddBox(p.WindowLit, at, new Vector3(1.4f, 0.2f, 1.4f));

                GameObject go = geo.AddLoose("Pit Light", at, Quaternion.identity);
                Light light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.93f, 0.82f);
                light.intensity = 3.2f;
                light.range = 22f;
                light.shadows = LightShadows.None;
            }
        }

        /// <summary>Rooftop: a wide deck, a parapet, plant rooms and the skyline below.</summary>
        private static void Rooftop(CityGeometry geo, CityPalette p, float r, System.Random rng)
        {
            // A parapet, not a wall: low enough to see over, high enough to stop
            // the player walking off the building by accident.
            Walls(geo, p.Concrete, r, 1.1f, 0.5f);

            // Plant rooms and vents give the deck something to fight around
            // without closing it in.
            for (int i = 0; i < 5; i++)
            {
                float angle = 30f + i * 72f;
                Vector3 at = Ring(angle, r * 0.62f);
                float height = Mathf.Lerp(2f, 3.4f, (float)rng.NextDouble());

                geo.AddBox(p.Metal, at + Vector3.up * (height * 0.5f),
                    new Vector3(3.4f, height, 2.6f), angle);
                geo.AddCollider(at + Vector3.up * (height * 0.5f),
                    new Vector3(3.4f, height, 2.6f), angle);
            }

            // The skyline: towers below the deck, so the edge reads as a drop.
            for (int i = 0; i < 22; i++)
            {
                float angle = i * (360f / 22f) + (float)rng.NextDouble() * 9f;
                float distance = r + 14f + (float)rng.NextDouble() * 26f;
                float height = Mathf.Lerp(14f, 46f, (float)rng.NextDouble());

                geo.AddBox(p.Facade(i * 53), Ring(angle, distance) + Vector3.down * (2f + height * 0.5f),
                    new Vector3(9f, height, 9f), angle);
            }

            for (int i = 0; i < 6; i++)
                World.Props.PropFactory.StreetLamp(geo, p, Ring(i * 60f, r * 0.92f), i * 60f, true);
        }

        /// <summary>A ring of wall segments, used for every enclosed arena.</summary>
        private static void Walls(CityGeometry geo, Material material, float r, float height,
            float thickness)
        {
            const int segments = 24;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * (360f / segments);
                Vector3 at = Ring(angle, r + thickness * 0.5f) + Vector3.up * (height * 0.5f);

                // Segments overlap slightly, or the seams open up into gaps a
                // character can be pushed through by a knockback.
                var size = new Vector3(r * 2f * Mathf.PI / segments * 1.15f, height, thickness);

                geo.AddBox(material, at, size, angle);
                geo.AddCollider(at, size, angle);
            }
        }

        private static Vector3 Ring(float degrees, float radius)
        {
            return Quaternion.Euler(0f, degrees, 0f) * Vector3.forward * radius;
        }

        private static Color ContainerColour(int index)
        {
            Color[] colours =
            {
                new Color(0.45f, 0.25f, 0.2f), new Color(0.2f, 0.32f, 0.36f),
                new Color(0.35f, 0.36f, 0.28f), new Color(0.28f, 0.2f, 0.26f),
                new Color(0.22f, 0.34f, 0.26f)
            };

            return colours[Mathf.Abs(index) % colours.Length];
        }
    }
}
