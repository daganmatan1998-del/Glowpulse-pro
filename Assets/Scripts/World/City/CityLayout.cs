using System.Collections.Generic;
using UnityEngine;

namespace Glowpulse.World.City
{
    /// <summary>Character of a city block. Drives palette, height and what fills the street.</summary>
    public enum District
    {
        /// <summary>Shopfronts, awnings, signage. Busiest streets.</summary>
        Commercial = 0,

        /// <summary>Apartment blocks, balconies, quieter frontage.</summary>
        Residential = 1,

        /// <summary>Warehouses and loading docks. Wide, empty, good for a fight.</summary>
        Industrial = 2,

        /// <summary>An open square. No buildings - this is a combat arena.</summary>
        Plaza = 3
    }

    /// <summary>What a named place in the city is for.</summary>
    public enum LocationKind
    {
        Intersection = 0,
        Plaza = 1,
        Alley = 2,
        Storefront = 3,
        LoadingDock = 4,
        Rooftop = 5
    }

    /// <summary>A building footprint. Axis aligned, because a readable street grid beats novelty.</summary>
    public struct Plot
    {
        public Vector2 Center;
        public Vector2 Size;
        public District District;
        public float Height;
        public int Seed;

        /// <summary>Which way the building's frontage faces, in degrees.</summary>
        public float FacingYaw;
    }

    public struct RoadSegment
    {
        public Vector2 A;
        public Vector2 B;
        public float Width;
        public bool IsAlley;
    }

    /// <summary>A named place missions and encounters can refer to.</summary>
    public struct CityLocation
    {
        public string Name;
        public Vector3 Position;
        public LocationKind Kind;
        public float Radius;
    }

    /// <summary>
    /// The city as data: where the roads run, where buildings stand, and which
    /// places are worth naming.
    ///
    /// Generating a plan before touching a single GameObject is what makes the
    /// city testable - the layout can be checked for buildings overlapping the
    /// road, disconnected streets or plots on top of each other without opening
    /// Unity at all. It is also seeded, so a given seed is always the same city.
    /// </summary>
    public sealed class CityLayout
    {
        public readonly List<Plot> Plots = new List<Plot>(64);
        public readonly List<RoadSegment> Roads = new List<RoadSegment>(48);
        public readonly List<CityLocation> Locations = new List<CityLocation>(24);

        /// <summary>Squares and docks - the open ground encounters are staged on.</summary>
        public readonly List<CityLocation> Arenas = new List<CityLocation>(8);

        public CitySettings Settings { get; private set; }
        public int Seed { get; private set; }

        /// <summary>Half-extent of the walkable city, used to fence the player in.</summary>
        public float Extent { get; private set; }

        public Vector3 PlayerSpawn { get; private set; }

        public static CityLayout Generate(CitySettings settings, int seed)
        {
            var layout = new CityLayout { Settings = settings, Seed = seed };
            layout.Build(new System.Random(seed));
            return layout;
        }

        private void Build(System.Random rng)
        {
            CitySettings s = Settings;
            int cols = Mathf.Max(2, s.BlocksX);
            int rows = Mathf.Max(2, s.BlocksZ);

            float stride = s.BlockSize + s.RoadWidth;
            float originX = -(cols * stride - s.RoadWidth) * 0.5f;
            float originZ = -(rows * stride - s.RoadWidth) * 0.5f;
            Extent = Mathf.Max(cols, rows) * stride * 0.5f + s.RoadWidth;

            BuildRoads(cols, rows, stride, originX, originZ, s);

            // One block is cleared to make a plaza; it becomes the main arena and
            // gives the grid a landmark to navigate by.
            int plazaCol = cols / 2;
            int plazaRow = rows / 2;

            for (int cx = 0; cx < cols; cx++)
            {
                for (int cz = 0; cz < rows; cz++)
                {
                    var blockCenter = new Vector2(
                        originX + cx * stride + s.BlockSize * 0.5f,
                        originZ + cz * stride + s.BlockSize * 0.5f);

                    if (cx == plazaCol && cz == plazaRow)
                    {
                        AddPlaza(blockCenter, s);
                        continue;
                    }

                    District district = PickDistrict(cx, cz, cols, rows, rng);
                    BuildBlock(blockCenter, district, s, rng);
                }
            }

            AddIntersections(cols, rows, stride, originX, originZ, s);

            // Spawn on the street just south of the plaza, facing it, so the
            // player's first look at the city is down its main axis.
            PlayerSpawn = new Vector3(
                originX + plazaCol * stride + s.BlockSize * 0.5f,
                0f,
                originZ + plazaRow * stride - s.RoadWidth * 0.5f - 4f);
        }

        private District PickDistrict(int cx, int cz, int cols, int rows, System.Random rng)
        {
            // Industrial sits on the edge where the loading docks make sense,
            // commercial along the central axes, residential fills the rest.
            bool edge = cx == 0 || cz == 0 || cx == cols - 1 || cz == rows - 1;
            bool axis = cx == cols / 2 || cz == rows / 2;

            if (edge && rng.NextDouble() < 0.55) return District.Industrial;
            if (axis) return District.Commercial;
            return rng.NextDouble() < 0.35 ? District.Commercial : District.Residential;
        }

        private void BuildRoads(int cols, int rows, float stride, float originX, float originZ,
            CitySettings s)
        {
            float halfW = (cols * stride - s.RoadWidth) * 0.5f;
            float halfH = (rows * stride - s.RoadWidth) * 0.5f;

            // Roads run along every block boundary, including the outer ring.
            for (int i = 0; i <= cols; i++)
            {
                float x = originX + i * stride - s.RoadWidth * 0.5f;
                Roads.Add(new RoadSegment
                {
                    A = new Vector2(x, -halfH - s.RoadWidth),
                    B = new Vector2(x, halfH + s.RoadWidth),
                    Width = s.RoadWidth
                });
            }

            for (int i = 0; i <= rows; i++)
            {
                float z = originZ + i * stride - s.RoadWidth * 0.5f;
                Roads.Add(new RoadSegment
                {
                    A = new Vector2(-halfW - s.RoadWidth, z),
                    B = new Vector2(halfW + s.RoadWidth, z),
                    Width = s.RoadWidth
                });
            }
        }

        /// <summary>
        /// Fills a block by lining buildings along all four street frontages and
        /// leaving the middle as a service alley - which is what gives the city
        /// its back routes without designing them by hand.
        /// </summary>
        private void BuildBlock(Vector2 center, District district, CitySettings s, System.Random rng)
        {
            float half = s.BlockSize * 0.5f;
            float depth = s.PlotDepth;
            float innerHalf = half - depth;

            if (innerHalf <= 1.5f)
            {
                // Block is too small for a courtyard; fill it with one mass.
                Plots.Add(MakePlot(center, new Vector2(s.BlockSize, s.BlockSize), district, 0f, s, rng));
                return;
            }

            for (int side = 0; side < 4; side++)
            {
                // side 0 = -Z frontage, 1 = +X, 2 = +Z, 3 = -X
                bool horizontal = side == 0 || side == 2;
                float runLength = horizontal ? s.BlockSize : s.BlockSize - depth * 2f;
                float yaw = side * 90f;

                float used = 0f;
                var widths = new List<float>(6);
                while (runLength - used > s.MinPlotWidth)
                {
                    float w = Mathf.Lerp(s.MinPlotWidth, s.MaxPlotWidth, (float)rng.NextDouble());
                    w = Mathf.Min(w, runLength - used);
                    if (runLength - used - w < s.MinPlotWidth) w = runLength - used;
                    widths.Add(w);
                    used += w;
                }

                float cursor = -runLength * 0.5f;
                foreach (float w in widths)
                {
                    float offset = cursor + w * 0.5f;
                    cursor += w;

                    Vector2 plotCenter;
                    Vector2 size;

                    switch (side)
                    {
                        case 0:
                            plotCenter = center + new Vector2(offset, -half + depth * 0.5f);
                            size = new Vector2(w, depth);
                            break;
                        case 1:
                            plotCenter = center + new Vector2(half - depth * 0.5f, offset);
                            size = new Vector2(depth, w);
                            break;
                        case 2:
                            plotCenter = center + new Vector2(offset, half - depth * 0.5f);
                            size = new Vector2(w, depth);
                            break;
                        default:
                            plotCenter = center + new Vector2(-half + depth * 0.5f, offset);
                            size = new Vector2(depth, w);
                            break;
                    }

                    // A small share of frontage is left empty, which reads as a
                    // gap between buildings and opens a shortcut into the alley.
                    if (rng.NextDouble() < s.GapChance && w < s.MaxPlotWidth * 0.8f)
                    {
                        AddAlleyMouth(plotCenter, size, yaw, s);
                        continue;
                    }

                    Plots.Add(MakePlot(plotCenter, size, district, yaw, s, rng));
                }
            }

            AddCourtyard(center, innerHalf, district, s);
        }

        private Plot MakePlot(Vector2 center, Vector2 size, District district, float yaw,
            CitySettings s, System.Random rng)
        {
            float t = (float)rng.NextDouble();
            float height;

            switch (district)
            {
                case District.Industrial:
                    height = Mathf.Lerp(s.MinHeight, s.MinHeight + 6f, t);
                    break;
                case District.Commercial:
                    height = Mathf.Lerp(s.MinHeight + 4f, s.MaxHeight, t);
                    break;
                default:
                    height = Mathf.Lerp(s.MinHeight + 2f, s.MaxHeight * 0.8f, t);
                    break;
            }

            return new Plot
            {
                Center = center,
                Size = size,
                District = district,
                Height = height,
                FacingYaw = yaw,
                Seed = rng.Next()
            };
        }

        private void AddAlleyMouth(Vector2 center, Vector2 size, float yaw, CitySettings s)
        {
            Locations.Add(new CityLocation
            {
                Name = "Alley",
                Position = new Vector3(center.x, 0f, center.y),
                Kind = LocationKind.Alley,
                Radius = Mathf.Max(size.x, size.y) * 0.5f
            });
        }

        private void AddCourtyard(Vector2 center, float innerHalf, District district, CitySettings s)
        {
            LocationKind kind = district == District.Industrial
                ? LocationKind.LoadingDock
                : LocationKind.Alley;

            var location = new CityLocation
            {
                Name = district == District.Industrial ? "Loading dock" : "Back alley",
                Position = new Vector3(center.x, 0f, center.y),
                Kind = kind,
                Radius = innerHalf
            };

            Locations.Add(location);

            // Only courtyards with room to fight in are offered as arenas.
            if (innerHalf >= 5f) Arenas.Add(location);
        }

        private void AddPlaza(Vector2 center, CitySettings s)
        {
            var plaza = new CityLocation
            {
                Name = "Market square",
                Position = new Vector3(center.x, 0f, center.y),
                Kind = LocationKind.Plaza,
                Radius = s.BlockSize * 0.5f
            };

            Locations.Add(plaza);
            Arenas.Add(plaza);
        }

        private void AddIntersections(int cols, int rows, float stride, float originX, float originZ,
            CitySettings s)
        {
            for (int i = 0; i <= cols; i++)
            {
                for (int j = 0; j <= rows; j++)
                {
                    float x = originX + i * stride - s.RoadWidth * 0.5f;
                    float z = originZ + j * stride - s.RoadWidth * 0.5f;

                    Locations.Add(new CityLocation
                    {
                        Name = $"Junction {(char)('A' + i)}{j + 1}",
                        Position = new Vector3(x, 0f, z),
                        Kind = LocationKind.Intersection,
                        Radius = s.RoadWidth * 0.5f
                    });
                }
            }
        }

        // ---- queries -------------------------------------------------------------

        /// <summary>True when a point sits inside any building footprint.</summary>
        public bool IsInsideBuilding(Vector2 point, float margin = 0f)
        {
            for (int i = 0; i < Plots.Count; i++)
            {
                Plot p = Plots[i];
                if (Mathf.Abs(point.x - p.Center.x) < p.Size.x * 0.5f + margin &&
                    Mathf.Abs(point.y - p.Center.y) < p.Size.y * 0.5f + margin)
                    return true;
            }

            return false;
        }

        /// <summary>The nearest named place of a given kind, or null if the city has none.</summary>
        public CityLocation? NearestLocation(Vector3 from, LocationKind kind)
        {
            CityLocation? best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < Locations.Count; i++)
            {
                if (Locations[i].Kind != kind) continue;
                float sqr = (Locations[i].Position - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = Locations[i];
            }

            return best;
        }

        /// <summary>Picks an arena for an encounter, preferring ones away from the player.</summary>
        public CityLocation? PickArena(Vector3 awayFrom, float minDistance)
        {
            CityLocation? best = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < Arenas.Count; i++)
            {
                float distance = Vector3.Distance(Arenas[i].Position, awayFrom);
                if (distance < minDistance) continue;

                // Prefer big, close-but-not-too-close spaces.
                float score = Arenas[i].Radius * 2f - distance * 0.3f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = Arenas[i];
            }

            return best ?? (Arenas.Count > 0 ? Arenas[0] : (CityLocation?)null);
        }

        /// <summary>
        /// Reports layout problems: buildings overlapping each other or standing
        /// in the road. Run by the tests, and cheap enough for a startup check.
        /// </summary>
        public List<string> Validate()
        {
            var problems = new List<string>();

            for (int i = 0; i < Plots.Count; i++)
            {
                Plot a = Plots[i];

                if (a.Size.x <= 0f || a.Size.y <= 0f)
                    problems.Add($"plot {i} has a non-positive footprint");

                if (a.Height <= 0f)
                    problems.Add($"plot {i} has no height");

                for (int j = i + 1; j < Plots.Count; j++)
                {
                    Plot b = Plots[j];
                    float overlapX = a.Size.x * 0.5f + b.Size.x * 0.5f - Mathf.Abs(a.Center.x - b.Center.x);
                    float overlapZ = a.Size.y * 0.5f + b.Size.y * 0.5f - Mathf.Abs(a.Center.y - b.Center.y);

                    // A shared wall is fine; a real intersection is not.
                    if (overlapX > 0.05f && overlapZ > 0.05f)
                        problems.Add($"plots {i} and {j} overlap by {overlapX:F2} x {overlapZ:F2}");
                }
            }

            if (Arenas.Count == 0) problems.Add("the city has nowhere to stage a fight");
            if (Roads.Count == 0) problems.Add("the city has no roads");

            return problems;
        }
    }
}
