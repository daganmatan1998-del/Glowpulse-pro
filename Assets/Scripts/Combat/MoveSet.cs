using System.Collections.Generic;
using Glowpulse.Core;
using Glowpulse.Core.Characters;
using UnityEngine;

namespace Glowpulse.Combat
{
    /// <summary>
    /// A character's complete set of moves, plus the entry points that turn a
    /// button press into a specific attack. Enemies get their own instances with
    /// different timings, which is most of what makes an archetype feel distinct.
    /// </summary>
    public sealed class MoveSet
    {
        private readonly Dictionary<string, AttackDefinition> _moves =
            new Dictionary<string, AttackDefinition>(24);

        public string LightOpener { get; set; }
        public string HeavyOpener { get; set; }
        public string CounterMove { get; set; }
        public string FinisherMove { get; set; }
        public string GrabMove { get; set; }
        public string ThrowMove { get; set; }

        public IEnumerable<AttackDefinition> All => _moves.Values;
        public int Count => _moves.Count;

        public MoveSet Add(AttackDefinition move)
        {
            if (move == null || string.IsNullOrEmpty(move.Id)) return this;
            _moves[move.Id] = move;
            return this;
        }

        public AttackDefinition Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return _moves.TryGetValue(id, out AttackDefinition move) ? move : null;
        }

        public bool Has(string id) => !string.IsNullOrEmpty(id) && _moves.ContainsKey(id);

        /// <summary>
        /// Resolves the next move in a string. With no current move this is the
        /// opener; inside a combo it follows the current move's chain.
        /// </summary>
        public AttackDefinition Resolve(AttackDefinition current, bool heavy)
        {
            if (current == null) return Get(heavy ? HeavyOpener : LightOpener);

            string next = heavy ? current.NextHeavy : current.NextLight;
            AttackDefinition resolved = Get(next);

            // Running off the end of a string restarts it rather than dropping
            // the input, so mashing keeps the character swinging.
            return resolved ?? Get(heavy ? HeavyOpener : LightOpener);
        }

        /// <summary>
        /// Reports chains that point at moves which do not exist. Called by the
        /// tests, and cheap enough to run once at startup in the editor.
        /// </summary>
        public List<string> Validate()
        {
            var problems = new List<string>();

            void CheckEntry(string label, string id)
            {
                if (string.IsNullOrEmpty(id)) problems.Add($"{label} is not set");
                else if (!Has(id)) problems.Add($"{label} points at missing move '{id}'");
            }

            CheckEntry("LightOpener", LightOpener);
            CheckEntry("HeavyOpener", HeavyOpener);

            foreach (AttackDefinition move in _moves.Values)
            {
                if (!string.IsNullOrEmpty(move.NextLight) && !Has(move.NextLight))
                    problems.Add($"'{move.Id}' chains light into missing move '{move.NextLight}'");

                if (!string.IsNullOrEmpty(move.NextHeavy) && !Has(move.NextHeavy))
                    problems.Add($"'{move.Id}' chains heavy into missing move '{move.NextHeavy}'");

                if (move.Duration <= 0f)
                    problems.Add($"'{move.Id}' has a zero duration");

                if (move.ComboWindowStart > move.ComboWindowEnd)
                    problems.Add($"'{move.Id}' has an inverted combo window");

                if (!string.IsNullOrEmpty(move.ClipId) && !PoseLibrary.Has(move.ClipId))
                    problems.Add($"'{move.Id}' references missing animation clip '{move.ClipId}'");
            }

            return problems;
        }

        // ---- the player's move set ------------------------------------------------

        private static MoveSet _player;

        /// <summary>
        /// The player's moves. Three light attacks that chain, each of which can
        /// be cashed out into a heavy finisher - so "light light heavy" and
        /// "light light light heavy" both exist and feel different - plus a two
        /// hit heavy string, a counter, a finisher, a grab and a throw.
        ///
        /// Heavy attacks are slower, cost real stamina and hit roughly three
        /// times as hard, which is the trade the whole combat loop rests on.
        /// </summary>
        public static MoveSet Player()
        {
            if (_player != null) return _player;

            CombatPoses.EnsureRegistered();

            var set = new MoveSet
            {
                LightOpener = "light_1",
                HeavyOpener = "heavy_1",
                CounterMove = "counter",
                FinisherMove = "finisher",
                GrabMove = "grab",
                ThrowMove = "throw"
            };

            // ---- light string ------------------------------------------------
            set.Add(new AttackDefinition
            {
                Id = "light_1", Kind = AttackKind.Light, ClipId = CombatPoses.LightJab,
                ImpactTag = "punch",
                Windup = 0.09f, Active = 0.07f, Recovery = 0.19f,
                Damage = 9f, Impact = HitImpact.Light, StaminaCost = 5f,
                HitboxOffset = new Vector3(0.18f, 1.28f, 0.72f), HitboxRadius = 0.52f,
                LungeDistance = 0.55f,
                ComboWindowStart = 0.38f,
                NextLight = "light_2", NextHeavy = "heavy_smash",
                HitStop = 0.045f, CameraShake = 0.11f, CameraKick = 0.05f
            });

            set.Add(new AttackDefinition
            {
                Id = "light_2", Kind = AttackKind.Light, ClipId = CombatPoses.LightCross,
                ImpactTag = "punch",
                Windup = 0.1f, Active = 0.07f, Recovery = 0.2f,
                Damage = 11f, Impact = HitImpact.Light, StaminaCost = 5f,
                HitboxOffset = new Vector3(-0.18f, 1.26f, 0.78f), HitboxRadius = 0.55f,
                LungeDistance = 0.62f,
                ComboWindowStart = 0.4f,
                NextLight = "light_3", NextHeavy = "heavy_smash",
                HitStop = 0.05f, CameraShake = 0.13f, CameraKick = 0.06f
            });

            set.Add(new AttackDefinition
            {
                Id = "light_3", Kind = AttackKind.Light, ClipId = CombatPoses.LightKick,
                ImpactTag = "kick",
                Windup = 0.13f, Active = 0.09f, Recovery = 0.26f,
                Damage = 15f, Impact = HitImpact.Medium, StaminaCost = 8f,
                Shape = HitboxShape.Capsule,
                HitboxOffset = new Vector3(0f, 1.0f, 0.7f), HitboxRadius = 0.58f, HitboxLength = 0.7f,
                LungeDistance = 0.75f, KnockbackMultiplier = 1.3f,
                ComboWindowStart = 0.44f,
                NextLight = null, NextHeavy = "heavy_launcher",
                HitStop = 0.07f, CameraShake = 0.2f, CameraKick = 0.1f
            });

            // ---- heavy string --------------------------------------------------
            set.Add(new AttackDefinition
            {
                Id = "heavy_1", Kind = AttackKind.Heavy, ClipId = CombatPoses.HeavyHook,
                ImpactTag = "heavy",
                Windup = 0.26f, Active = 0.11f, Recovery = 0.38f,
                Damage = 30f, Impact = HitImpact.Heavy, StaminaCost = 20f,
                KnockbackMultiplier = 1.4f, GuardStaminaDamage = 28f, ChipDamage = 0.18f,
                Shape = HitboxShape.Capsule,
                HitboxOffset = new Vector3(0f, 1.22f, 0.8f), HitboxRadius = 0.66f, HitboxLength = 0.85f,
                LungeDistance = 1.05f, MaxTargets = 4,
                ComboWindowStart = 0.55f,
                NextHeavy = "heavy_2",
                HitStop = 0.1f, CameraShake = 0.34f, CameraKick = 0.22f
            });

            set.Add(new AttackDefinition
            {
                Id = "heavy_2", Kind = AttackKind.Heavy, ClipId = CombatPoses.HeavyOverhead,
                ImpactTag = "heavy",
                Windup = 0.34f, Active = 0.12f, Recovery = 0.46f,
                Damage = 42f, Impact = HitImpact.Knockdown, StaminaCost = 26f,
                KnockbackMultiplier = 1.6f, GuardStaminaDamage = 38f, ChipDamage = 0.22f,
                Shape = HitboxShape.Capsule,
                HitboxOffset = new Vector3(0f, 1.1f, 0.85f), HitboxRadius = 0.72f, HitboxLength = 0.9f,
                LungeDistance = 1.2f, MaxTargets = 4,
                ComboWindowStart = 0.7f,
                HitStop = 0.14f, CameraShake = 0.46f, CameraKick = 0.34f,
                SlowMotionScale = 0.45f, SlowMotionDuration = 0.16f
            });

            // Cash-out heavies, reached by pressing heavy inside a light string.
            set.Add(new AttackDefinition
            {
                Id = "heavy_smash", Kind = AttackKind.Heavy, ClipId = CombatPoses.HeavyHook,
                ImpactTag = "heavy",
                Windup = 0.2f, Active = 0.1f, Recovery = 0.34f,
                Damage = 34f, Impact = HitImpact.Heavy, StaminaCost = 22f,
                KnockbackMultiplier = 1.5f, GuardStaminaDamage = 30f, ChipDamage = 0.2f,
                Shape = HitboxShape.Capsule,
                HitboxOffset = new Vector3(0f, 1.2f, 0.82f), HitboxRadius = 0.68f, HitboxLength = 0.85f,
                LungeDistance = 1.15f, MaxTargets = 4,
                ComboWindowStart = 0.68f,
                HitStop = 0.11f, CameraShake = 0.38f, CameraKick = 0.26f
            });

            set.Add(new AttackDefinition
            {
                Id = "heavy_launcher", Kind = AttackKind.Heavy, ClipId = CombatPoses.HeavyUppercut,
                ImpactTag = "heavy",
                Windup = 0.24f, Active = 0.12f, Recovery = 0.42f,
                Damage = 46f, Impact = HitImpact.Launch, StaminaCost = 28f,
                KnockbackMultiplier = 1.2f, GuardStaminaDamage = 40f, ChipDamage = 0.22f,
                HitboxOffset = new Vector3(0f, 1.15f, 0.7f), HitboxRadius = 0.75f,
                LungeDistance = 0.8f, MaxTargets = 3,
                ComboWindowStart = 0.75f,
                HitStop = 0.15f, CameraShake = 0.5f, CameraKick = 0.3f,
                SlowMotionScale = 0.4f, SlowMotionDuration = 0.22f
            });

            // ---- reactive moves ---------------------------------------------------
            set.Add(new AttackDefinition
            {
                Id = "counter", Kind = AttackKind.Counter, ClipId = CombatPoses.Counter,
                ImpactTag = "heavy",
                Windup = 0.08f, Active = 0.1f, Recovery = 0.26f,
                Damage = 38f, Impact = HitImpact.Heavy, StaminaCost = 0f,
                Unblockable = true, KnockbackMultiplier = 1.5f,
                HitboxOffset = new Vector3(0f, 1.2f, 0.8f), HitboxRadius = 0.7f,
                LungeDistance = 1.3f, LungeTrackingBonus = 2.4f, MaxTargets = 1,
                ComboWindowStart = 0.6f, NextLight = "light_2",
                HitStop = 0.13f, CameraShake = 0.4f, CameraKick = 0.28f,
                SlowMotionScale = 0.35f, SlowMotionDuration = 0.24f
            });

            set.Add(new AttackDefinition
            {
                Id = "finisher", Kind = AttackKind.Finisher, ClipId = CombatPoses.Finisher,
                ImpactTag = "finisher",
                Windup = 0.3f, Active = 0.14f, Recovery = 0.5f,
                Damage = 90f, Impact = HitImpact.Knockdown, StaminaCost = 15f,
                Unblockable = true, KnockbackMultiplier = 0.6f,
                HitboxOffset = new Vector3(0f, 0.55f, 0.85f), HitboxRadius = 0.85f,
                LungeDistance = 0.5f, MaxTargets = 1,
                ComboWindowStart = 1f,
                HitStop = 0.2f, CameraShake = 0.65f, CameraKick = 0.4f,
                SlowMotionScale = 0.3f, SlowMotionDuration = 0.5f
            });

            // ---- grappling ----------------------------------------------------------
            set.Add(new AttackDefinition
            {
                Id = "grab", Kind = AttackKind.Grab, ClipId = CombatPoses.Grab,
                ImpactTag = "grab",
                Windup = 0.16f, Active = 0.12f, Recovery = 0.3f,
                Damage = 0f, Impact = HitImpact.Light, StaminaCost = 12f,
                HitboxOffset = new Vector3(0f, 1.1f, 0.72f), HitboxRadius = 0.6f,
                LungeDistance = 0.6f, MaxTargets = 1,
                ComboWindowStart = 1f,
                HitStop = 0.04f, CameraShake = 0.08f
            });

            set.Add(new AttackDefinition
            {
                Id = "throw", Kind = AttackKind.Throw, ClipId = CombatPoses.Throw,
                ImpactTag = "throw",
                Windup = 0.22f, Active = 0.1f, Recovery = 0.42f,
                Damage = 34f, Impact = HitImpact.Knockdown, StaminaCost = 10f,
                Unblockable = true, KnockbackMultiplier = 2.2f,
                HitboxOffset = new Vector3(0f, 1.1f, 0.75f), HitboxRadius = 0.7f,
                LungeDistance = 0f, MaxTargets = 1,
                ComboWindowStart = 1f,
                HitStop = 0.13f, CameraShake = 0.45f, CameraKick = 0.3f,
                SlowMotionScale = 0.5f, SlowMotionDuration = 0.2f
            });

            _player = set;
            return _player;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _player = null;
    }
}
