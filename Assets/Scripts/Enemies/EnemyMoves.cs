using Glowpulse.Combat;
using Glowpulse.Core;
using UnityEngine;

namespace Glowpulse.Enemies
{
    /// <summary>
    /// The enemy move sets. They reuse the same <see cref="AttackDefinition"/>
    /// data and the same animation clips as the player - only the numbers differ.
    ///
    /// Enemy attacks are deliberately slower to start than the player's. The
    /// wind-up is the tell, and a readable tell is what makes dodging and
    /// parrying a skill rather than a guess.
    /// </summary>
    public static class EnemyMoves
    {
        private static MoveSet _brawler, _bruiser, _runner;

        public static MoveSet Brawler()
        {
            if (_brawler != null) return _brawler;
            CombatPoses.EnsureRegistered();

            var set = new MoveSet { LightOpener = "e_jab", HeavyOpener = "e_swing" };

            set.Add(new AttackDefinition
            {
                Id = "e_jab", Kind = AttackKind.Light, ClipId = CombatPoses.LightJab,
                ImpactTag = "punch",
                Windup = 0.26f, Active = 0.08f, Recovery = 0.34f,
                Damage = 9f, Impact = HitImpact.Light, StaminaCost = 6f,
                HitboxOffset = new Vector3(0.15f, 1.25f, 0.7f), HitboxRadius = 0.5f,
                LungeDistance = 0.7f, LungeTrackingBonus = 0.9f, MaxTargets = 1,
                ComboWindowStart = 0.55f, NextLight = "e_cross",
                HitStop = 0.04f, CameraShake = 0.14f
            });

            set.Add(new AttackDefinition
            {
                Id = "e_cross", Kind = AttackKind.Light, ClipId = CombatPoses.LightCross,
                ImpactTag = "punch",
                Windup = 0.28f, Active = 0.08f, Recovery = 0.44f,
                Damage = 12f, Impact = HitImpact.Medium, StaminaCost = 8f,
                HitboxOffset = new Vector3(-0.15f, 1.24f, 0.74f), HitboxRadius = 0.52f,
                LungeDistance = 0.8f, LungeTrackingBonus = 1.0f, MaxTargets = 1,
                ComboWindowStart = 1f,
                HitStop = 0.05f, CameraShake = 0.18f
            });

            set.Add(new AttackDefinition
            {
                Id = "e_swing", Kind = AttackKind.Heavy, ClipId = CombatPoses.HeavyHook,
                ImpactTag = "heavy",
                Windup = 0.52f, Active = 0.11f, Recovery = 0.55f,
                Damage = 22f, Impact = HitImpact.Heavy, StaminaCost = 18f,
                KnockbackMultiplier = 1.2f, GuardStaminaDamage = 24f,
                Shape = HitboxShape.Capsule,
                HitboxOffset = new Vector3(0f, 1.2f, 0.78f), HitboxRadius = 0.62f, HitboxLength = 0.7f,
                LungeDistance = 1.0f, LungeTrackingBonus = 1.2f, MaxTargets = 2,
                ComboWindowStart = 1f,
                HitStop = 0.08f, CameraShake = 0.3f, CameraKick = 0.16f
            });

            set.Add(new AttackDefinition
            {
                Id = "e_kick", Kind = AttackKind.Heavy, ClipId = CombatPoses.LightKick,
                ImpactTag = "kick",
                Windup = 0.4f, Active = 0.09f, Recovery = 0.48f,
                Damage = 16f, Impact = HitImpact.Medium, StaminaCost = 12f,
                KnockbackMultiplier = 1.5f,
                Shape = HitboxShape.Capsule,
                HitboxOffset = new Vector3(0f, 1.0f, 0.72f), HitboxRadius = 0.55f, HitboxLength = 0.6f,
                LungeDistance = 0.9f, LungeTrackingBonus = 1.1f, MaxTargets = 1,
                ComboWindowStart = 1f,
                HitStop = 0.06f, CameraShake = 0.24f
            });

            _brawler = set;
            return _brawler;
        }

        public static MoveSet Bruiser()
        {
            if (_bruiser != null) return _bruiser;
            CombatPoses.EnsureRegistered();

            var set = new MoveSet { LightOpener = "b_hook", HeavyOpener = "b_slam" };

            set.Add(new AttackDefinition
            {
                Id = "b_hook", Kind = AttackKind.Heavy, ClipId = CombatPoses.HeavyHook,
                ImpactTag = "heavy",
                Windup = 0.62f, Active = 0.13f, Recovery = 0.68f,
                Damage = 30f, Impact = HitImpact.Heavy, StaminaCost = 16f,
                KnockbackMultiplier = 1.7f, GuardStaminaDamage = 40f, ChipDamage = 0.2f,
                Shape = HitboxShape.Capsule,
                HitboxOffset = new Vector3(0f, 1.3f, 0.9f), HitboxRadius = 0.78f, HitboxLength = 0.9f,
                LungeDistance = 1.1f, LungeTrackingBonus = 1.4f, MaxTargets = 3,
                ComboWindowStart = 0.72f, NextHeavy = "b_slam",
                HitStop = 0.11f, CameraShake = 0.42f, CameraKick = 0.26f
            });

            // The unblockable is the bruiser's whole design: the guard is not an
            // answer to him, so the player has to move.
            set.Add(new AttackDefinition
            {
                Id = "b_slam", Kind = AttackKind.Heavy, ClipId = CombatPoses.HeavyOverhead,
                ImpactTag = "heavy",
                Windup = 0.86f, Active = 0.15f, Recovery = 0.82f,
                Damage = 44f, Impact = HitImpact.Knockdown, StaminaCost = 24f,
                Unblockable = true, KnockbackMultiplier = 1.9f,
                Shape = HitboxShape.Capsule,
                HitboxOffset = new Vector3(0f, 0.9f, 0.95f), HitboxRadius = 0.9f, HitboxLength = 0.8f,
                LungeDistance = 1.3f, LungeTrackingBonus = 1.6f, MaxTargets = 4,
                ComboWindowStart = 1f,
                HitStop = 0.16f, CameraShake = 0.6f, CameraKick = 0.42f
            });

            set.Add(new AttackDefinition
            {
                Id = "b_charge", Kind = AttackKind.Heavy, ClipId = CombatPoses.HeavyUppercut,
                ImpactTag = "heavy",
                Windup = 0.7f, Active = 0.2f, Recovery = 0.75f,
                Damage = 36f, Impact = HitImpact.Launch, StaminaCost = 22f,
                KnockbackMultiplier = 1.4f, GuardStaminaDamage = 46f,
                HitboxOffset = new Vector3(0f, 1.15f, 0.85f), HitboxRadius = 0.85f,
                LungeDistance = 4.2f, LungeTrackingBonus = 3.5f, MaxTargets = 3,
                ComboWindowStart = 1f,
                HitStop = 0.15f, CameraShake = 0.55f, CameraKick = 0.34f
            });

            _bruiser = set;
            return _bruiser;
        }

        public static MoveSet Runner()
        {
            if (_runner != null) return _runner;
            CombatPoses.EnsureRegistered();

            var set = new MoveSet { LightOpener = "r_jab", HeavyOpener = "r_kick" };

            set.Add(new AttackDefinition
            {
                Id = "r_jab", Kind = AttackKind.Light, ClipId = CombatPoses.LightJab,
                ImpactTag = "punch",
                Windup = 0.16f, Active = 0.06f, Recovery = 0.24f,
                Damage = 6f, Impact = HitImpact.Light, StaminaCost = 4f,
                HitboxOffset = new Vector3(0.14f, 1.2f, 0.66f), HitboxRadius = 0.46f,
                LungeDistance = 1.0f, LungeTrackingBonus = 1.6f, MaxTargets = 1,
                ComboWindowStart = 0.5f, NextLight = "r_hook",
                HitStop = 0.03f, CameraShake = 0.1f
            });

            set.Add(new AttackDefinition
            {
                Id = "r_hook", Kind = AttackKind.Light, ClipId = CombatPoses.LightCross,
                ImpactTag = "punch",
                Windup = 0.18f, Active = 0.06f, Recovery = 0.3f,
                Damage = 8f, Impact = HitImpact.Light, StaminaCost = 5f,
                HitboxOffset = new Vector3(-0.14f, 1.2f, 0.68f), HitboxRadius = 0.46f,
                LungeDistance = 0.9f, LungeTrackingBonus = 1.4f, MaxTargets = 1,
                ComboWindowStart = 1f,
                HitStop = 0.035f, CameraShake = 0.12f
            });

            set.Add(new AttackDefinition
            {
                Id = "r_kick", Kind = AttackKind.Heavy, ClipId = CombatPoses.LightKick,
                ImpactTag = "kick",
                Windup = 0.3f, Active = 0.08f, Recovery = 0.4f,
                Damage = 14f, Impact = HitImpact.Medium, StaminaCost = 10f,
                KnockbackMultiplier = 1.6f,
                Shape = HitboxShape.Capsule,
                HitboxOffset = new Vector3(0f, 1.0f, 0.7f), HitboxRadius = 0.52f, HitboxLength = 0.6f,
                LungeDistance = 1.6f, LungeTrackingBonus = 2.2f, MaxTargets = 1,
                ComboWindowStart = 1f,
                HitStop = 0.06f, CameraShake = 0.22f
            });

            _runner = set;
            return _runner;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _brawler = _bruiser = _runner = null;
        }
    }
}
