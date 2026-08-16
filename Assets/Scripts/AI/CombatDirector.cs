using System.Collections.Generic;
using Glowpulse.Core;
using UnityEngine;

namespace Glowpulse.AI
{
    /// <summary>Anything the director can schedule. Implemented by the enemy brain.</summary>
    public interface ICombatParticipant
    {
        Transform Transform { get; }
        bool IsAlive { get; }

        /// <summary>True while actively fighting a target, rather than idling or patrolling.</summary>
        bool IsEngaged { get; }

        /// <summary>How much this participant wants to attack right now, 0..1.</summary>
        float AttackUrgency { get; }

        /// <summary>0 is straight in front of the player, 1 is directly behind.</summary>
        float FlankPreference { get; }
    }

    /// <summary>
    /// Choreographs a fight so it reads instead of turning into a pile-on.
    ///
    /// Two jobs. First, it hands out a limited number of attack tokens, so only
    /// one or two enemies swing at a time while the rest circle - the single
    /// biggest difference between a crowd that feels designed and a crowd that
    /// feels unfair. Second, it assigns each enemy a slot on a ring around the
    /// player, so they spread out and surround rather than stacking on the same
    /// spot.
    /// </summary>
    [DefaultExecutionOrder(-70)]
    public sealed class CombatDirector : MonoBehaviour
    {
        [Header("Attack pacing")]
        [Tooltip("How many enemies may be mid-attack at the same time.")]
        [SerializeField] private int _maxSimultaneousAttackers = 2;

        [Tooltip("Minimum seconds between two attacks being authorised, so blows do not land together.")]
        [SerializeField] private float _attackSpacing = 0.45f;

        [Tooltip("Seconds a token is held before it is reclaimed, in case an attacker is interrupted.")]
        [SerializeField] private float _tokenTimeout = 3.5f;

        [Header("Encirclement")]
        [Tooltip("Number of standing positions around the target.")]
        [SerializeField] private int _slotCount = 8;

        [Tooltip("Radius of the ring enemies hold while waiting their turn.")]
        [SerializeField] private float _slotRadius = 3.1f;

        [Tooltip("Seconds between recomputing who stands where.")]
        [SerializeField] private float _slotRefreshInterval = 0.9f;

        private readonly List<ICombatParticipant> _participants = new List<ICombatParticipant>(16);
        private readonly Dictionary<ICombatParticipant, int> _slots =
            new Dictionary<ICombatParticipant, int>(16);
        private readonly List<Token> _tokens = new List<Token>(4);
        private bool[] _slotTaken;

        private float _nextTokenAt;
        private float _nextSlotRefreshAt;
        private Transform _focus;

        private struct Token
        {
            public ICombatParticipant Holder;
            public float ExpiresAt;
        }

        private static CombatDirector _instance;

        public static CombatDirector Instance => _instance;

        /// <summary>The character the encounter is arranged around, normally the player.</summary>
        public Transform Focus
        {
            get => _focus;
            set => _focus = value;
        }

        public int ParticipantCount => _participants.Count;
        public int ActiveAttackers => _tokens.Count;

        public int MaxSimultaneousAttackers
        {
            get => _maxSimultaneousAttackers;
            set => _maxSimultaneousAttackers = Mathf.Max(1, value);
        }

        public static CombatDirector Install(GameObject host)
        {
            if (_instance != null) return _instance;
            _instance = host.GetComponent<CombatDirector>() ?? host.AddComponent<CombatDirector>();
            return _instance;
        }

        private void Awake()
        {
            // Allocate before the duplicate check. Destroy is deferred to the end
            // of the frame, so a duplicate still receives one Update - and with a
            // null slot array that Update would throw.
            _slotTaken = new bool[Mathf.Max(2, _slotCount)];

            if (_instance != null && _instance != this)
            {
                enabled = false;
                Destroy(this);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ---- registration ---------------------------------------------------------

        public void Register(ICombatParticipant participant)
        {
            if (participant == null || _participants.Contains(participant)) return;
            _participants.Add(participant);
            _nextSlotRefreshAt = 0f;
        }

        public void Unregister(ICombatParticipant participant)
        {
            if (participant == null) return;
            _participants.Remove(participant);
            ReleaseToken(participant);

            if (_slots.TryGetValue(participant, out int slot))
            {
                if (slot >= 0 && slot < _slotTaken.Length) _slotTaken[slot] = false;
                _slots.Remove(participant);
            }
        }

        // ---- attack tokens ------------------------------------------------------------

        /// <summary>
        /// Asks permission to attack. Granted only if a slot is free and enough
        /// time has passed since the last authorisation.
        /// </summary>
        public bool RequestAttack(ICombatParticipant participant)
        {
            if (participant == null || !participant.IsAlive) return false;
            if (HoldsToken(participant)) return true;

            if (_tokens.Count >= _maxSimultaneousAttackers) return false;
            if (Time.time < _nextTokenAt) return false;

            _tokens.Add(new Token { Holder = participant, ExpiresAt = Time.time + _tokenTimeout });
            _nextTokenAt = Time.time + _attackSpacing;
            return true;
        }

        public void ReleaseToken(ICombatParticipant participant)
        {
            for (int i = _tokens.Count - 1; i >= 0; i--)
            {
                if (_tokens[i].Holder != participant) continue;
                _tokens.RemoveAt(i);
                return;
            }
        }

        public bool HoldsToken(ICombatParticipant participant)
        {
            for (int i = 0; i < _tokens.Count; i++)
                if (_tokens[i].Holder == participant) return true;
            return false;
        }

        // ---- encirclement ---------------------------------------------------------------

        /// <summary>
        /// World position this participant should hold while waiting its turn.
        /// Falls back to a point near the target if no slot has been assigned.
        /// </summary>
        public Vector3 GetSlotPosition(ICombatParticipant participant, float radiusOverride = -1f)
        {
            Transform focus = _focus;
            if (focus == null) return participant.Transform.position;

            float radius = radiusOverride > 0f ? radiusOverride : _slotRadius;

            if (!_slots.TryGetValue(participant, out int slot))
                return focus.position - MathUtil.FlatDirection(
                    focus.position - participant.Transform.position) * radius;

            float angle = slot / (float)_slotTaken.Length * Mathf.PI * 2f;

            // Slots are laid out relative to the target's facing, so "behind the
            // player" stays behind the player as they turn.
            Vector3 offset = focus.rotation * new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            return focus.position + offset * radius;
        }

        /// <summary>The slot index assigned to a participant, or -1.</summary>
        public int GetSlot(ICombatParticipant participant)
        {
            return _slots.TryGetValue(participant, out int slot) ? slot : -1;
        }

        private void Update()
        {
            ExpireTokens();

            if (Time.time < _nextSlotRefreshAt) return;
            _nextSlotRefreshAt = Time.time + _slotRefreshInterval;
            AssignSlots();
        }

        private void ExpireTokens()
        {
            for (int i = _tokens.Count - 1; i >= 0; i--)
            {
                Token token = _tokens[i];
                bool stale = token.Holder == null || !token.Holder.IsAlive
                             || !token.Holder.IsEngaged || Time.time >= token.ExpiresAt;
                if (stale) _tokens.RemoveAt(i);
            }
        }

        /// <summary>
        /// Assigns each engaged enemy the free slot closest to where it already
        /// stands, biased by how much it likes to flank. Greedy and stable: an
        /// enemy keeps its slot unless something better opens up, which stops the
        /// crowd from constantly reshuffling.
        /// </summary>
        private void AssignSlots()
        {
            Transform focus = _focus;
            if (focus == null) return;

            for (int i = 0; i < _slotTaken.Length; i++) _slotTaken[i] = false;
            _slots.Clear();

            // Closest first, so the enemies already in the player's face get the
            // frontal slots and the stragglers fill in around the back.
            _participants.Sort(CompareByDistanceToFocus);

            for (int i = 0; i < _participants.Count; i++)
            {
                ICombatParticipant participant = _participants[i];
                if (participant == null || !participant.IsAlive || !participant.IsEngaged) continue;

                int best = -1;
                float bestScore = float.MaxValue;

                Vector3 toParticipant = MathUtil.FlatDirection(
                    participant.Transform.position - focus.position);
                if (toParticipant.sqrMagnitude < 0.0001f) toParticipant = -focus.forward;

                for (int s = 0; s < _slotTaken.Length; s++)
                {
                    if (_slotTaken[s]) continue;

                    float angle = s / (float)_slotTaken.Length * Mathf.PI * 2f;
                    Vector3 slotDirection = focus.rotation *
                                            new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));

                    // Cost is how far the enemy would have to walk around the ring,
                    // minus a bonus for slots behind the player if it likes to flank.
                    float travel = 1f - Vector3.Dot(toParticipant, slotDirection);
                    float behindness = 1f - Mathf.Clamp01(Vector3.Dot(focus.forward, slotDirection) * 0.5f + 0.5f);
                    float score = travel - behindness * participant.FlankPreference * 1.2f;

                    if (score >= bestScore) continue;
                    bestScore = score;
                    best = s;
                }

                if (best < 0) continue;
                _slotTaken[best] = true;
                _slots[participant] = best;
            }
        }

        private int CompareByDistanceToFocus(ICombatParticipant a, ICombatParticipant b)
        {
            if (a == null || a.Transform == null) return 1;
            if (b == null || b.Transform == null) return -1;

            Vector3 origin = _focus != null ? _focus.position : Vector3.zero;
            float da = MathUtil.FlatSqrDistance(a.Transform.position, origin);
            float db = MathUtil.FlatSqrDistance(b.Transform.position, origin);
            return da.CompareTo(db);
        }

        /// <summary>Alerts every registered enemy within a radius, e.g. when a fight starts.</summary>
        public void RaiseAlarm(Vector3 position, float radius)
        {
            float sqr = radius * radius;
            for (int i = 0; i < _participants.Count; i++)
            {
                ICombatParticipant participant = _participants[i];
                if (participant == null || !participant.IsAlive) continue;
                if (MathUtil.FlatSqrDistance(participant.Transform.position, position) > sqr) continue;
                (participant as IAlertable)?.Alert(position);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }

    /// <summary>Something that can be told about trouble it did not see itself.</summary>
    public interface IAlertable
    {
        void Alert(Vector3 position);
    }
}
