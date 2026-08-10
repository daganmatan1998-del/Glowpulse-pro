using Glowpulse.Core;
using UnityEngine;

namespace Glowpulse.World.City
{
    /// <summary>
    /// Turns a <see cref="Plot"/> into a building.
    ///
    /// Everything is boxes, but the silhouette is what sells a street: a ground
    /// floor that reads differently from the storeys above, a cornice line, a
    /// setback on the taller masses, and roof clutter so the skyline is not a row
    /// of flat lids. Detail is spent on the first six metres, because that is the
    /// part the player ever stands next to.
    /// </summary>
    public static class BuildingFactory
    {
        public static void Build(CityGeometry geo, CityPalette palette, in Plot plot, CitySettings s)
        {
            var rng = new System.Random(plot.Seed);
            Vector3 center = new Vector3(plot.Center.x, 0f, plot.Center.y);

            float width = plot.Size.x;
            float depth = plot.Size.y;
            float height = plot.Height;
            float groundFloor = Mathf.Min(4.2f, height * 0.42f);

            Material facade = palette.Facade(plot.Seed);
            Material trim = palette.Trim(plot.Seed >> 3);

            // ---- main mass -------------------------------------------------
            float upperHeight = height - groundFloor;
            geo.AddBox(facade,
                center + new Vector3(0f, groundFloor + upperHeight * 0.5f, 0f),
                new Vector3(width, upperHeight, depth));

            // Ground floor is inset slightly, which reads as a shopfront line and
            // gives the pavement a lip to sit under.
            const float inset = 0.22f;
            geo.AddBox(palette.Concrete,
                center + new Vector3(0f, groundFloor * 0.5f, 0f),
                new Vector3(width - inset, groundFloor, depth - inset));

            // ---- cornice ---------------------------------------------------
            geo.AddBox(trim,
                center + new Vector3(0f, groundFloor + 0.12f, 0f),
                new Vector3(width + 0.3f, 0.24f, depth + 0.3f));

            geo.AddBox(trim,
                center + new Vector3(0f, height - 0.2f, 0f),
                new Vector3(width + 0.42f, 0.4f, depth + 0.42f));

            // ---- setback on tall buildings ----------------------------------
            if (height > s.MinHeight + 9f && rng.NextDouble() < 0.55)
            {
                float capH = Mathf.Lerp(2.5f, 5.5f, (float)rng.NextDouble());
                geo.AddBox(facade,
                    center + new Vector3(0f, height + capH * 0.5f, 0f),
                    new Vector3(width * 0.72f, capH, depth * 0.72f));
                height += capH;
            }

            BuildFrontage(geo, palette, plot, rng, groundFloor, width, depth, center);
            BuildWindows(geo, palette, plot, rng, groundFloor, width, depth, center);
            BuildRoof(geo, palette, rng, center, width, depth, height);

            // One collider for the whole mass. Physics does not need the detail,
            // and a single box is far cheaper than the geometry it stands in for.
            geo.AddCollider(center + new Vector3(0f, height * 0.5f, 0f),
                new Vector3(width, height, depth));
        }

        /// <summary>Street-level detail: shopfront glazing, doors, awnings and signage.</summary>
        private static void BuildFrontage(CityGeometry geo, CityPalette palette, in Plot plot,
            System.Random rng, float groundFloor, float width, float depth, Vector3 center)
        {
            // The frontage faces the street; the yaw tells us which way that is.
            Quaternion facing = Quaternion.Euler(0f, plot.FacingYaw, 0f);
            Vector3 outward = facing * Vector3.back;
            Vector3 along = facing * Vector3.right;

            float frontLength = Mathf.Abs(Vector3.Dot(along, new Vector3(width, 0f, depth)));
            if (frontLength < 1f) frontLength = Mathf.Max(width, depth);

            float halfDepth = Mathf.Abs(Vector3.Dot(outward, new Vector3(width, 0f, depth))) * 0.5f;
            Vector3 wall = center + outward * (halfDepth - 0.12f);

            bool commercial = plot.District == District.Commercial;
            bool industrial = plot.District == District.Industrial;

            if (industrial)
            {
                // A roller shutter and a dock lip: reads as somewhere goods move.
                float doorW = Mathf.Min(frontLength * 0.5f, 5.5f);
                geo.Add(Core.Rendering.MeshLibrary.Cube, palette.DarkMetal,
                    wall + Vector3.up * (groundFloor * 0.42f) + outward * 0.06f,
                    facing, new Vector3(doorW, groundFloor * 0.78f, 0.18f));

                geo.Add(Core.Rendering.MeshLibrary.Cube, palette.Concrete,
                    wall + Vector3.up * 0.35f + outward * 0.7f,
                    facing, new Vector3(doorW + 1.4f, 0.7f, 1.5f));
                return;
            }

            // Glazing runs most of the frontage with mullions between panes.
            int bays = Mathf.Max(1, Mathf.RoundToInt(frontLength / 2.6f));
            float bayWidth = frontLength / bays;

            for (int i = 0; i < bays; i++)
            {
                float offset = -frontLength * 0.5f + bayWidth * (i + 0.5f);
                Vector3 bayCenter = wall + along * offset;

                bool door = i == bays / 2;
                Material pane = door ? palette.DarkMetal : palette.Glass;

                geo.Add(Core.Rendering.MeshLibrary.Cube, pane,
                    bayCenter + Vector3.up * (groundFloor * 0.55f) + outward * 0.05f,
                    facing, new Vector3(bayWidth * 0.82f, groundFloor * 0.62f, 0.12f));

                geo.Add(Core.Rendering.MeshLibrary.Cube, palette.Trim(i),
                    bayCenter + along * (bayWidth * 0.5f) + Vector3.up * (groundFloor * 0.5f)
                    + outward * 0.06f,
                    facing, new Vector3(0.16f, groundFloor, 0.2f));
            }

            if (!commercial) return;

            // Awning plus a lit sign above it. This is most of what makes a street
            // look inhabited rather than modelled.
            Material shop = palette.Shopfront(plot.Seed >> 5);
            geo.Add(Core.Rendering.MeshLibrary.Cube, shop,
                wall + Vector3.up * (groundFloor * 0.86f) + outward * 0.75f,
                facing * Quaternion.Euler(-14f, 0f, 0f),
                new Vector3(frontLength * 0.86f, 0.1f, 1.6f));

            if (rng.NextDouble() < 0.7f)
            {
                geo.Add(Core.Rendering.MeshLibrary.Cube, palette.Neon,
                    wall + Vector3.up * (groundFloor + 0.75f) + outward * 0.28f,
                    facing, new Vector3(frontLength * 0.42f, 0.62f, 0.1f));
            }
        }

        /// <summary>
        /// A grid of window recesses over the upper storeys, with a share of them
        /// lit. Punching the openings in as separate quads is what stops the
        /// facades from reading as blank slabs at night.
        /// </summary>
        private static void BuildWindows(CityGeometry geo, CityPalette palette, in Plot plot,
            System.Random rng, float groundFloor, float width, float depth, Vector3 center)
        {
            const float floorHeight = 3.2f;
            int floors = Mathf.FloorToInt((plot.Height - groundFloor) / floorHeight);
            if (floors <= 0) return;

            float litChance = plot.District == District.Industrial ? 0.18f : 0.42f;

            for (int side = 0; side < 4; side++)
            {
                bool alongX = side == 0 || side == 2;
                float faceLength = alongX ? width : depth;
                float outDistance = (alongX ? depth : width) * 0.5f;

                Vector3 normal = side switch
                {
                    0 => Vector3.back,
                    1 => Vector3.right,
                    2 => Vector3.forward,
                    _ => Vector3.left
                };
                Vector3 tangent = alongX ? Vector3.right : Vector3.forward;
                Quaternion rot = Quaternion.LookRotation(normal, Vector3.up);

                int columns = Mathf.Max(1, Mathf.FloorToInt(faceLength / 2.4f));
                float columnWidth = faceLength / columns;

                for (int f = 0; f < floors; f++)
                {
                    float y = groundFloor + floorHeight * (f + 0.55f);

                    for (int c = 0; c < columns; c++)
                    {
                        float offset = -faceLength * 0.5f + columnWidth * (c + 0.5f);
                        Vector3 pos = center + tangent * offset + normal * (outDistance + 0.02f)
                                      + Vector3.up * y;

                        bool lit = rng.NextDouble() < litChance;
                        geo.Add(Core.Rendering.MeshLibrary.Cube,
                            lit ? palette.WindowLit : palette.Glass,
                            pos, rot, new Vector3(columnWidth * 0.55f, 1.5f, 0.08f));
                    }
                }
            }
        }

        /// <summary>Roof clutter, so the skyline has a texture instead of a flat lid.</summary>
        private static void BuildRoof(CityGeometry geo, CityPalette palette, System.Random rng,
            Vector3 center, float width, float depth, float height)
        {
            int units = rng.Next(1, 4);
            for (int i = 0; i < units; i++)
            {
                float w = Mathf.Lerp(1.2f, 2.8f, (float)rng.NextDouble());
                float d = Mathf.Lerp(1.2f, 2.4f, (float)rng.NextDouble());
                float h = Mathf.Lerp(0.7f, 1.6f, (float)rng.NextDouble());

                float ox = Mathf.Lerp(-width * 0.3f, width * 0.3f, (float)rng.NextDouble());
                float oz = Mathf.Lerp(-depth * 0.3f, depth * 0.3f, (float)rng.NextDouble());

                geo.AddBox(palette.Metal,
                    center + new Vector3(ox, height + h * 0.5f, oz), new Vector3(w, h, d));
            }

            // A parapet, which reads as a roof you could stand on.
            geo.AddBox(palette.Concrete, center + new Vector3(0f, height + 0.35f, 0f),
                new Vector3(width + 0.2f, 0.7f, depth + 0.2f));
            geo.AddBox(palette.Concrete, center + new Vector3(0f, height + 0.35f, 0f),
                new Vector3(width - 0.9f, 0.72f, depth - 0.9f));

            if (rng.NextDouble() < 0.35)
            {
                float poleH = Mathf.Lerp(2.5f, 5f, (float)rng.NextDouble());
                geo.AddCylinder(palette.Metal,
                    center + new Vector3(width * 0.25f, height + poleH * 0.5f, depth * 0.2f),
                    0.08f, poleH * 0.5f);
            }
        }
    }
}
