using UnityEngine;

namespace Glowpulse.Core.Settings
{
    public enum Difficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }

    /// <summary>
    /// What a difficulty actually changes, as multipliers on the enemy data.
    ///
    /// Everything here is a scale factor rather than a replacement value, and
    /// Normal is exactly 1 across the board. That is deliberate: the combat
    /// balance was tuned by playing it, and a difficulty setting should bend that
    /// tuning, not substitute a second one. It also means new enemy types
    /// automatically obey the difficulty without anybody remembering to add them.
    /// </summary>
    public readonly struct DifficultyProfile
    {
        /// <summary>Scales the damage enemies deal to the player.</summary>
        public readonly float EnemyDamage;

        /// <summary>Scales enemy maximum health.</summary>
        public readonly float EnemyHealth;

        /// <summary>
        /// Scales how long enemies wait between attacks. Above 1 they hesitate,
        /// below 1 they crowd in - this is most of what "aggression" feels like.
        /// </summary>
        public readonly float EnemyCooldown;

        /// <summary>Scales how long an enemy takes to react to seeing the player.</summary>
        public readonly float EnemyReaction;

        /// <summary>How many enemies the combat director lets swing at once.</summary>
        public readonly int SimultaneousAttackers;

        public DifficultyProfile(float damage, float health, float cooldown, float reaction,
            int attackers)
        {
            EnemyDamage = damage;
            EnemyHealth = health;
            EnemyCooldown = cooldown;
            EnemyReaction = reaction;
            SimultaneousAttackers = attackers;
        }

        public static DifficultyProfile For(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Easy:
                    // Softer hits, less to chew through, and a visible pause
                    // between attacks so there is always time to answer one.
                    return new DifficultyProfile(0.65f, 0.75f, 1.35f, 1.4f, 1);

                case Difficulty.Hard:
                    // Enemies commit faster, recover sooner and two can swing at
                    // once, which is what actually raises the pressure - far more
                    // than the damage numbers do.
                    return new DifficultyProfile(1.45f, 1.35f, 0.7f, 0.65f, 3);

                default:
                    return Normal;
            }
        }

        /// <summary>The shipped balance, untouched.</summary>
        public static DifficultyProfile Normal => new DifficultyProfile(1f, 1f, 1f, 1f, 2);

        public static string DisplayName(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Easy: return "Easy";
                case Difficulty.Hard: return "Hard";
                default: return "Normal";
            }
        }

        public static string Describe(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Easy:
                    return "Enemies hit softer, have less health, and give you room to answer.";
                case Difficulty.Hard:
                    return "Enemies hit harder, take more punishment, and gang up on you.";
                default:
                    return "The fight as it was tuned.";
            }
        }
    }
}
