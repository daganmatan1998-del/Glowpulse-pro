using System;
using Glowpulse.Core;
using UnityEngine;

namespace Glowpulse.Combat
{
    /// <summary>Broad category of a move, used for input routing and scoring.</summary>
    public enum AttackKind
    {
        Light = 0,
        Heavy = 1,
        Counter = 2,
        Finisher = 3,
        Grab = 4,
        Throw = 5
    }

    /// <summary>Shape swept for hit detection.</summary>
    public enum HitboxShape
    {
        /// <summary>A ball at the offset point. Punches and kicks.</summary>
        Sphere = 0,

        /// <summary>A capsule reaching forward from the offset. Sweeping and spinning moves.</summary>
        Capsule = 1
    }

    /// <summary>
    /// One move: its timing, its hitbox, what it does on contact, and how it
    /// chains. Timings are in seconds rather than animation frames so the data
    /// stays meaningful when the placeholder animation is swapped for real clips.
    ///
    /// The three phases are the standard fighting-game contract:
    ///   windup   - committed, no hitbox, the tell the opponent reads
    ///   active   - the hitbox is live
    ///   recovery - vulnerable, the punish window
    /// </summary>
    [Serializable]
    public sealed class AttackDefinition
    {
        [Header("Identity")]
        public string Id = "attack";
        public AttackKind Kind = AttackKind.Light;

        [Tooltip("PoseLibrary clip id played for this move.")]
        public string ClipId;

        [Tooltip("Tag passed to audio and VFX so a kick sounds different from a punch.")]
        public string ImpactTag = "punch";

        [Header("Timing (seconds)")]
        public float Windup = 0.12f;
        public float Active = 0.09f;
        public float Recovery = 0.22f;

        [Header("Damage")]
        public float Damage = 10f;
        public HitImpact Impact = HitImpact.Light;
        public float KnockbackMultiplier = 1f;
        public bool Unblockable;

        [Tooltip("Fraction of the damage that still gets through a successful block.")]
        [Range(0f, 1f)] public float ChipDamage = 0.12f;

        [Tooltip("Stamina the defender loses when they block this.")]
        public float GuardStaminaDamage = 14f;

        [Header("Cost")]
        public float StaminaCost = 8f;

        [Header("Hitbox")]
        public HitboxShape Shape = HitboxShape.Sphere;

        [Tooltip("Local-space centre of the hitbox, relative to the attacker's feet.")]
        public Vector3 HitboxOffset = new Vector3(0f, 1.15f, 0.85f);

        public float HitboxRadius = 0.62f;

        [Tooltip("Extra forward reach for capsule hitboxes.")]
        public float HitboxLength = 1.1f;

        public int MaxTargets = 3;

        [Header("Movement")]
        [Tooltip("Metres the attacker slides forward across the windup and active frames.")]
        public float LungeDistance = 0.85f;

        [Tooltip("Extra lunge added when closing on a locked target that is further away.")]
        public float LungeTrackingBonus = 1.6f;

        [Header("Combo")]
        [Tooltip("Fraction of the total duration after which the next input is accepted.")]
        [Range(0f, 1f)] public float ComboWindowStart = 0.42f;

        [Range(0f, 1f)] public float ComboWindowEnd = 1f;

        [Tooltip("Move id a light attack chains into. Empty ends the string.")]
        public string NextLight;

        [Tooltip("Move id a heavy attack chains into. Empty ends the string.")]
        public string NextHeavy;

        [Header("Feel")]
        [Tooltip("Seconds of hit stop when this connects.")]
        public float HitStop = 0.055f;

        [Tooltip("Camera trauma added on contact, 0..1.")]
        public float CameraShake = 0.16f;

        [Tooltip("Time scale for the brief slow motion. 1 disables it.")]
        [Range(0.05f, 1f)] public float SlowMotionScale = 1f;

        public float SlowMotionDuration = 0.18f;

        [Tooltip("Camera kick strength along the attack direction.")]
        public float CameraKick;

        /// <summary>Total length of the move.</summary>
        public float Duration => Windup + Active + Recovery;

        public float ActiveStart => Windup;
        public float ActiveEnd => Windup + Active;

        /// <summary>True when the hitbox is live at the given time since the move started.</summary>
        public bool IsActiveAt(float t) => t >= ActiveStart && t <= ActiveEnd;

        /// <summary>True when a follow-up input should be accepted at this time.</summary>
        public bool InComboWindow(float t)
        {
            float duration = Duration;
            return t >= duration * ComboWindowStart && t <= duration * ComboWindowEnd;
        }

        /// <summary>Playback rate that makes the pose clip last exactly as long as the move.</summary>
        public float ClipSpeed(float clipDuration)
        {
            return clipDuration <= 0.001f ? 1f : clipDuration / Duration;
        }

        /// <summary>Builds the damage payload this move delivers.</summary>
        public DamageInfo BuildDamage(GameObject attacker, Faction faction, Vector3 point, Vector3 direction,
            float damageMultiplier = 1f)
        {
            DamageInfo info = DamageInfo.Create(Damage * damageMultiplier, Impact, point, direction,
                attacker, faction);
            info.KnockbackForce *= KnockbackMultiplier;
            info.Unblockable = Unblockable;
            info.BlockedDamageFraction = ChipDamage;
            info.BlockStaminaCost = GuardStaminaDamage;
            info.CanFinish = Kind == AttackKind.Finisher;
            info.AttackId = ImpactTag;
            return info;
        }

        public AttackDefinition Clone() => (AttackDefinition)MemberwiseClone();
    }
}
