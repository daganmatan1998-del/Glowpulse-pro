using System;
using Glowpulse.Combat;
using Glowpulse.Core;
using Glowpulse.Core.Characters;
using UnityEngine;

namespace Glowpulse.Player
{
    /// <summary>
    /// The player's combat identity: stats, and the defensive half of the combat
    /// system - blocking, perfect blocks and guard breaks.
    ///
    /// Defence lives here rather than in the combat controller because it has to
    /// resolve inside <see cref="Combatant.ApplyDamage"/>, on the attacker's
    /// frame, before any damage is applied.
    /// </summary>
    public sealed class PlayerCombatant : Combatant
    {
        [Header("Base stats")]
        [SerializeField] private float _baseHealth = 140f;
        [SerializeField] private float _baseStamina = 110f;

        [Header("Guard")]
        [Tooltip("Seconds after raising the guard during which a block becomes a parry.")]
        [SerializeField] private float _parryWindow = 0.19f;

        [Tooltip("Attacks arriving outside this half-angle from straight ahead cannot be blocked.")]
        [SerializeField, Range(30f, 180f)] private float _guardArc = 115f;

        [Tooltip("Seconds the guard is unusable after it is broken.")]
        [SerializeField] private float _guardBreakRecovery = 1.35f;

        private float _guardRaisedAt = float.NegativeInfinity;
        private float _guardBrokenUntil = float.NegativeInfinity;
        private bool _guardHeld;

        /// <summary>Raised on a perfect block, carrying the attacker that was parried.</summary>
        public event Action<Combatant> Parried;

        /// <summary>Raised when a hit is absorbed on the guard.</summary>
        public event Action<DamageInfo> BlockedHit;

        /// <summary>Raised when the guard is broken by accumulated stamina damage.</summary>
        public event Action GuardBroken;

        /// <summary>Raised when an attack passes harmlessly through dodge i-frames.</summary>
        public event Action<Combatant> Evaded;

        public float ParryWindow
        {
            get => _parryWindow;
            set => _parryWindow = Mathf.Max(0f, value);
        }

        public bool GuardAvailable => Time.time >= _guardBrokenUntil;

        /// <summary>Set each frame by the combat controller from the block input.</summary>
        public bool GuardHeld
        {
            get => _guardHeld;
            set
            {
                if (value && !_guardHeld) _guardRaisedAt = Time.time;
                _guardHeld = value;
            }
        }

        public override bool IsBlocking =>
            _guardHeld && GuardAvailable && IsAlive && !IsDown && !IsReacting;

        /// <summary>True while a block would count as a perfect block.</summary>
        public bool InParryWindow => IsBlocking && Time.time - _guardRaisedAt <= _parryWindow;

        protected override void Awake()
        {
            base.Awake();

            Faction = Faction.Player;
            DisplayName = "Player";

            Health?.Configure(_baseHealth);
            Stamina?.Configure(_baseStamina);
        }

        /// <summary>Applies progression bonuses in a single call.</summary>
        public void ApplyStatBonuses(float bonusHealth, float bonusStamina)
        {
            Health?.SetMax(_baseHealth + bonusHealth, preserveRatio: true);
            Stamina?.SetMax(_baseStamina + bonusStamina, preserveRatio: true);
        }

        // ---- defence -------------------------------------------------------------

        protected override HitResult EvaluateDefence(in DamageInfo info)
        {
            if (info.Unblockable || !IsBlocking) return HitResult.Hit;

            // You cannot guard what you cannot see. The incoming direction points
            // away from the attacker, so it is negated to find where they stand.
            Vector3 toAttacker = MathUtil.FlatDirection(-info.Direction);
            if (toAttacker.sqrMagnitude > 0.0001f)
            {
                float angle = Vector3.Angle(transform.forward, toAttacker);
                if (angle > _guardArc * 0.5f) return HitResult.Hit;
            }

            // Not enough stamina to absorb the blow: the guard collapses under it.
            Stamina stamina = Stamina;
            if (stamina != null && stamina.IsExhausted) return HitResult.Hit;

            return InParryWindow ? HitResult.Parried : HitResult.Blocked;
        }

        protected override void OnBlocked(in DamageInfo info)
        {
            BlockedHit?.Invoke(info);

            // The stamina cost was already applied by the base class, so an empty
            // bar here means this hit is what emptied it.
            Stamina stamina = Stamina;
            if (stamina == null || !stamina.IsExhausted) return;

            BreakGuard(info.Point);
        }

        protected override void OnParried(in DamageInfo info)
        {
            // A parry costs the attacker their whole turn - that is the reward
            // for the tight timing.
            Combatant attacker = info.Attacker != null
                ? info.Attacker.GetComponentInParent<Combatant>()
                : null;

            if (attacker != null && attacker != this)
            {
                Vector3 push = MathUtil.FlatDirection(attacker.transform.position - transform.position);
                attacker.ApplyStagger(0.85f, push, HitImpact.Medium);
                attacker.ApplyKnockback(push * 2.4f);
            }

            Animator?.PlayAction(CombatPoses.ParryDeflect);
            Stamina?.Restore(14f);

            Parried?.Invoke(attacker);
        }

        protected override void OnEvaded(in DamageInfo info)
        {
            Combatant attacker = info.Attacker != null
                ? info.Attacker.GetComponentInParent<Combatant>()
                : null;

            Evaded?.Invoke(attacker);
        }

        private void BreakGuard(Vector3 point)
        {
            _guardHeld = false;
            _guardBrokenUntil = Time.time + _guardBreakRecovery;

            ApplyStagger(0.62f, -transform.forward, HitImpact.Medium);
            Animator?.PlayAction(CombatPoses.GuardBreak);
            CombatFeedback.GuardBroken(point);
            GuardBroken?.Invoke();
        }

        protected override void HandleDeath()
        {
            _guardHeld = false;
            base.HandleDeath();
            CombatFeedback.Death(transform.position);
        }
    }
}
