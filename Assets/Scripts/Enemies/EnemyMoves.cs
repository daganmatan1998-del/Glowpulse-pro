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
        private static MoveSet _elite, _miniBoss, _finalBoss;

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

        /// <summary>
        /// The elite's set: a fast opener that chains twice, and a committed
        /// finisher. Longer strings than a brawler, so an elite keeps its turn
        /// instead of trading one hit and resetting.
        /// </summary>
        public static MoveSet Elite()
        {
            if (_elite != null) return _elite;
            CombatPoses.EnsureRegistered();

            var set = new MoveSet { LightOpener = "el_jab", HeavyOpener = "el_hook" };

            set.Add(new AttackDefinition
            {
                Id = "el_jab", Kind = AttackKind.Light, ClipId = CombatPoses.LightJab,
                ImpactTag = "punch",
                Windup = 0.2f, Active = 0.06f, Recovery = 0.22f,
                Damage = 9f, Impact = HitImpact.Light, StaminaCost = 5f,
                HitboxOffset = new Vector3(0.14f, 1.24f, 0.7f), HitboxRadius = 0.48f,
                LungeDistance = 1.1f, LungeTrackingBonus = 1.6f, MaxTargets = 1,
                ComboWindowStart = 0.48f, NextLight = "el_cross",
                HitStop = 0.035f, CameraShake = 0.12f
            });

            set.Add(new AttackDefinition
            {
                Id = "el_cross", Kind = AttackKind.Light, ClipId = CombatPoses.LightCross,
                ImpactTag = "punch",
                Windup = 0.2f, Active = 0.06f, Recovery = 0.26f,
                Damage = 11f, Impact = HitImpact.Light, StaminaCost = 6f,
                HitboxOffset = new Vector3(-0.14f, 1.24f, 0.72f), HitboxRadius = 0.48f,
                LungeDistance = 1f, LungeTrackingBonus = 1.5f, MaxTargets = 1,
                ComboWindowStart = 0.5f, NextLight = "el_spin",
                HitStop = 0.04f, CameraShake = 0.14f
            });

            set.Add(new AttackDefinition
            {
                Id = "el_spin", Kind = AttackKind.Heavy, ClipId = CombatPoses.LightKick,
                ImpactTag = "kick",
                Windup = 0.34f, Active = 0.09f, Recovery = 0.42f,
                Damage = 20f, Impact = HitImpact.Heavy, StaminaCost = 12f,
                KnockbackMultiplier = 1.7f,
                Shape = HitboxShape.Capsule,
                HitboxOffset = new Vector3(0f, 1.05f, 0.72f), HitboxRadius = 0.56f,
                HitboxLength = 0.7f,
                LungeDistance = 1.5f, LungeTrackingBonus = 2f, MaxTargets = 2,
                ComboWindowStart = 1f,
                HitStop = 0.075f, CameraShake = 0.26f
            });

            set.Add(new AttackDefinition
            {
                Id = "el_hook", Kind = AttackKind.Heavy, ClipId = CombatPoses.HeavyHook,
                ImpactTag = "heavy",
                Windup = 0.46f, Active = 0.1f, Recovery = 0.5f,
                Damage = 26f, Impact = HitImpact.Heavy, StaminaCost = 16f,
                KnockbackMultiplier = 1.9f,
                HitboxOffset = new Vector3(0.16f, 1.2f, 0.78f), HitboxRadius = 0.62f,
                LungeDistance = 1.7f, LungeTrackingBonus = 1.8f, MaxTargets = 1,
                ComboWindowStart = 1f,
                HitStop = 0.09f, CameraShake = 0.3f
            });

            _elite = set;
            return _elite;
        }

        /// <summary>
        /// The mini-boss: three heavy swings on a readable rhythm, one of which
        /// knocks down. There is nothing quick here - the fight is about reading
        /// the wind-up and getting out of the way.
        /// </summary>
        public static MoveSet MiniBoss()
        {
            if (_miniBoss != null) return _miniBoss;
            CombatPoses.EnsureRegistered();

            var set = new MoveSet { LightOpener = "mb_swipe", HeavyOpener = "mb_slam" };

            set.Add(new AttackDefinition
            {
                Id = "mb_swipe", Kind = AttackKind.Heavy, ClipId = CombatPoses.HeavyHook,
                ImpactTag = "heavy",
                Windup = 0.5f, Active = 0.12f, Recovery = 0.52f,
                Damage = 24f, Impact = HitImpact.Heavy, StaminaCost = 14f,
                KnockbackMultiplier = 2f,
                Shape = HitboxShape.Capsule,
                HitboxOffset = new Vector3(0f, 1.25f, 0.8f), HitboxRadius = 0.72f,
                HitboxLength = 1.5f,
                LungeDistance = 1.5f, LungeTrackingBonus = 1.4f, MaxTargets = 3,
                ComboWindowStart = 0.62f, NextHeavy = "mb_slam",
                HitStop = 0.095f, CameraShake = 0.34f
            });

            set.Add(new AttackDefinition
            {
                Id = "mb_slam", Kind = AttackKind.Heavy, ClipId = CombatPoses.HeavyOverhead,
                ImpactTag = "heavy",
                Windup = 0.86f, Active = 0.14f, Recovery = 0.75f,
                Damage = 44f, Impact = HitImpact.Knockdown, StaminaCost = 24f,
                KnockbackMultiplier = 2.6f, Unblockable = true,
                HitboxOffset = new Vector3(0f, 0.7f, 0.9f), HitboxRadius = 1.05f,
                LungeDistance = 1.3f, LungeTrackingBonus = 1.1f, MaxTargets = 3,
                ComboWindowStart = 1f,
                HitStop = 0.13f, CameraShake = 0.55f, CameraKick = 0.4f
            });

            // The charge is how it closes a gap the player thought was safe.
            set.Add(new AttackDefinition
            {
                Id = "mb_charge", Kind = AttackKind.Heavy, ClipId = CombatPoses.HeavyUppercut,
                ImpactTag = "heavy",
                Windup = 0.72f, Active = 0.22f, Recovery = 0.7f,
                Damage = 32f, Impact = HitImpact.Knockdown, StaminaCost = 20f,
                KnockbackMultiplier = 2.4f,
                Shape = HitboxShape.Capsule,
                HitboxOffset = new Vector3(0f, 1.1f, 0.85f), HitboxRadius = 0.7f,
                HitboxLength = 1f,
                LungeDistance = 5.2f, LungeTrackingBonus = 0.8f, MaxTargets = 3,
                ComboWindowStart = 1f,
                HitStop = 0.11f, CameraShake = 0.42f, CameraKick = 0.3f
            });

            _miniBoss = set;
            return _miniBoss;
        }

        /// <summary>
        /// The final boss: a fast string, a committed unblockable, and a sweep
        /// that punishes standing still. Enough variety that the fight cannot be
        /// solved by learning one answer.
        /// </summary>
        public static MoveSet FinalBoss()
        {
            if (_finalBoss != null) return _finalBoss;
            CombatPoses.EnsureRegistered();

            var set = new MoveSet { LightOpener = "fb_jab", HeavyOpener = "fb_smash" };

            set.Add(new AttackDefinition
            {
                Id = "fb_jab", Kind = AttackKind.Light, ClipId = CombatPoses.LightJab,
                ImpactTag = "punch",
                Windup = 0.22f, Active = 0.06f, Recovery = 0.2f,
                Damage = 12f, Impact = HitImpact.Light, StaminaCost = 5f,
                HitboxOffset = new Vector3(0.15f, 1.3f, 0.74f), HitboxRadius = 0.5f,
                LungeDistance = 1.2f, LungeTrackingBonus = 1.7f, MaxTargets = 1,
                ComboWindowStart = 0.46f, NextLight = "fb_cross",
                HitStop = 0.04f, CameraShake = 0.14f
            });

            set.Add(new AttackDefinition
            {
                Id = "fb_cross", Kind = AttackKind.Light, ClipId = CombatPoses.LightCross,
                ImpactTag = "punch",
                Windup = 0.2f, Active = 0.06f, Recovery = 0.24f,
                Damage = 14f, Impact = HitImpact.Medium, StaminaCost = 6f,
                HitboxOffset = new Vector3(-0.15f, 1.3f, 0.76f), HitboxRadius = 0.5f,
                LungeDistance = 1.1f, LungeTrackingBonus = 1.5f, MaxTargets = 1,
                ComboWindowStart = 0.48f, NextLight = "fb_uppercut",
                HitStop = 0.05f, CameraShake = 0.18f
            });

            set.Add(new AttackDefinition
            {
                Id = "fb_uppercut", Kind = AttackKind.Heavy, ClipId = CombatPoses.HeavyUppercut,
                ImpactTag = "heavy",
                Windup = 0.4f, Active = 0.1f, Recovery = 0.46f,
                Damage = 28f, Impact = HitImpact.Launch, StaminaCost = 16f,
                KnockbackMultiplier = 2.1f,
                HitboxOffset = new Vector3(0f, 1.15f, 0.72f), HitboxRadius = 0.62f,
                LungeDistance = 1.4f, LungeTrackingBonus = 1.6f, MaxTargets = 1,
                ComboWindowStart = 1f,
                HitStop = 0.1f, CameraShake = 0.34f, CameraKick = 0.26f
            });

            set.Add(new AttackDefinition
            {
                Id = "fb_smash", Kind = AttackKind.Heavy, ClipId = CombatPoses.HeavyOverhead,
                ImpactTag = "heavy",
                Windup = 0.78f, Active = 0.13f, Recovery = 0.66f,
                Damage = 46f, Impact = HitImpact.Knockdown, StaminaCost = 24f,
                KnockbackMultiplier = 2.5f, Unblockable = true,
                HitboxOffset = new Vector3(0f, 0.75f, 0.86f), HitboxRadius = 1f,
                LungeDistance = 1.6f, LungeTrackingBonus = 1.2f, MaxTargets = 3,
                ComboWindowStart = 1f,
                HitStop = 0.13f, CameraShake = 0.6f, CameraKick = 0.45f
            });

            // The sweep is the answer to a player who plants their feet and blocks.
            set.Add(new AttackDefinition
            {
                Id = "fb_sweep", Kind = AttackKind.Heavy, ClipId = CombatPoses.LightKick,
                ImpactTag = "kick",
                Windup = 0.44f, Active = 0.14f, Recovery = 0.5f,
                Damage = 22f, Impact = HitImpact.Knockdown, StaminaCost = 14f,
                KnockbackMultiplier = 1.5f,
                Shape = HitboxShape.Capsule,
                HitboxOffset = new Vector3(0f, 0.45f, 0.75f), HitboxRadius = 0.6f,
                HitboxLength = 1.8f,
                LungeDistance = 2.2f, LungeTrackingBonus = 1.8f, MaxTargets = 3,
                ComboWindowStart = 1f,
                HitStop = 0.09f, CameraShake = 0.34f
            });

            _finalBoss = set;
            return _finalBoss;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _brawler = _bruiser = _runner = null;
            _elite = _miniBoss = _finalBoss = null;
        }
    }
}
