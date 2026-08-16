using UnityEngine;

namespace Glowpulse.Core
{
    /// <summary>How hard a hit throws the victim around.</summary>
    public enum HitImpact
    {
        /// <summary>Small flinch, victim keeps its footing and its combo state.</summary>
        Light = 0,
        /// <summary>Full stagger, interrupts whatever the victim was doing.</summary>
        Medium = 1,
        /// <summary>Stagger plus a long shove backwards.</summary>
        Heavy = 2,
        /// <summary>Sends the victim to the floor; they have to stand back up.</summary>
        Knockdown = 3,
        /// <summary>Pops the victim into the air.</summary>
        Launch = 4
    }

    /// <summary>
    /// Everything a hit needs to carry. Passed by value; never held onto by the
    /// receiver so it stays allocation free.
    /// </summary>
    public struct DamageInfo
    {
        public float Amount;
        public HitImpact Impact;

        /// <summary>World point the hit landed at - drives VFX and hit sparks.</summary>
        public Vector3 Point;

        /// <summary>Normalised direction the force pushes the victim.</summary>
        public Vector3 Direction;

        public float KnockbackForce;

        /// <summary>Seconds the victim is unable to act. Zero means use the impact default.</summary>
        public float StaggerDuration;

        /// <summary>Chip damage dealt through a successful block, as a fraction of Amount.</summary>
        public float BlockedDamageFraction;

        /// <summary>Stamina drained from a blocking victim.</summary>
        public float BlockStaminaCost;

        /// <summary>True when the attack cannot be blocked and must be dodged.</summary>
        public bool Unblockable;

        /// <summary>True when this hit can trigger a finisher on a downed target.</summary>
        public bool CanFinish;

        public Faction AttackerFaction;
        public GameObject Attacker;

        /// <summary>Free-form tag used by audio/VFX to pick the right punch or kick.</summary>
        public string AttackId;

        public static DamageInfo Create(float amount, HitImpact impact, Vector3 point, Vector3 direction,
            GameObject attacker, Faction attackerFaction)
        {
            return new DamageInfo
            {
                Amount = amount,
                Impact = impact,
                Point = point,
                Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward,
                KnockbackForce = DefaultKnockback(impact),
                StaggerDuration = 0f,
                BlockedDamageFraction = 0.15f,
                BlockStaminaCost = 10f + amount * 0.35f,
                Unblockable = false,
                CanFinish = false,
                AttackerFaction = attackerFaction,
                Attacker = attacker,
                AttackId = null
            };
        }

        public static float DefaultKnockback(HitImpact impact)
        {
            switch (impact)
            {
                case HitImpact.Light: return 1.4f;
                case HitImpact.Medium: return 3.2f;
                case HitImpact.Heavy: return 6.5f;
                case HitImpact.Knockdown: return 8.5f;
                case HitImpact.Launch: return 6.0f;
                default: return 1.5f;
            }
        }

        public static float DefaultStagger(HitImpact impact)
        {
            switch (impact)
            {
                case HitImpact.Light: return 0.22f;
                case HitImpact.Medium: return 0.45f;
                case HitImpact.Heavy: return 0.7f;
                case HitImpact.Knockdown: return 2.1f;
                case HitImpact.Launch: return 1.4f;
                default: return 0.25f;
            }
        }
    }

    /// <summary>Result of delivering a hit, reported back to the attacker.</summary>
    public enum HitResult
    {
        Missed = 0,
        Hit = 1,
        Blocked = 2,
        Parried = 3,
        Killed = 4,
        Immune = 5
    }
}
