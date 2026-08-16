using System.Collections.Generic;
using Glowpulse.Core;
using Glowpulse.Core.Rendering;
using Glowpulse.World.Props;
using UnityEngine;

namespace Glowpulse.World.City
{
    /// <summary>
    /// Realises a <see cref="CityLayout"/> as geometry: ground, roads, pavements,
    /// buildings and street dressing.
    ///
    /// The plan is generated first and built second, so this class never makes a
    /// design decision - it only draws one. That split is what lets the layout be
    /// tested without a scene, and what makes the city reproducible from a seed.
    /// </summary>
    public sealed class CityBuilder
    {
        private readonly CityLayout _layout;
        private readonly CitySettings _settings;
        private readonly CityPalette _palette;
        private readonly System.Random _rng;

        public Transform Root { get; private set; }
        public CityGeometry Geometry { get; private set; }

        public CityBuilder(CityLayout layout)
        {
            _layout = layout;
            _settings = layout.Settings;
            _palette = CityPalette.Build();
            _rng = new System.Random(layout.Seed ^ 0x5f3a);
        }

        public Transform Build(Transform parent)
        {
            var root = new GameObject("City");
            root.transform.SetParent(parent, false);
            Root = root.transform;

            Geometry = new CityGeometry(Root, _settings.BatchStaticGeometry);

            BuildGround();
            BuildRoads();
            BuildBuildings();
            BuildStreetDressing();
            BuildPlazas();
            BuildBoundary();

            Geometry.Build();

            GameLayers.SetLayerRecursive(root, GameLayers.Environment);
            RelayerGround();

            Debug.Log($"[City] {_layout.Plots.Count} buildings, {Geometry.PieceCount} pieces welded into " +
                      $"{Geometry.BatchCount} draw batches, {Geometry.ColliderCount} colliders.");
            return Root;
        }

        // ---- ground and roads ---------------------------------------------------

        private void BuildGround()
        {
            float size = _layout.Extent * 2f + 40f;

            var go = MeshLibrary.CreatePart("Ground", Root, MeshLibrary.Cube,
                _palette.Road, new Vector3(0f, -0.5f, 0f), Quaternion.identity,
                new Vector3(size, 1f, size), castShadows: false);
            go.AddComponent<BoxCollider>();
            go.layer = GameLayers.Ground;
            go.name = "Ground";
        }

        private void RelayerGround()
        {
            Transform ground = Root.Find("Ground");
            if (ground != null) GameLayers.SetLayerRecursive(ground.gameObject, GameLayers.Ground);
        }

        private void BuildRoads()
        {
            CitySettings s = _settings;
            float lane = s.CarriagewayWidth;

            foreach (RoadSegment road in _layout.Roads)
            {
                Vector2 delta = road.B - road.A;
                float length = delta.magnitude;
                if (length < 0.1f) continue;

                Vector2 mid = (road.A + road.B) * 0.5f;
                bool alongZ = Mathf.Abs(delta.y) > Mathf.Abs(delta.x);
                var center = new Vector3(mid.x, 0f, mid.y);

                Vector3 carriageway = alongZ
                    ? new Vector3(lane, 0.06f, length)
                    : new Vector3(length, 0.06f, lane);

                Geometry.AddBox(_palette.Road, center + Vector3.up * 0.03f, carriageway);

                // Pavements sit proud of the carriageway, with a kerb face so the
                // step reads from ground level.
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 offset = alongZ
                        ? new Vector3(side * (lane * 0.5f + s.SidewalkWidth * 0.5f), 0f, 0f)
                        : new Vector3(0f, 0f, side * (lane * 0.5f + s.SidewalkWidth * 0.5f));

                    Vector3 walk = alongZ
                        ? new Vector3(s.SidewalkWidth, 0.16f, length)
                        : new Vector3(length, 0.16f, s.SidewalkWidth);

                    Geometry.AddBox(_palette.Sidewalk, center + offset + Vector3.up * 0.08f, walk);

                    Vector3 kerbOffset = alongZ
                        ? new Vector3(side * lane * 0.5f, 0f, 0f)
                        : new Vector3(0f, 0f, side * lane * 0.5f);
                    Vector3 kerb = alongZ
                        ? new Vector3(0.16f, 0.18f, length)
                        : new Vector3(length, 0.18f, 0.16f);

                    Geometry.AddBox(_palette.Kerb, center + kerbOffset + Vector3.up * 0.09f, kerb);
                }

                // Centre line, dashed.
                int dashes = Mathf.Max(1, Mathf.FloorToInt(length / 5f));
                for (int i = 0; i < dashes; i++)
                {
                    float t = (i + 0.5f) / dashes;
                    Vector3 p = Vector3.Lerp(new Vector3(road.A.x, 0f, road.A.y),
                        new Vector3(road.B.x, 0f, road.B.y), t);
                    Vector3 dashSize = alongZ
                        ? new Vector3(0.16f, 0.02f, 2.2f)
                        : new Vector3(2.2f, 0.02f, 0.16f);
                    Geometry.AddBox(_palette.RoadMarking, p + Vector3.up * 0.075f, dashSize);
                }
            }
        }

        // ---- buildings -----------------------------------------------------------

        private void BuildBuildings()
        {
            foreach (Plot plot in _layout.Plots)
                BuildingFactory.Build(Geometry, _palette, plot, _settings);
        }

        // ---- street dressing ------------------------------------------------------

        private void BuildStreetDressing()
        {
            CitySettings s = _settings;
            float lane = s.CarriagewayWidth;
            int lampIndex = 0;

            foreach (RoadSegment road in _layout.Roads)
            {
                Vector2 delta = road.B - road.A;
                float length = delta.magnitude;
                if (length < 4f) continue;

                bool alongZ = Mathf.Abs(delta.y) > Mathf.Abs(delta.x);
                Vector3 a = new Vector3(road.A.x, 0f, road.A.y);
                Vector3 b = new Vector3(road.B.x, 0f, road.B.y);

                int stops = Mathf.Max(1, Mathf.FloorToInt(length / s.LampSpacing));

                for (int i = 0; i <= stops; i++)
                {
                    float t = stops == 0 ? 0.5f : i / (float)stops;
                    Vector3 p = Vector3.Lerp(a, b, t);

                    // Alternate kerbs so lighting staggers down the street.
                    int side = (i % 2 == 0) ? 1 : -1;
                    Vector3 kerbOffset = alongZ
                        ? new Vector3(side * (lane * 0.5f + s.SidewalkWidth * 0.55f), 0f, 0f)
                        : new Vector3(0f, 0f, side * (lane * 0.5f + s.SidewalkWidth * 0.55f));

                    Vector3 pos = p + kerbOffset + Vector3.up * 0.16f;
                    if (TooCloseToJunction(pos)) continue;

                    float yaw = alongZ ? (side > 0 ? 270f : 90f) : (side > 0 ? 180f : 0f);

                    // Only every third lamp carries a real light; the rest are
                    // emissive heads, which looks the same and costs nothing.
                    PropFactory.StreetLamp(Geometry, _palette, pos, yaw, lampIndex % 3 == 0);
                    lampIndex++;

                    if (_rng.NextDouble() > s.StreetFurnitureDensity) continue;
                    PlaceFurniture(pos, yaw, alongZ, side, kerbOffset);
                }

                PlaceParkedCars(a, b, alongZ, lane, length);
            }
        }

        private void PlaceFurniture(Vector3 lampPos, float yaw, bool alongZ, int side,
            Vector3 kerbOffset)
        {
            Vector3 along = alongZ ? Vector3.forward : Vector3.right;
            Vector3 pos = lampPos + along * Mathf.Lerp(3f, 6f, (float)_rng.NextDouble())
                                  * (_rng.NextDouble() < 0.5 ? 1f : -1f);

            if (TooCloseToJunction(pos)) return;

            double roll = _rng.NextDouble();
            int seed = _rng.Next();

            if (roll < 0.3) PropFactory.Bench(Geometry, _palette, pos, yaw);
            else if (roll < 0.52) PropFactory.Bin(Geometry, _palette, pos, yaw);
            else if (roll < 0.74) PropFactory.Tree(Geometry, _palette, pos, seed);
            else if (roll < 0.86) PropFactory.Planter(Geometry, _palette, pos, yaw);
            else if (roll < 0.94) PropFactory.SignPost(Geometry, _palette, pos, yaw, seed);
            else PropFactory.Hydrant(Geometry, _palette, pos);
        }

        private void PlaceParkedCars(Vector3 a, Vector3 b, bool alongZ, float lane, float length)
        {
            int bays = Mathf.FloorToInt(length / 7f);
            for (int i = 0; i < bays; i++)
            {
                if (_rng.NextDouble() > _settings.VehicleDensity) continue;

                float t = (i + 0.5f) / bays;
                Vector3 p = Vector3.Lerp(a, b, t);
                int side = _rng.NextDouble() < 0.5 ? 1 : -1;

                Vector3 offset = alongZ
                    ? new Vector3(side * (lane * 0.5f - 1.3f), 0f, 0f)
                    : new Vector3(0f, 0f, side * (lane * 0.5f - 1.3f));

                Vector3 pos = p + offset;
                if (TooCloseToJunction(pos, 9f)) continue;

                float yaw = alongZ ? (side > 0 ? 0f : 180f) : (side > 0 ? 90f : 270f);
                PropFactory.Car(Geometry, _palette, pos, yaw + Mathf.Lerp(-3f, 3f, (float)_rng.NextDouble()),
                    _rng.Next());
            }
        }

        /// <summary>Junctions are left clear so props never block a crossing.</summary>
        private bool TooCloseToJunction(Vector3 point, float radius = 7.5f)
        {
            float sqr = radius * radius;
            foreach (CityLocation location in _layout.Locations)
            {
                if (location.Kind != LocationKind.Intersection) continue;
                if (MathUtil.FlatSqrDistance(location.Position, point) < sqr) return true;
            }

            return false;
        }

        // ---- squares and courtyards ------------------------------------------------

        private void BuildPlazas()
        {
            foreach (CityLocation location in _layout.Locations)
            {
                switch (location.Kind)
                {
                    case LocationKind.Plaza:
                        BuildPlaza(location);
                        break;
                    case LocationKind.LoadingDock:
                        BuildLoadingDock(location);
                        break;
                    case LocationKind.Alley:
                        BuildAlley(location);
                        break;
                }
            }
        }

        private void BuildPlaza(CityLocation plaza)
        {
            float r = plaza.Radius;
            Vector3 c = plaza.Position;

            Geometry.AddBox(_palette.Sidewalk, c + Vector3.up * 0.09f, new Vector3(r * 2f, 0.18f, r * 2f));

            // A ring of paving and a raised centre give the square a focal point
            // and, more practically, a readable arena floor to fight on.
            Geometry.AddCylinder(_palette.Kerb, c + Vector3.up * 0.22f, r * 0.45f, 0.06f);
            Geometry.AddCylinder(_palette.Concrete, c + Vector3.up * 0.4f, 2.4f, 0.4f);
            Geometry.AddCylinder(_palette.Metal, c + Vector3.up * 1.3f, 0.42f, 0.9f);
            Geometry.AddSphere(_palette.Neon, c + Vector3.up * 2.5f, Vector3.one * 0.9f);
            Geometry.AddCollider(c + Vector3.up * 0.4f, new Vector3(4.8f, 0.8f, 4.8f));

            GameObject go = Geometry.AddLoose("Plaza Light", c + Vector3.up * 3f, Quaternion.identity);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.42f, 0.8f, 0.84f);
            light.intensity = 3.4f;
            light.range = 22f;
            light.shadows = LightShadows.None;

            // Benches around the edge, facing in.
            for (int i = 0; i < 8; i++)
            {
                float angle = i / 8f * Mathf.PI * 2f;
                var pos = c + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (r * 0.62f);
                PropFactory.Bench(Geometry, _palette, pos + Vector3.up * 0.18f,
                    -angle * Mathf.Rad2Deg + 90f);

                if (i % 2 != 0) continue;
                var treePos = c + new Vector3(Mathf.Cos(angle + 0.4f), 0f, Mathf.Sin(angle + 0.4f)) * (r * 0.82f);
                PropFactory.Tree(Geometry, _palette, treePos + Vector3.up * 0.18f, _rng.Next());
            }
        }

        private void BuildLoadingDock(CityLocation dock)
        {
            Vector3 c = dock.Position;
            float r = Mathf.Max(2f, dock.Radius);

            Geometry.AddBox(_palette.Concrete, c + Vector3.up * 0.06f,
                new Vector3(r * 2f, 0.12f, r * 2f));

            int skips = Mathf.Clamp(Mathf.RoundToInt(r / 4f), 1, 3);
            for (int i = 0; i < skips; i++)
            {
                var pos = MathUtil.RandomPointInRing(c, r * 0.25f, r * 0.7f);
                PropFactory.Skip(Geometry, _palette, pos, (float)_rng.NextDouble() * 180f);
            }

            for (int i = 0; i < 3; i++)
                PropFactory.Crates(Geometry, _palette, MathUtil.RandomPointInRing(c, r * 0.3f, r * 0.8f),
                    _rng.Next());
        }

        private void BuildAlley(CityLocation alley)
        {
            Vector3 c = alley.Position;
            float r = Mathf.Max(1.5f, alley.Radius);

            Geometry.AddBox(_palette.Concrete, c + Vector3.up * 0.05f,
                new Vector3(r * 2f, 0.1f, r * 2f));

            int bins = Mathf.Clamp(Mathf.RoundToInt(r / 2.5f), 1, 5);
            for (int i = 0; i < bins; i++)
            {
                var pos = MathUtil.RandomPointInRing(c, r * 0.3f, r * 0.85f);
                if (_rng.NextDouble() < 0.35) PropFactory.Crates(Geometry, _palette, pos, _rng.Next());
                else PropFactory.Bin(Geometry, _palette, pos, (float)_rng.NextDouble() * 180f);
            }
        }

        // ---- boundary ---------------------------------------------------------------

        /// <summary>
        /// A wall of blocks around the slice. It fences the player in without a
        /// visible barrier, and reads as the city carrying on past the edge.
        /// </summary>
        private void BuildBoundary()
        {
            float extent = _layout.Extent + 6f;
            float height = 26f;

            for (int side = 0; side < 4; side++)
            {
                bool alongZ = side % 2 == 1;
                float sign = side < 2 ? 1f : -1f;

                Vector3 center = alongZ
                    ? new Vector3(sign * extent, height * 0.5f, 0f)
                    : new Vector3(0f, height * 0.5f, sign * extent);

                Vector3 size = alongZ
                    ? new Vector3(10f, height, extent * 2f + 20f)
                    : new Vector3(extent * 2f + 20f, height, 10f);

                Geometry.AddBox(_palette.Facade(side * 7), center, size);
                Geometry.AddCollider(center, size);
            }
        }
    }
}
