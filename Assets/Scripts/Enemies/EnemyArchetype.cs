using Glowpulse.Combat;
using Glowpulse.Core.Characters;
using Glowpulse.Core.Settings;
using UnityEngine;

namespace Glowpulse.Enemies
{
    /// <summary>The three enemy roles. Encounters mix them deliberately.</summary>
    public enum EnemyKind
    {
        /// <summary>Balanced: walks in, trades, occasionally blocks. The baseline.</summary>
        Brawler = 0,

        /// <summary>Slow, tough, hits enormously hard and shrugs off light hits.</summary>
        Bruiser = 1,

        /// <summary>Fast and evasive: flanks, jabs, backs off. Pressures from behind.</summary>
        Runner = 2
    }

    /// <summary>
    /// Everything that makes one enemy type feel different from another. Kept as
    /// plain data with named presets, so adding a variant is one method rather
    /// than a new class hierarchy.
    /// </summary>
    public sealed class EnemyArchetype
    {
        public EnemyKind Kind;
        public string DisplayName;
        public CharacterStyle Style;

        [Header("Stats")]
        public float Health;
        public float Stamina;

        /// <summary>
        /// Damage absorbed before a stagger lands. High poise is what lets a
        /// bruiser walk through jabs, and it is the reason light attacks alone
        /// cannot lock one down.
        /// </summary>
        public float Poise;

        public float PoiseRegenPerSecond;

        /// <summary>Fraction of incoming damage ignored.</summary>
        public float DamageResistance;

        [Header("Movement")]
        public float WalkSpeed;
        public float ChaseSpeed;
        public float StrafeSpeed;
        public float TurnSpeed;
        public float Acceleration;

        [Header("Perception")]
        public float SightRange;
        public float SightHalfAngle;

        /// <summary>Radius at which combat noise alerts this enemy even out of sight.</summary>
        public float HearingRange;

        /// <summary>Seconds between noticing the player and committing to a chase.</summary>
        public float ReactionTime;

        [Header("Combat")]
        /// <summary>Distance it tries to hold while circling, waiting for an opening.</summary>
        public float PreferredRange;

        /// <summary>Distance at which it will commit to an attack.</summary>
        public float AttackRange;

        /// <summary>Seconds between attacks once it holds the initiative.</summary>
        public float AttackCooldown;

        public float AttackCooldownVariance;

        /// <summary>0..1 chance of raising a guard instead of pressing an attack.</summary>
        [Range(0f, 1f)] public float BlockChance;

        /// <summary>0..1 chance of hopping back after being hit.</summary>
        [Range(0f, 1f)] public float RetreatChance;

        /// <summary>How strongly it prefers a flanking slot over a frontal one.</summary>
        [Range(0f, 1f)] public float FlankPreference;

        /// <summary>
        /// Scales the damage every attack from this enemy deals. One on the
        /// presets; the difficulty setting is what moves it, so raising a
        /// difficulty never means editing a move table.
        /// </summary>
        public float DamageMultiplier = 1f;

        [Header("Rewards")]
        public int ExperienceReward;
        public int MoneyReward;

        /// <summary>The moves this enemy can throw.</summary>
        public MoveSet Moves;

        public float RollAttackCooldown()
        {
            return Mathf.Max(0.15f, AttackCooldown + Random.Range(-AttackCooldownVariance, AttackCooldownVariance));
        }

        /// <summary>
        /// A copy of this archetype bent by a difficulty profile.
        ///
        /// A copy, not an edit: the presets are cached singletons, so scaling one
        /// in place would compound every time an enemy spawned and a Hard run
        /// would drift into absurdity within a minute. Returning a new instance
        /// also means the preset stays the honest record of the Normal balance.
        /// </summary>
        public EnemyArchetype Scaled(in DifficultyProfile profile)
        {
            EnemyArchetype copy = Clone();

            copy.Health *= Mathf.Max(0.1f, profile.EnemyHealth);
            copy.DamageMultiplier *= Mathf.Max(0.05f, profile.EnemyDamage);

            copy.AttackCooldown *= Mathf.Max(0.1f, profile.EnemyCooldown);
            copy.AttackCooldownVariance *= Mathf.Max(0.1f, profile.EnemyCooldown);
            copy.ReactionTime *= Mathf.Max(0.1f, profile.EnemyReaction);

            return copy;
        }

        public EnemyArchetype Clone() => (EnemyArchetype)MemberwiseClone();

        // ---- presets ----------------------------------------------------------

        private static EnemyArchetype _brawler, _bruiser, _runner;

        public static EnemyArchetype Get(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Bruiser: return Bruiser();
                case EnemyKind.Runner: return Runner();
                default: return Brawler();
            }
        }

        public static EnemyArchetype Brawler()
        {
            if (_brawler != null) return _brawler;

            _brawler = new EnemyArchetype
            {
                Kind = EnemyKind.Brawler,
                DisplayName = "Thug",
                Style = CharacterStyle.Thug(),
                Health = 90f,
                Stamina = 70f,
                Poise = 28f,
                PoiseRegenPerSecond = 9f,
                DamageResistance = 0f,
                WalkSpeed = 2.0f,
                ChaseSpeed = 4.1f,
                StrafeSpeed = 2.2f,
                TurnSpeed = 460f,
                Acceleration = 12f,
                SightRange = 17f,
                SightHalfAngle = 72f,
                HearingRange = 22f,
                ReactionTime = 0.32f,
                PreferredRange = 2.5f,
                AttackRange = 2.1f,
                AttackCooldown = 1.5f,
                AttackCooldownVariance = 0.45f,
                BlockChance = 0.3f,
                RetreatChance = 0.25f,
                FlankPreference = 0.35f,
                ExperienceReward = 22,
                MoneyReward = 14,
                Moves = EnemyMoves.Brawler()
            };

            return _brawler;
        }

        public static EnemyArchetype Bruiser()
        {
            if (_bruiser != null) return _bruiser;

            _bruiser = new EnemyArchetype
            {
                Kind = EnemyKind.Bruiser,
                DisplayName = "Bruiser",
                Style = CharacterStyle.Bruiser(),
                Health = 240f,
                Stamina = 120f,
                // Very high poise: light attacks will not interrupt him, so the
                // player has to dodge or parry rather than mash through.
                Poise = 95f,
                PoiseRegenPerSecond = 16f,
                DamageResistance = 0.18f,
                WalkSpeed = 1.6f,
                ChaseSpeed = 2.9f,
                StrafeSpeed = 1.3f,
                TurnSpeed = 190f,
                Acceleration = 6f,
                SightRange = 15f,
                SightHalfAngle = 62f,
                HearingRange = 24f,
                ReactionTime = 0.6f,
                PreferredRange = 2.9f,
                AttackRange = 2.7f,
                AttackCooldown = 2.5f,
                AttackCooldownVariance = 0.6f,
                BlockChance = 0.12f,
                RetreatChance = 0.02f,
                FlankPreference = 0.05f,
                ExperienceReward = 65,
                MoneyReward = 48,
                Moves = EnemyMoves.Bruiser()
            };

            return _bruiser;
        }

        public static EnemyArchetype Runner()
        {
            if (_runner != null) return _runner;

            _runner = new EnemyArchetype
            {
                Kind = EnemyKind.Runner,
                DisplayName = "Runner",
                Style = CharacterStyle.Runner(),
                Health = 55f,
                Stamina = 90f,
                Poise = 10f,
                PoiseRegenPerSecond = 7f,
                DamageResistance = 0f,
                WalkSpeed = 2.6f,
                ChaseSpeed = 6.1f,
                StrafeSpeed = 3.6f,
                TurnSpeed = 720f,
                Acceleration = 20f,
                SightRange = 20f,
                SightHalfAngle = 85f,
                HearingRange = 26f,
                ReactionTime = 0.16f,
                PreferredRange = 3.6f,
                AttackRange = 1.9f,
                AttackCooldown = 1.05f,
                AttackCooldownVariance = 0.3f,
                BlockChance = 0.05f,
                RetreatChance = 0.75f,
                // Strong flanking preference is what makes a group of runners feel
                // like being surrounded rather than queued up in front of you.
                FlankPreference = 0.9f,
                ExperienceReward = 30,
                MoneyReward = 18,
                Moves = EnemyMoves.Runner()
            };

            return _runner;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _brawler = _bruiser = _runner = null;
        }
    }
}
