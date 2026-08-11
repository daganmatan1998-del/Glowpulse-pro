using Glowpulse.Audio;
using Glowpulse.CameraSystem;
using Glowpulse.Combat;
using Glowpulse.Core;
using Glowpulse.Core.Characters;
using Glowpulse.VFX;
using UnityEngine;

namespace Glowpulse.Enemies
{
    /// <summary>
    /// Phases for a boss fight.
    ///
    /// A boss with more health is not a boss, it is a longer fight. What makes the
    /// last encounter different is that it changes as it loses: it commits
    /// faster, it stops respecting spacing, and it tells you it has done so. The
    /// phase is driven off health rather than a timer, so the change always lands
    /// on something the player did.
    ///
    /// Phases retune the existing archetype rather than swapping in a new enemy,
    /// which keeps the damage already dealt and means one boss can carry any
    /// number of phases without a second archetype per phase.
    /// </summary>
    [RequireComponent(typeof(EnemyBrain))]
    [RequireComponent(typeof(EnemyCombatant))]
    public sealed class BossBrain : MonoBehaviour
    {
        /// <summary>How a phase bends the base numbers.</summary>
        private readonly struct Phase
        {
            /// <summary>Health fraction at or below which this phase begins.</summary>
            public readonly float Threshold;

            public readonly float Cooldown;
            public readonly float Speed;
            public readonly float Damage;
            public readonly float Poise;
            public readonly string Announcement;

            public Phase(float threshold, float cooldown, float speed, float damage, float poise,
                string announcement)
            {
                Threshold = threshold;
                Cooldown = cooldown;
                Speed = speed;
                Damage = damage;
                Poise = poise;
                Announcement = announcement;
            }
        }

        // Phase one is the archetype as authored. Each later phase shortens the
        // gaps between attacks far more than it raises damage, because pressure
        // is what actually reads as an enemy getting angry.
        private static readonly Phase[] Phases =
        {
            new Phase(1f, 1f, 1f, 1f, 1f, null),
            new Phase(0.65f, 0.76f, 1.12f, 1.1f, 1.15f, "ENRAGED"),
            new Phase(0.3f, 0.55f, 1.24f, 1.2f, 1.35f, "LAST STAND")
        };

        [SerializeField] private bool _announce = true;

        private EnemyBrain _brain;
        private EnemyCombatant _combatant;
        private EnemyArchetype _base;
        private Health _health;

        private int _phase;

        /// <summary>Zero-based phase index. Rises, never falls.</summary>
        public int Phase_ => _phase;

        public int PhaseCount => Phases.Length;

        /// <summary>Raised when the boss enters a new phase, with the phase index.</summary>
        public event System.Action<int> PhaseChanged;

        private void Awake()
        {
            _brain = GetComponent<EnemyBrain>();
            _combatant = GetComponent<EnemyCombatant>();
            _health = GetComponent<Health>();
        }

        /// <summary>
        /// Remembers the numbers phase one was built from. Every later phase is a
        /// multiple of these, so phases never compound off each other.
        /// </summary>
        public void Bind(EnemyArchetype baseArchetype)
        {
            _base = baseArchetype;
            _phase = 0;
        }

        private void Update()
        {
            if (_base == null || _health == null || !_health.IsAlive) return;

            float fraction = _health.Normalized;

            // Walk forward only. A boss healed by anything must not slide back to
            // a gentler phase and undo the drama.
            while (_phase + 1 < Phases.Length && fraction <= Phases[_phase + 1].Threshold)
                EnterPhase(_phase + 1);
        }

        private void EnterPhase(int index)
        {
            _phase = index;
            Phase phase = Phases[index];

            EnemyArchetype tuned = _base.Clone();
            tuned.AttackCooldown = _base.AttackCooldown * phase.Cooldown;
            tuned.AttackCooldownVariance = _base.AttackCooldownVariance * phase.Cooldown;
            tuned.ReactionTime = _base.ReactionTime * phase.Cooldown;
            tuned.ChaseSpeed = _base.ChaseSpeed * phase.Speed;
            tuned.StrafeSpeed = _base.StrafeSpeed * phase.Speed;
            tuned.WalkSpeed = _base.WalkSpeed * phase.Speed;
            tuned.DamageMultiplier = _base.DamageMultiplier * phase.Damage;
            tuned.Poise = _base.Poise * phase.Poise;

            // A boss cornered stops retreating - backing off at 20% health reads
            // as the fight fizzling out rather than climaxing.
            tuned.RetreatChance = _base.RetreatChance * Mathf.Max(0f, 1f - index * 0.5f);

            _combatant.Retune(tuned);
            _brain.Retune(tuned);

            AnnouncePhase(phase);
            PhaseChanged?.Invoke(index);
        }

        /// <summary>
        /// The moment of the change, made loud. Without a beat here the phase is
        /// invisible: the numbers move but the player only feels a vague sense
        /// that things got worse.
        /// </summary>
        private void AnnouncePhase(in Phase phase)
        {
            if (!_announce || string.IsNullOrEmpty(phase.Announcement)) return;

            Vector3 point = transform.position + Vector3.up * 1.2f;

            CameraShaker.Shake(0.5f);
            ImpactEffects.Play(ImpactVisual.Slam, point, Vector3.up, 1.6f);
            AudioManager.PlayAt(Sfx.GuardBreak, point, 1f, 0.05f);

            // Poise is refilled on the turn, so the phase change itself cannot be
            // interrupted by whatever the player was in the middle of.
            _combatant.RefillPoise();
            _brain.Animator?.PlayAction(PoseLibrary.HitHeavy, 0.7f);

            // The street should scatter when the boss turns up the heat.
            CombatFeedback.RaiseCommotion(point, 1f);
        }
    }
}
