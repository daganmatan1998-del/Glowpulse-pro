using System.Collections.Generic;
using Glowpulse.Combat;
using Glowpulse.Core;
using UnityEngine;

namespace Glowpulse.World.Npc
{
    /// <summary>
    /// Owns the crowd: how many people are on the street, where they are, how
    /// often each of them is allowed to think, and what they have heard.
    ///
    /// The population is a fixed budget that follows the player rather than a
    /// city full of persistent residents. Nobody can tell the difference - the
    /// street behind you is not being watched - and it means the cost of the crowd
    /// is constant no matter how large the city grows. Civilians that fall behind
    /// are recycled to a pavement ahead with a new seed, so they come back as a
    /// different person.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class CrowdDirector : MonoBehaviour
    {
        private struct Danger
        {
            public Vector3 Position;
            public float Severity;
            public float ExpiresAt;
        }

        private const int MaxDangers = 6;
        private const int MaxSpawnsPerTick = 3;

        [Header("Population")]
        [Tooltip("How many civilians exist at once. A budget, not a city census.")]
        [Range(0, 80)] public int Population = 26;

        [Tooltip("Civilians are placed within this radius of the player.")]
        public float SpawnRadius = 58f;

        [Tooltip("...but never this close, so nobody pops in while being looked at.")]
        public float MinSpawnDistance = 20f;

        [Tooltip("Beyond this the civilian is recycled to somewhere ahead.")]
        public float RecycleRadius = 82f;

        [Header("Detail")]
        [Tooltip("Inside this radius civilians think every frame.")]
        public float FullDetailRadius = 22f;

        [Tooltip("Inside this radius they think a few times a second.")]
        public float ReducedDetailRadius = 46f;

        [Header("Reaction")]
        [Tooltip("How far a commotion travels.")]
        public float AlarmRadius = 26f;

        [Tooltip("How long a single commotion keeps people frightened.")]
        public float AlarmDuration = 6f;

        private static CrowdDirector _instance;

        private readonly List<CivilianBrain> _crowd = new List<CivilianBrain>(48);
        private readonly Danger[] _dangers = new Danger[MaxDangers];

        private PedestrianNetwork _network;
        private Transform _focus;
        private Transform _root;
        private System.Random _rng;

        private float _nextMaintainAt;

        public static CrowdDirector Instance => _instance;

        public int Count => _crowd.Count;

        public PedestrianNetwork Network => _network;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                enabled = false;
                Destroy(this);
                return;
            }

            _instance = this;
            _rng = new System.Random(unchecked(GetInstanceID() * 397));
        }

        private void OnEnable() => CombatFeedback.Commotion += HandleCommotion;

        private void OnDisable() => CombatFeedback.Commotion -= HandleCommotion;

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// Gives the director a city to populate. Until this is called it does
        /// nothing at all, which is what lets the proving-ground world mode skip
        /// the crowd entirely.
        /// </summary>
        public void Configure(PedestrianNetwork network, Transform focus, int seed)
        {
            _network = network;
            _focus = focus;
            _rng = new System.Random(seed);

            if (_root == null)
            {
                _root = new GameObject("Crowd").transform;
                _root.SetParent(transform, false);
            }
        }

        private void Update()
        {
            if (_network == null || _focus == null || _network.NodeCount == 0) return;
            if (Time.time < _nextMaintainAt) return;

            // Maintenance is the only per-crowd work the director does, and it
            // runs a few times a second rather than every frame.
            _nextMaintainAt = Time.time + 0.35f;

            Recycle();
            AssignDetail();
            TopUp();
        }

        // ---- population ----------------------------------------------------------

        private void TopUp()
        {
            int spawned = 0;

            while (_crowd.Count < Population && spawned < MaxSpawnsPerTick)
            {
                int node = PickSpawnNode();
                if (node < 0) return;

                Vector3 position = _network.NodePosition(node);
                var rotation = Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f);

                CivilianBrain brain = CivilianFactory.Create(NextSeed(), position, rotation, _root);
                brain.Configure(_network, node, NextSeed());
                brain.SetDetail(DetailFor(position));
                WarnAboutRememberedDanger(brain);

                _crowd.Add(brain);
                spawned++;
            }
        }

        private void Recycle()
        {
            Vector3 focus = _focus.position;
            float recycleSqr = RecycleRadius * RecycleRadius;

            for (int i = _crowd.Count - 1; i >= 0; i--)
            {
                CivilianBrain brain = _crowd[i];

                if (brain == null)
                {
                    _crowd.RemoveAt(i);
                    continue;
                }

                if (_crowd.Count > Population)
                {
                    // The budget was turned down. Retire the surplus rather than
                    // carrying it around forever.
                    _crowd.RemoveAt(i);
                    Destroy(brain.gameObject);
                    continue;
                }

                if ((brain.transform.position - focus).sqrMagnitude < recycleSqr) continue;

                int node = PickSpawnNode();
                if (node < 0) continue;

                Relocate(brain, node);
            }
        }

        /// <summary>Moves a civilian to a fresh pavement and gives them a new identity.</summary>
        private void Relocate(CivilianBrain brain, int node)
        {
            Vector3 position = _network.NodePosition(node);
            brain.transform.SetPositionAndRotation(position,
                Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f));

            brain.Configure(_network, node, NextSeed());
            brain.SetDetail(DetailFor(position));
            WarnAboutRememberedDanger(brain);
        }

        /// <summary>
        /// A node in the ring around the player: far enough away not to appear in
        /// front of them, close enough to be walking past shortly.
        /// </summary>
        private int PickSpawnNode()
        {
            Vector3 focus = _focus.position;
            float minSqr = MinSpawnDistance * MinSpawnDistance;
            float maxSqr = SpawnRadius * SpawnRadius;

            int count = _network.NodeCount;
            int start = _rng.Next(count);

            // Walk the nodes from a random offset instead of retrying random
            // draws, so a city where few nodes qualify still finds one in bounded
            // time.
            for (int i = 0; i < count; i++)
            {
                int index = (start + i) % count;
                float sqr = (_network.NodePosition(index) - focus).sqrMagnitude;
                if (sqr >= minSqr && sqr <= maxSqr) return index;
            }

            return -1;
        }

        private void AssignDetail()
        {
            for (int i = 0; i < _crowd.Count; i++)
            {
                CivilianBrain brain = _crowd[i];
                if (brain == null) continue;
                brain.SetDetail(DetailFor(brain.transform.position));
            }
        }

        private CivilianDetail DetailFor(Vector3 position)
        {
            float sqr = (position - _focus.position).sqrMagnitude;

            if (sqr <= FullDetailRadius * FullDetailRadius) return CivilianDetail.Full;
            if (sqr <= ReducedDetailRadius * ReducedDetailRadius) return CivilianDetail.Reduced;
            return CivilianDetail.Distant;
        }

        /// <summary>
        /// Every civilian's look and personality comes from one of these. Drawing
        /// them from the director's own generator keeps a given city seed producing
        /// the same crowd, which matters when a bug only shows up on one of them.
        /// </summary>
        private int NextSeed() => _rng.Next(1, int.MaxValue);

        // ---- danger --------------------------------------------------------------

        /// <summary>
        /// Something alarming happened here. Safe to call when no crowd exists, so
        /// combat code can report trouble without caring whether the world it is
        /// fighting in has civilians in it.
        /// </summary>
        public static void ReportDanger(Vector3 position, float severity = 1f)
        {
            _instance?.Raise(position, severity);
        }

        private void HandleCommotion(Vector3 position, float severity)
        {
            Raise(position, severity);
        }

        private void Raise(Vector3 position, float severity)
        {
            if (_crowd.Count == 0) return;

            Remember(position, severity);

            float radiusSqr = AlarmRadius * AlarmRadius;

            for (int i = 0; i < _crowd.Count; i++)
            {
                CivilianBrain brain = _crowd[i];
                if (brain == null) continue;
                if ((brain.transform.position - position).sqrMagnitude > radiusSqr) continue;

                brain.Alarm(position, severity, AlarmDuration);
            }
        }

        /// <summary>
        /// Keeps a small history of trouble, so a civilian recycled into the middle
        /// of an ongoing fight knows about it instead of strolling in cheerfully.
        /// </summary>
        private void Remember(Vector3 position, float severity)
        {
            int slot = 0;
            float oldest = float.MaxValue;

            for (int i = 0; i < MaxDangers; i++)
            {
                if (_dangers[i].ExpiresAt <= Time.time)
                {
                    slot = i;
                    break;
                }

                // Merge into a nearby danger rather than filling the buffer with
                // every punch of the same fight.
                if ((_dangers[i].Position - position).sqrMagnitude < 36f)
                {
                    _dangers[i].Position = Vector3.Lerp(_dangers[i].Position, position, 0.5f);
                    _dangers[i].Severity = Mathf.Max(_dangers[i].Severity, severity);
                    _dangers[i].ExpiresAt = Time.time + AlarmDuration;
                    return;
                }

                if (_dangers[i].ExpiresAt >= oldest) continue;
                oldest = _dangers[i].ExpiresAt;
                slot = i;
            }

            _dangers[slot] = new Danger
            {
                Position = position,
                Severity = severity,
                ExpiresAt = Time.time + AlarmDuration
            };
        }

        /// <summary>Warns a freshly placed civilian about trouble already in progress.</summary>
        private void WarnAboutRememberedDanger(CivilianBrain brain)
        {
            float radiusSqr = AlarmRadius * AlarmRadius;

            for (int i = 0; i < MaxDangers; i++)
            {
                if (_dangers[i].ExpiresAt <= Time.time) continue;
                if ((_dangers[i].Position - brain.transform.position).sqrMagnitude > radiusSqr) continue;

                brain.Alarm(_dangers[i].Position, _dangers[i].Severity,
                    _dangers[i].ExpiresAt - Time.time);
                return;
            }
        }

        /// <summary>Removes every civilian. Used when the world is torn down or rebuilt.</summary>
        public void Clear()
        {
            for (int i = 0; i < _crowd.Count; i++)
                if (_crowd[i] != null) Destroy(_crowd[i].gameObject);

            _crowd.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }
}
