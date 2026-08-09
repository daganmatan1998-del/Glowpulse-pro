using Glowpulse.Core;
using Glowpulse.Core.Characters;
using Glowpulse.Core.Movement;
using UnityEngine;

namespace Glowpulse.Combat
{
    /// <summary>
    /// A punchbag that fights back just enough to be useful: it takes hits,
    /// reacts, gets knocked down, gets up, dies and then resets.
    ///
    /// It exists so the combat system can be tuned and verified before the enemy
    /// AI arrives, and it stays in the proving ground afterwards as the place to
    /// check frame data and hit reactions in isolation.
    /// </summary>
    public sealed class TrainingDummy : Combatant
    {
        [Header("Dummy")]
        [SerializeField] private float _maxHealth = 120f;

        [Tooltip("Seconds after dying before the dummy stands back up ready to go again.")]
        [SerializeField] private float _respawnDelay = 3f;

        [Tooltip("When set, the dummy blocks anything that arrives from the front.")]
        [SerializeField] private bool _blocksAttacks;

        [Tooltip("Fraction of incoming damage ignored, standing in for enemy armour.")]
        [SerializeField, Range(0f, 0.9f)] private float _damageResistance;

        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private float _reviveAt = float.PositiveInfinity;

        /// <summary>Total damage this dummy has absorbed since its last reset.</summary>
        public float DamageTaken { get; private set; }

        public bool BlocksAttacks
        {
            get => _blocksAttacks;
            set => _blocksAttacks = value;
        }

        protected override void Awake()
        {
            base.Awake();

            Faction = Faction.Hostile;
            DisplayName = "Training Dummy";

            Health?.Configure(_maxHealth);
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
        }

        public override bool IsBlocking => _blocksAttacks && IsAlive && !IsDown;

        protected override HitResult EvaluateDefence(in DamageInfo info)
        {
            if (!IsBlocking || info.Unblockable) return HitResult.Hit;

            Vector3 toAttacker = MathUtil.FlatDirection(-info.Direction);
            if (toAttacker.sqrMagnitude < 0.0001f) return HitResult.Hit;

            return Vector3.Angle(transform.forward, toAttacker) <= 70f
                ? HitResult.Blocked
                : HitResult.Hit;
        }

        protected override void OnHitTaken(in DamageInfo info, float appliedDamage, bool killed)
        {
            DamageTaken += appliedDamage;
            if (killed) CombatFeedback.Death(AimPoint);
        }

        protected override void HandleDeath()
        {
            base.HandleDeath();
            _reviveAt = Time.time + _respawnDelay;
            CombatFeedback.Knockdown(transform.position);
        }

        protected override void Update()
        {
            base.Update();

            if (IsAlive || Time.time < _reviveAt) return;
            Reset();
        }

        /// <summary>Puts the dummy back on its feet at its starting spot.</summary>
        public void Reset()
        {
            _reviveAt = float.PositiveInfinity;
            DamageTaken = 0f;

            Health?.Revive();
            Animator?.ResetPose();
            Animator?.SetStance(CharacterStance.Combat);

            CharacterMotor motor = Motor;
            if (motor != null) motor.Teleport(_homePosition, _homeRotation);
            else transform.SetPositionAndRotation(_homePosition, _homeRotation);
        }

        protected override float ModifyIncomingDamage(float amount, in DamageInfo info)
        {
            return amount * (1f - _damageResistance);
        }
    }
}
