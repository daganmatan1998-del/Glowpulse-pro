using System.Collections.Generic;
using Glowpulse.World.City;
using UnityEngine;

namespace Glowpulse.World.Npc
{
    /// <summary>
    /// The pavement graph civilians walk on, derived from the city layout.
    ///
    /// Crowds do not need pathfinding. What they need is somewhere plausible to
    /// be going, and a graph laid over the pavements gives that for free: a
    /// civilian walks to the next node, picks another, and keeps going. There is
    /// no path to allocate, nothing to recompute when the crowd moves, and the
    /// result is people who follow the kerb instead of cutting across a building.
    ///
    /// Like <see cref="CityLayout"/> this is pure data with no GameObjects in it,
    /// so its connectivity can be checked in a test rather than by watching
    /// somebody walk into a wall.
    /// </summary>
    public sealed class PedestrianNetwork
    {
        /// <summary>A point on the pavement worth walking to.</summary>
        public struct Node
        {
            public Vector3 Position;

            /// <summary>Index of this node's first link in <see cref="Links"/>.</summary>
            public int FirstLink;

            public int LinkCount;
        }

        /// <summary>A walkable connection between two nodes.</summary>
        public struct Link
        {
            public int To;
            public float Length;

            /// <summary>
            /// True when the link steps off the kerb and over a carriageway.
            /// Civilians pause at these, which is most of what makes a crowd look
            /// like it is obeying a street rather than drifting through one.
            /// </summary>
            public bool IsCrossing;
        }

        private readonly List<Node> _nodes = new List<Node>(96);
        private readonly List<Link> _links = new List<Link>(384);

        public IReadOnlyList<Node> Nodes => _nodes;
        public IReadOnlyList<Link> Links => _links;

        public int NodeCount => _nodes.Count;
        public int LinkCount => _links.Count;

        public Vector3 NodePosition(int index) => _nodes[index].Position;

        /// <summary>
        /// Lays a lane grid over the pavements. Each road contributes two walking
        /// lanes, one per pavement, and nodes sit wherever lanes cross - which is
        /// exactly where a real pavement corner is.
        /// </summary>
        public static PedestrianNetwork Build(CityLayout layout)
        {
            var network = new PedestrianNetwork();
            if (layout == null) return network;

            CitySettings s = layout.Settings;
            float offset = s.CarriagewayWidth * 0.5f + s.SidewalkWidth * 0.5f;

            List<float> roadX = CentreLines(layout, vertical: true);
            List<float> roadZ = CentreLines(layout, vertical: false);

            List<float> laneX = Lanes(roadX, offset);
            List<float> laneZ = Lanes(roadZ, offset);

            network.BuildGrid(layout, laneX, laneZ, roadX, roadZ);
            return network;
        }

        /// <summary>Distinct centreline coordinates of the roads running one way.</summary>
        private static List<float> CentreLines(CityLayout layout, bool vertical)
        {
            var values = new List<float>(8);

            for (int i = 0; i < layout.Roads.Count; i++)
            {
                RoadSegment road = layout.Roads[i];
                if (road.IsAlley) continue;

                bool isVertical = Mathf.Abs(road.A.x - road.B.x) < 0.01f;
                if (isVertical != vertical) continue;

                float value = isVertical ? road.A.x : road.A.y;
                if (!Contains(values, value)) values.Add(value);
            }

            values.Sort();
            return values;
        }

        private static bool Contains(List<float> values, float value)
        {
            for (int i = 0; i < values.Count; i++)
                if (Mathf.Abs(values[i] - value) < 0.05f) return true;
            return false;
        }

        private static List<float> Lanes(List<float> centres, float offset)
        {
            var lanes = new List<float>(centres.Count * 2);
            for (int i = 0; i < centres.Count; i++)
            {
                lanes.Add(centres[i] - offset);
                lanes.Add(centres[i] + offset);
            }

            lanes.Sort();
            return lanes;
        }

        private void BuildGrid(CityLayout layout, List<float> laneX, List<float> laneZ,
            List<float> roadX, List<float> roadZ)
        {
            int cols = laneX.Count;
            int rows = laneZ.Count;
            if (cols < 2 || rows < 2) return;

            // Grid slot to node index, or -1 where the slot was rejected.
            var slot = new int[cols * rows];
            var positions = new List<Vector3>(cols * rows);

            for (int ix = 0; ix < cols; ix++)
            {
                for (int iz = 0; iz < rows; iz++)
                {
                    var point = new Vector2(laneX[ix], laneZ[iz]);

                    // A lane should never run through a building, but a plot that
                    // reaches the kerb would put one there. Drop the node rather
                    // than send somebody to stand inside a wall.
                    if (layout.IsInsideBuilding(point, 0.3f))
                    {
                        slot[ix * rows + iz] = -1;
                        continue;
                    }

                    slot[ix * rows + iz] = positions.Count;
                    positions.Add(new Vector3(point.x, 0f, point.y));
                }
            }

            // Adjacency is gathered per node first, then flattened, so the runtime
            // walks one contiguous array instead of a list of lists.
            var adjacency = new List<Link>[positions.Count];
            for (int i = 0; i < adjacency.Length; i++) adjacency[i] = new List<Link>(4);

            for (int ix = 0; ix < cols; ix++)
            {
                for (int iz = 0; iz < rows; iz++)
                {
                    int here = slot[ix * rows + iz];
                    if (here < 0) continue;

                    if (ix + 1 < cols)
                    {
                        int east = slot[(ix + 1) * rows + iz];
                        if (east >= 0)
                            Connect(adjacency, positions, here, east,
                                Straddles(roadX, laneX[ix], laneX[ix + 1]));
                    }

                    if (iz + 1 < rows)
                    {
                        int north = slot[ix * rows + iz + 1];
                        if (north >= 0)
                            Connect(adjacency, positions, here, north,
                                Straddles(roadZ, laneZ[iz], laneZ[iz + 1]));
                    }
                }
            }

            for (int i = 0; i < positions.Count; i++)
            {
                _nodes.Add(new Node
                {
                    Position = positions[i],
                    FirstLink = _links.Count,
                    LinkCount = adjacency[i].Count
                });

                _links.AddRange(adjacency[i]);
            }
        }

        private static void Connect(List<Link>[] adjacency, List<Vector3> positions, int a, int b,
            bool crossing)
        {
            float length = Vector3.Distance(positions[a], positions[b]);
            adjacency[a].Add(new Link { To = b, Length = length, IsCrossing = crossing });
            adjacency[b].Add(new Link { To = a, Length = length, IsCrossing = crossing });
        }

        /// <summary>True when the span from a to b passes over a road centreline.</summary>
        private static bool Straddles(List<float> centres, float a, float b)
        {
            float min = Mathf.Min(a, b);
            float max = Mathf.Max(a, b);

            for (int i = 0; i < centres.Count; i++)
                if (centres[i] > min + 0.01f && centres[i] < max - 0.01f) return true;

            return false;
        }

        // ---- queries -------------------------------------------------------------

        public Link LinkAt(in Node node, int offset) => _links[node.FirstLink + offset];

        /// <summary>The node closest to a point, or -1 when the network is empty.</summary>
        public int Nearest(Vector3 position)
        {
            int best = -1;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < _nodes.Count; i++)
            {
                float sqr = (_nodes[i].Position - position).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = i;
            }

            return best;
        }

        /// <summary>
        /// Chooses where to walk next. Carrying straight on is weighted heavily and
        /// doubling back is weighted almost to nothing, so a walk down a street
        /// looks like someone going somewhere rather than a random stumble.
        /// </summary>
        public int NextStep(int from, int cameFrom, Vector3 heading, System.Random rng)
        {
            if (from < 0 || from >= _nodes.Count) return -1;

            Node node = _nodes[from];
            if (node.LinkCount == 0) return -1;

            Vector3 forward = heading.sqrMagnitude > 0.0001f ? heading.normalized : Vector3.zero;

            float total = 0f;
            float best = -1f;
            int bestIndex = -1;

            // Two passes: accumulate weights, then draw. Cheaper than allocating a
            // scratch array for every step every civilian takes.
            for (int pass = 0; pass < 2; pass++)
            {
                float draw = pass == 1 ? (float)rng.NextDouble() * total : 0f;
                float running = 0f;

                for (int i = 0; i < node.LinkCount; i++)
                {
                    Link link = _links[node.FirstLink + i];
                    float weight = Weight(node, link, cameFrom, forward);

                    if (pass == 0)
                    {
                        total += weight;
                        if (weight > best)
                        {
                            best = weight;
                            bestIndex = link.To;
                        }

                        continue;
                    }

                    running += weight;
                    if (draw <= running) return link.To;
                }

                // Everything scored zero, which only happens at a dead end.
                if (pass == 0 && total <= 0f) return bestIndex;
            }

            return bestIndex;
        }

        private float Weight(in Node node, in Link link, int cameFrom, Vector3 forward)
        {
            if (link.To == cameFrom) return 0.06f;

            float weight = link.IsCrossing ? 0.45f : 1f;

            if (forward.sqrMagnitude < 0.0001f) return weight;

            Vector3 direction = _nodes[link.To].Position - node.Position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return weight;

            float alignment = Vector3.Dot(direction.normalized, forward);
            return weight * Mathf.Lerp(0.25f, 2.4f, Mathf.InverseLerp(-1f, 1f, alignment));
        }

        /// <summary>
        /// The neighbour that puts the most distance between the walker and a
        /// threat. This is the whole of a civilian's escape logic: run to the next
        /// corner away from the trouble, then ask again.
        /// </summary>
        public int StepAwayFrom(int from, Vector3 threat)
        {
            if (from < 0 || from >= _nodes.Count) return -1;

            Node node = _nodes[from];
            float hereSqr = (node.Position - threat).sqrMagnitude;

            int best = -1;
            float bestGain = 0f;

            for (int i = 0; i < node.LinkCount; i++)
            {
                Link link = _links[node.FirstLink + i];

                // Fleeing across a road is exactly what a frightened person does,
                // so crossings are not penalised here the way they are when
                // strolling - but they are still not preferred.
                float gain = (_nodes[link.To].Position - threat).sqrMagnitude - hereSqr;
                if (link.IsCrossing) gain *= 0.8f;

                if (gain <= bestGain) continue;
                bestGain = gain;
                best = link.To;
            }

            return best;
        }

        /// <summary>
        /// Reports connectivity problems. A stranded node is worse than it sounds:
        /// a civilian that walks onto one has nowhere to go and stands there
        /// forever, which reads as a broken game.
        /// </summary>
        public List<string> Validate()
        {
            var problems = new List<string>();

            if (_nodes.Count == 0)
            {
                problems.Add("the pedestrian network has no nodes");
                return problems;
            }

            for (int i = 0; i < _nodes.Count; i++)
                if (_nodes[i].LinkCount == 0)
                    problems.Add($"node {i} is stranded with no links");

            int reached = CountReachable(0);
            if (reached != _nodes.Count)
                problems.Add($"the network is in more than one piece: " +
                             $"{reached} of {_nodes.Count} nodes reachable");

            return problems;
        }

        private int CountReachable(int start)
        {
            var seen = new bool[_nodes.Count];
            var queue = new Queue<int>();

            seen[start] = true;
            queue.Enqueue(start);
            int count = 1;

            while (queue.Count > 0)
            {
                Node node = _nodes[queue.Dequeue()];
                for (int i = 0; i < node.LinkCount; i++)
                {
                    int to = _links[node.FirstLink + i].To;
                    if (seen[to]) continue;
                    seen[to] = true;
                    count++;
                    queue.Enqueue(to);
                }
            }

            return count;
        }
    }
}
