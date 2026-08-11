using System;
using Glowpulse.Combat;
using Glowpulse.Core;
using Glowpulse.Core.Characters;
using UnityEngine;

namespace Glowpulse.Enemies
{
    /// <summary>
    /// An enemy's combat identity: poise, damage resistance, blocking, and the
    /// rewards it drops.
    ///
    /// Poise is the important part. Rather than every hit interrupting, an enemy
    /// absorbs a budget of stagger before it flinches. That is what lets a
    /// bruiser walk through jabs and forces the player to use heavies, throws or
    /// well-timed openings instead of mashing light attacks.
    /// </summary>
    public sealed class EnemyCombatant : Combatant
    {
        private EnemyArchetype _archetype;

        private float _poise;
        private float _maxPoise = 30f;
        private float _poiseRegen = 8f;
        private float _resistance;
        private float _lastPoiseDamageAt = float.NegativeInfinity;

        private bool _guardUp;

        /// <summary>Raised when poise breaks and the enemy is actually staggered.</summary>
        public event Action PoiseBroken;

        /// <summary>Raised on death with the rewards this enemy carries.</summary>
        public event Action<EnemyCombatant, int, int> DiedWithRewards;

        public EnemyArchetype Archetype => _archetype;

        public float Poise => _poise;
        public float PoiseNormalized => _maxPoise <= 0f ? 0f : Mathf.Clamp01(_poise / _maxPoise);

        /// <summary>True while the enemy is shrugging off hits rather than reacting.</summary>
        public bool HasSuperArmour => _poise > 0f && _maxPoise > 40f;

        public override bool IsBlocking => _guardUp && IsAlive && !IsDown && !IsReacting;

        /// <summary>Raised and lowered by the brain.</summary>
        public bool GuardUp
        {
            get => _guardUp;
            set => _guardUp = value;
        }

        public void Configure(EnemyArchetype archetype)
        {
            _archetype = archetype;

            Faction = Faction.Hostile;
            DisplayName = archetype.DisplayName;

            Health?.Configure(archetype.Health);
            Stamina?.Configure(archetype.Stamina);

            _maxPoise = Mathf.Max(1f, archetype.Poise);
            _poise = _maxPoise;
            _poiseRegen = archetype.PoiseRegenPerSecond;
            _resistance = Mathf.Clamp01(archetype.DamageResistance);
        }

        /// <summary>
        /// Swaps the archetype's combat numbers without touching current health or
        /// stamina. Used by a boss changing phase - re-running Configure would
        /// heal it to full, which is not a phase change, it is a reset.
        /// </summary>
        public void Retune(EnemyArchetype archetype)
        {
            if (archetype == null) return;

            _archetype = archetype;

            // Raise the ceiling without refilling: a boss that gets tougher keeps
            // the damage it has already taken.
            Health?.SetMax(archetype.Health, preserveRatio: false);

            _maxPoise = Mathf.Max(1f, archetype.Poise);
            _poise = Mathf.Min(_poise, _maxPoise);
            _poiseRegen = archetype.PoiseRegenPerSecond;
            _resistance = Mathf.Clamp01(archetype.DamageResistance);
        }

        /// <summary>Restores the stagger budget. Used to make a beat uninterruptible.</summary>
        public void RefillPoise() => _poise = _maxPoise;

        protected override float ModifyIncomingDamage(float amount, in DamageInfo info)
        {
            return amount * (1f - _resistance);
        }

        protected override HitResult EvaluateDefence(in DamageInfo info)
        {
            if (info.Unblockable || !IsBlocking) return HitResult.Hit;

            Vector3 toAttacker = MathUtil.FlatDirection(-info.Direction);
            if (toAttacker.sqrMagnitude < 0.0001f) return HitResult.Hit;

            // Enemies have a narrower guard than the player: flanking them works.
            return Vector3.Angle(transform.forward, toAttacker) <= 60f
                ? HitResult.Blocked
                : HitResult.Hit;
        }

        /// <summary>
        /// Filters staggers through the poise budget. A knockdown or launch always
        /// lands; anything smaller has to spend poise, and only breaking it
        /// actually interrupts.
        /// </summary>
        public override void ApplyStagger(float duration, Vector3 direction, HitImpact impact)
        {
            if (!IsAlive) return;

            bool unstoppable = impact == HitImpact.Knockdown || impact == HitImpact.Launch;

            if (!unstoppable && _maxPoise > 0f)
            {
                _poise -= PoiseCost(impact);
                _lastPoiseDamageAt = Time.time;

                if (_poise > 0f)
                {
                    // Absorbed. The body still visibly reacts, but the enemy keeps
                    // control - which is the readable signal that it has armour.
                    Vector3 local = transform.InverseTransformDirection(direction.normalized);
                    Animator?.AddImpulse(local, 0.8f + (int)impact * 0.4f);
                    return;
                }

                _poise = 0f;
                PoiseBroken?.Invoke();
            }

            base.ApplyStagger(duration, direction, impact);
        }

        private static float PoiseCost(HitImpact impact)
        {
            switch (impact)
            {
                case HitImpact.Light: return 9f;
                case HitImpact.Medium: return 18f;
                case HitImpact.Heavy: return 42f;
                default: return 60f;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (!IsAlive || _poise >= _maxPoise) return;

            // Poise only comes back after a breather, so sustained pressure keeps
            // an enemy suppressed even if no single hit breaks them.
            if (Time.time - _lastPoiseDamageAt < 1.1f) return;
            _poise = Mathf.Min(_maxPoise, _poise + _poiseRegen * Time.deltaTime);
        }

        protected override void OnBlocked(in DamageInfo info)
        {
            Stamina stamina = Stamina;
            if (stamina == null || !stamina.IsExhausted) return;

            // Out of stamina: the guard collapses and the enemy is wide open.
            _guardUp = false;
            _poise = 0f;
            base.ApplyStagger(1.1f, -transform.forward, HitImpact.Medium);
            Animator?.PlayAction(CombatPoses.GuardBreak);
            CombatFeedback.GuardBroken(info.Point);
        }

        protected override void HandleDeath()
        {
            _guardUp = false;
            base.HandleDeath();

            CombatFeedback.Death(AimPoint);

            int xp = _archetype?.ExperienceReward ?? 10;
            int money = _archetype?.MoneyReward ?? 5;
            DiedWithRewards?.Invoke(this, xp, money);
        }

        /// <summary>Restores the enemy to full fighting condition. Used by spawners and pooling.</summary>
        public void ResetForReuse()
        {
            Health?.Revive();
            Stamina?.Refill();
            _poise = _maxPoise;
            _guardUp = false;
            Release();
            Animator?.ResetPose();
            Animator?.SetStance(CharacterStance.Relaxed);
        }
    }
}
