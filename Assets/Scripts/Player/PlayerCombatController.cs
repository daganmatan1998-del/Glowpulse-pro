using System;
using System.Collections.Generic;
using Glowpulse.Combat;
using Glowpulse.Core;
using Glowpulse.Core.Characters;
using Glowpulse.Core.InputSystem;
using Glowpulse.Core.Movement;
using UnityEngine;

namespace Glowpulse.Player
{
    /// <summary>What the combat system is currently doing.</summary>
    public enum CombatState
    {
        Ready = 0,
        Attacking = 1,
        Holding = 2
    }

    /// <summary>
    /// Drives the player's offence: attack strings, combo chaining, grabs and
    /// throws, counters and finishers.
    ///
    /// Runs just before <see cref="PlayerController"/> so the velocity and facing
    /// an attack wants are already set when the character moves for the frame.
    /// The two never both drive the body: while an attack is running the movement
    /// controller yields via <see cref="PlayerController.BeginCombatAction"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerCombatant))]
    [DefaultExecutionOrder(-60)]
    public sealed class PlayerCombatController : MonoBehaviour
    {
        [Header("Targeting")]
        [Tooltip("Radius searched for an auto-facing target when nothing is locked on.")]
        [SerializeField] private float _autoFaceRadius = 4.5f;

        [Tooltip("Half-angle of the cone searched for an auto-facing target.")]
        [SerializeField, Range(20f, 180f)] private float _autoFaceHalfAngle = 100f;

        [Tooltip("Degrees per second the body turns onto its target during an attack's wind-up.")]
        [SerializeField] private float _attackTurnSpeed = 720f;

        [Header("Counters")]
        [Tooltip("Seconds after a parry or a clean dodge during which a counter can be thrown.")]
        [SerializeField] private float _counterWindow = 1.1f;

        [Header("Grappling")]
        [Tooltip("Seconds a grabbed enemy is held before they break free.")]
        [SerializeField] private float _grabHoldDuration = 3.2f;

        [Tooltip("Distance in front of the player a held enemy is planted.")]
        [SerializeField] private float _grabHoldDistance = 1.05f;

        [SerializeField] private float _grabPunchDamage = 12f;
        [SerializeField] private float _grabPunchInterval = 0.38f;

        [Header("Finishers")]
        [Tooltip("Distance at which a downed enemy can be finished.")]
        [SerializeField] private float _finisherRange = 1.9f;

        private PlayerController _player;
        private PlayerCombatant _combatant;
        private CharacterMotor _motor;
        private ICharacterAnimator _animator;

        private MoveSet _moveSet;

        // The shared timeline runner: identical hit resolution for the player and
        // every enemy, so a move behaves the same whoever throws it.
        private readonly AttackRunner _runner = new AttackRunner();

        // A separate probe for the single-target queries grabs and finishers make,
        // which must not disturb the running swing's hit memory.
        private readonly MeleeHitbox _probe = new MeleeHitbox();
        private readonly List<ITargetable> _facingCandidates = new List<ITargetable>(8);

        private CombatState _state = CombatState.Ready;

        private InputBuffer _lightBuffer = new InputBuffer(0.24f);
        private InputBuffer _heavyBuffer = new InputBuffer(0.24f);
        private InputBuffer _grabBuffer = new InputBuffer(0.2f);
        private AttackDefinition _queued;

        private float _counterUntil = float.NegativeInfinity;

        private Combatant _held;
        private float _grabEndsAt;
        private float _nextGrabPunchAt;

        private float _damageMultiplier = 1f;
        private int _comboCount;
        private float _comboExpiresAt;

        /// <summary>Raised when a move starts, for HUD and audio.</summary>
        public event Action<AttackDefinition> AttackStarted;

        /// <summary>Raised for each victim a move connects with.</summary>
        public event Action<AttackDefinition, Combatant, HitResult> AttackLanded;

        /// <summary>Current combo length and the time it expires, for the HUD counter.</summary>
        public event Action<int> ComboChanged;

        public CombatState State => _state;
        public AttackDefinition CurrentMove => _runner.Move;
        public bool IsAttacking => _state == CombatState.Attacking;
        public bool IsHolding => _state == CombatState.Holding;
        public Combatant HeldEnemy => _held;
        public int ComboCount => _comboCount;

        /// <summary>True while a counter attack is available.</summary>
        public bool CounterReady => Time.time < _counterUntil;

        /// <summary>Damage scaling from the progression system.</summary>
        public float DamageMultiplier
        {
            get => _damageMultiplier;
            set
            {
                _damageMultiplier = Mathf.Max(0.1f, value);
                _runner.DamageMultiplier = _damageMultiplier;
            }
        }

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _combatant = GetComponent<PlayerCombatant>();
            _motor = GetComponent<CharacterMotor>();
            _animator = GetComponent<ICharacterAnimator>();

            CombatPoses.EnsureRegistered();
            _moveSet = MoveSet.Player();
            _runner.Landed += HandleLanded;
        }

        private void OnEnable()
        {
            _combatant.Parried += HandleParried;
            _combatant.Evaded += HandleEvaded;
            _combatant.Staggered += HandleInterrupted;
            _combatant.KnockedDown += HandleInterrupted;
        }

        private void OnDisable()
        {
            _combatant.Parried -= HandleParried;
            _combatant.Evaded -= HandleEvaded;
            _combatant.Staggered -= HandleInterrupted;
            _combatant.KnockedDown -= HandleInterrupted;

            ReleaseHold();
        }

        public void BindAnimator(ICharacterAnimator animator) => _animator = animator;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            IInputProvider input = InputService.Current;

            _lightBuffer.Feed(input.LightAttackPressed);
            _heavyBuffer.Feed(input.HeavyAttackPressed);
            _grabBuffer.Feed(input.GrabPressed);

            UpdateGuard(input);
            UpdateCombo();

            if (!_combatant.IsAlive)
            {
                Abort();
                return;
            }

            switch (_state)
            {
                case CombatState.Attacking:
                    TickAttack(dt);
                    break;
                case CombatState.Holding:
                    TickHold(dt);
                    break;
                default:
                    TryStartSomething();
                    break;
            }
        }

        // ---- guard --------------------------------------------------------------

        private void UpdateGuard(IInputProvider input)
        {
            // The guard can only be raised while free: mid-attack blocking would
            // remove the risk that makes committing to a heavy meaningful.
            bool canGuard = _player.CanAct && _state == CombatState.Ready && _motor.IsGrounded;
            _combatant.GuardHeld = canGuard && input.BlockHeld;
        }

        private void UpdateCombo()
        {
            if (_comboCount <= 0 || Time.time < _comboExpiresAt) return;
            _comboCount = 0;
            ComboChanged?.Invoke(0);
        }

        // ---- starting moves --------------------------------------------------------

        private void TryStartSomething()
        {
            if (!_player.CanAct) return;

            bool light = _lightBuffer.Pending;
            bool heavy = _heavyBuffer.Pending;

            if (_grabBuffer.Pending && TryStart(_moveSet.Get(_moveSet.GrabMove)))
            {
                _grabBuffer.Consume();
                return;
            }

            if (!light && !heavy) return;

            // A parry or a clean dodge converts the next attack into a counter,
            // which is the reward loop the defensive options are built around.
            if (CounterReady)
            {
                AttackDefinition counter = _moveSet.Get(_moveSet.CounterMove);
                if (TryStart(counter))
                {
                    _counterUntil = float.NegativeInfinity;
                    ConsumeAttackInput(light);
                    return;
                }
            }

            // Heavy on a downed enemy in reach is a finisher, not a normal swing.
            if (heavy && TryStartFinisher())
            {
                _heavyBuffer.Consume();
                return;
            }

            AttackDefinition next = _moveSet.Resolve(null, heavy);
            if (TryStart(next)) ConsumeAttackInput(light);
        }

        private void ConsumeAttackInput(bool light)
        {
            if (light) _lightBuffer.Consume();
            else _heavyBuffer.Consume();
        }

        private bool TryStartFinisher()
        {
            AttackDefinition finisher = _moveSet.Get(_moveSet.FinisherMove);
            if (finisher == null) return false;

            AttackDefinition probe = finisher.Clone();
            probe.HitboxRadius = _finisherRange;

            Combatant victim = _probe.QuerySingle(transform, probe, _combatant.Faction, requireDowned: true);
            if (victim == null) return false;

            // Stand over them before the animation plays, so the kick connects.
            Vector3 toVictim = MathUtil.FlatDirection(victim.transform.position - transform.position);
            if (toVictim.sqrMagnitude > 0.0001f) _player.SnapFacing(toVictim);

            return TryStart(finisher);
        }

        private bool TryStart(AttackDefinition move)
        {
            if (move == null) return false;

            Stamina stamina = _combatant.Stamina;
            if (stamina != null && move.StaminaCost > 0f && !stamina.TrySpend(move.StaminaCost))
                return false;

            _runner.Begin(move);
            _queued = null;
            _state = CombatState.Attacking;

            _combatant.GuardHeld = false;
            _player.BeginCombatAction();

            FaceAttackTarget(instant: true);
            AttackRunner.PlayClip(_animator, move);

            stamina?.BlockRegen(move.Duration + 0.25f);
            AttackStarted?.Invoke(move);
            return true;
        }

        // ---- running a move ---------------------------------------------------------

        private void TickAttack(float dt)
        {
            AttackDefinition move = _runner.Move;
            if (move == null)
            {
                EndAttack();
                return;
            }

            // The runner advances the timeline, opens the hitbox, resolves hits
            // and reports the lunge; everything left here is player-specific.
            Vector3 lunge = _runner.Tick(dt, transform, _combatant.Faction, gameObject, _player.LockTarget);
            _player.SetCombatVelocity(lunge);

            // Keep tracking through the wind-up so a moving enemy cannot simply
            // walk out of a committed swing.
            if (_runner.Time < move.ActiveEnd) FaceAttackTarget(instant: false, dt: dt);

            QueueFollowUp(move);

            // Dodging out of the recovery frames is the escape hatch that keeps
            // committing to a heavy from feeling like a trap.
            if (_runner.InRecovery && InputService.Current.DodgePressed)
            {
                EndAttack();
                return;
            }

            if (!_runner.Finished) return;

            if (_queued != null)
            {
                AttackDefinition next = _queued;
                _queued = null;
                if (TryStart(next)) return;
            }

            EndAttack();
        }

        private void HandleLanded(AttackDefinition move, Combatant victim, HitResult result,
            DamageInfo info)
        {
            if (result == HitResult.Hit || result == HitResult.Killed) RegisterComboHit();
            AttackLanded?.Invoke(move, victim, result);
        }

        private void QueueFollowUp(AttackDefinition move)
        {
            if (_queued != null || !_runner.InComboWindow) return;

            bool heavy = _heavyBuffer.Pending;
            bool light = _lightBuffer.Pending;
            if (!heavy && !light) return;

            // Heavy wins a tie: it is the deliberate, expensive input.
            AttackDefinition next = _moveSet.Resolve(move, heavy);
            if (next == null) return;

            Stamina stamina = _combatant.Stamina;
            if (stamina != null && next.StaminaCost > 0f && !stamina.CanSpend(next.StaminaCost)) return;

            _queued = next;
            if (heavy) _heavyBuffer.Consume();
            else _lightBuffer.Consume();
        }

        private void EndAttack()
        {
            _state = CombatState.Ready;
            _runner.Cancel();
            _queued = null;
            _player.SetCombatVelocity(Vector3.zero);
            _player.EndCombatAction();
        }

        // ---- combo counter -------------------------------------------------------------

        private void RegisterComboHit()
        {
            _comboCount++;
            _comboExpiresAt = Time.time + 2.4f;
            ComboChanged?.Invoke(_comboCount);
        }

        // ---- grappling -------------------------------------------------------------------

        /// <summary>Called from the grab move's active frames to try to seize someone.</summary>
        private bool TrySeize(AttackDefinition move)
        {
            Combatant victim = _probe.QuerySingle(transform, move, _combatant.Faction);
            if (victim == null || !victim.CanBeGrabbed) return false;

            _held = victim;
            victim.Grabbed(transform);

            _state = CombatState.Holding;
            _grabEndsAt = Time.time + _grabHoldDuration;
            _nextGrabPunchAt = Time.time + 0.2f;

            _animator?.PlayAction(CombatPoses.GrabHold, 1f, 0.1f);
            _player.BeginCombatAction();
            _player.SetCombatVelocity(Vector3.zero);
            return true;
        }

        private void TickHold(float dt)
        {
            if (_held == null || !_held.IsAlive || !_held.IsGrabbed)
            {
                ReleaseHold();
                return;
            }

            // Plant the victim in front and keep both facing each other.
            Vector3 anchor = transform.position + transform.forward * _grabHoldDistance;
            _held.transform.position = Vector3.Lerp(_held.transform.position, anchor, 1f - Mathf.Exp(-18f * dt));
            _held.transform.rotation = Quaternion.LookRotation(-transform.forward, Vector3.up);

            _player.SetCombatVelocity(Vector3.zero);

            IInputProvider input = InputService.Current;

            if (_heavyBuffer.Pending || _grabBuffer.Pending)
            {
                _heavyBuffer.Clear();
                _grabBuffer.Clear();
                PerformThrow();
                return;
            }

            if (_lightBuffer.Pending && Time.time >= _nextGrabPunchAt)
            {
                _lightBuffer.Consume();
                _nextGrabPunchAt = Time.time + _grabPunchInterval;
                PunchHeld();
                return;
            }

            if (input.DodgePressed || Time.time >= _grabEndsAt) ReleaseHold();
        }

        private void PunchHeld()
        {
            Vector3 point = _held.AimPoint;
            Vector3 direction = transform.forward;

            DamageInfo info = DamageInfo.Create(_grabPunchDamage * _damageMultiplier, HitImpact.Light,
                point, direction, gameObject, _combatant.Faction);
            info.Unblockable = true;
            info.AttackId = "punch";

            HitResult result = _held.ApplyDamage(in info);
            _animator?.PlayAction(CombatPoses.LightCross, 1.4f, 0.03f);

            AttackDefinition grab = _moveSet.Get(_moveSet.GrabMove);
            if (grab != null) CombatFeedback.Landed(grab, in info, result, point, true);

            RegisterComboHit();
            if (!_held.IsAlive) ReleaseHold();
        }

        private void PerformThrow()
        {
            AttackDefinition move = _moveSet.Get(_moveSet.ThrowMove);
            Combatant victim = _held;
            if (move == null || victim == null)
            {
                ReleaseHold();
                return;
            }

            Vector3 direction = transform.forward;
            Vector3 point = victim.AimPoint;

            victim.Release();
            _held = null;

            DamageInfo info = move.BuildDamage(gameObject, _combatant.Faction, point, direction,
                _damageMultiplier);
            HitResult result = victim.ApplyDamage(in info);

            victim.ApplyStagger(1.9f, direction, HitImpact.Knockdown);

            // A throw is mostly horizontal with enough lift to read as airborne.
            victim.ApplyKnockback(direction * info.KnockbackForce + Vector3.up * 3.4f);

            CombatFeedback.Landed(move, in info, result, point, isPlayerAttacker: true);
            AttackLanded?.Invoke(move, victim, result);
            RegisterComboHit();

            _animator?.PlayAction(move.ClipId, 1f, 0.04f);

            // The throw was resolved by hand, so the runner picks the move up in
            // recovery and simply plays out the rest of the animation.
            _runner.SkipToRecovery(move);
            _state = CombatState.Attacking;
        }

        private void ReleaseHold()
        {
            if (_held != null)
            {
                _held.Release();
                _held = null;
            }

            if (_state != CombatState.Holding) return;

            _state = CombatState.Ready;
            _animator?.StopAction();
            _player.SetCombatVelocity(Vector3.zero);
            _player.EndCombatAction();
        }

        // ---- reactions ------------------------------------------------------------------

        private void HandleParried(Combatant attacker)
        {
            _counterUntil = Time.time + _counterWindow;
        }

        private void HandleEvaded(Combatant attacker)
        {
            // Only a dodge earns a counter. Standing invulnerability does not.
            if (_player.IsDodging) _counterUntil = Time.time + _counterWindow;
        }

        private void HandleInterrupted(float duration) => Abort();

        private void Abort()
        {
            if (_held != null)
            {
                _held.Release();
                _held = null;
            }

            if (_state == CombatState.Ready) return;

            _state = CombatState.Ready;
            _runner.Cancel();
            _queued = null;
            _comboCount = 0;
            ComboChanged?.Invoke(0);

            _player.SetCombatVelocity(Vector3.zero);
            _player.EndCombatAction();
        }

        // ---- grab hook -------------------------------------------------------------------

        private void LateUpdate()
        {
            // The grab's active frames are checked after the move has advanced, so
            // the seize happens on the same frame the hitbox would have connected.
            AttackDefinition move = _runner.Move;
            if (_state != CombatState.Attacking || move == null) return;
            if (move.Kind != AttackKind.Grab) return;
            if (!move.IsActiveAt(_runner.Time)) return;

            TrySeize(move);
        }

        // ---- targeting -----------------------------------------------------------------------

        private void FaceAttackTarget(bool instant, float dt = 0f)
        {
            Vector3 direction = ResolveFacing();
            if (direction.sqrMagnitude < 0.0001f) return;

            if (instant) _player.SnapFacing(direction);
            else _player.FaceDirection(direction, _attackTurnSpeed, dt);
        }

        /// <summary>
        /// Where an attack should point. The lock-on target wins; otherwise the
        /// nearest enemy inside a forward cone is picked, so attacks connect
        /// without the player having to aim precisely.
        /// </summary>
        private Vector3 ResolveFacing()
        {
            ITargetable locked = _player.LockTarget;
            if (locked != null && locked.IsTargetable)
                return MathUtil.FlatDirection(locked.Transform.position - transform.position);

            Combatant nearest = FindAutoFaceTarget();
            if (nearest != null)
                return MathUtil.FlatDirection(nearest.transform.position - transform.position);

            // No enemy nearby: attack along the movement input if there is any,
            // otherwise straight ahead.
            Vector2 move = InputService.Current.Move;
            if (move.sqrMagnitude > 0.04f && _player.CameraTransform != null)
            {
                Vector3 forward = MathUtil.FlatDirection(_player.CameraTransform.forward);
                Vector3 right = MathUtil.FlatDirection(_player.CameraTransform.right);
                return MathUtil.FlatDirection(forward * move.y + right * move.x);
            }

            return transform.forward;
        }

        private Combatant FindAutoFaceTarget()
        {
            List<ITargetable> candidates = _facingCandidates;
            TargetRegistry.Query(transform.position, _autoFaceRadius, _combatant.Faction, candidates);
            if (candidates.Count == 0) return null;

            float cosLimit = Mathf.Cos(_autoFaceHalfAngle * Mathf.Deg2Rad);
            Combatant best = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                Vector3 to = candidates[i].Transform.position - transform.position;
                Vector3 flat = MathUtil.FlatDirection(to);
                if (flat.sqrMagnitude < 0.0001f) continue;

                float alignment = Vector3.Dot(transform.forward, flat);
                if (alignment < cosLimit) continue;

                float score = MathUtil.Flat(to).magnitude * (2f - alignment);
                if (score >= bestScore) continue;

                bestScore = score;
                best = candidates[i] as Combatant;
            }

            return best;
        }

        private void OnDrawGizmosSelected()
        {
            if (_runner.Move != null) MeleeHitbox.DrawGizmo(transform, _runner.Move);
        }
    }
}
