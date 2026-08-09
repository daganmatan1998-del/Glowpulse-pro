using System;
using Glowpulse.Combat;
using Glowpulse.Core;
using Glowpulse.Core.Characters;
using Glowpulse.Core.InputSystem;
using Glowpulse.Core.Movement;
using UnityEngine;

namespace Glowpulse.Player
{
    /// <summary>What the player's body is currently committed to.</summary>
    public enum PlayerState
    {
        Locomotion = 0,
        Dodging = 1,
        Airborne = 2,
        Attacking = 3,
        Staggered = 4,
        Downed = 5,
        Dead = 6
    }

    /// <summary>
    /// Player movement and the state machine that gates it. Combat states are
    /// declared here but driven by the combat system in the next phase, so that
    /// attacks and movement never disagree about who owns the character.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    [DefaultExecutionOrder(-50)]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerLocomotionConfig _config;

        [Tooltip("Transform whose yaw defines 'forward' for movement input. Usually the camera.")]
        [SerializeField] private Transform _cameraTransform;

        private CharacterMotor _motor;
        private PlayerCombatant _combatant;
        private ICharacterAnimator _animator;

        private PlayerState _state = PlayerState.Locomotion;
        private float _stateTimer;

        private Vector3 _planarVelocity;
        private float _currentYaw;
        private float _previousYaw;
        private float _turnRate;

        private InputBuffer _jumpBuffer = new InputBuffer(0.16f);
        private InputBuffer _dodgeBuffer = new InputBuffer(0.2f);
        private float _dodgeReadyAt;
        private Vector3 _dodgeDirection;
        private bool _jumpCut;
        private bool _sprinting;

        private float _stateLockUntil;

        /// <summary>Raised whenever the movement state changes. Useful for UI and audio.</summary>
        public event Action<PlayerState, PlayerState> StateChanged;

        public event Action Jumped;
        public event Action<Vector3> Dodged;
        public event Action<float> Landed;

        public PlayerState State => _state;
        public CharacterMotor Motor => _motor;
        public PlayerLocomotionConfig Config => _config != null ? _config : PlayerLocomotionConfig.Default;

        /// <summary>Horizontal speed in metres per second.</summary>
        public float Speed => _planarVelocity.magnitude;

        /// <summary>0..1 where 1 is full sprint. Drives HUD and animation.</summary>
        public float SpeedNormalized => Mathf.Clamp01(Speed / Config.MaxGroundSpeed);

        public bool IsSprinting => _sprinting;
        public bool IsGrounded => _motor != null && _motor.IsGrounded;
        public bool IsDodging => _state == PlayerState.Dodging;

        /// <summary>True while the player cannot start a new action.</summary>
        public bool IsBusy => _state == PlayerState.Dodging || _state == PlayerState.Staggered
                              || _state == PlayerState.Downed || _state == PlayerState.Dead
                              || Time.time < _stateLockUntil;

        /// <summary>Target the player is locked onto, or null. Set by the lock-on system.</summary>
        public ITargetable LockTarget { get; set; }

        public bool HasLockTarget => LockTarget != null && LockTarget.IsTargetable;

        /// <summary>Set by the combat system while an attack owns the character.</summary>
        public bool CombatOwnsMovement { get; set; }

        public Transform CameraTransform
        {
            get => _cameraTransform;
            set => _cameraTransform = value;
        }

        private void Awake()
        {
            _motor = GetComponent<CharacterMotor>();
            _combatant = GetComponent<PlayerCombatant>();
            _animator = GetComponent<ICharacterAnimator>();

            PlayerLocomotionConfig c = Config;
            _motor.Configure(c.Height, c.Radius, c.StepOffset, c.SlopeLimit, c.Gravity, c.FallMultiplier);

            _jumpBuffer.Window = c.JumpBuffer;
            _currentYaw = transform.eulerAngles.y;
            _previousYaw = _currentYaw;
        }

        private void Start()
        {
            if (_cameraTransform == null && UnityEngine.Camera.main != null)
                _cameraTransform = UnityEngine.Camera.main.transform;

            if (_combatant != null)
            {
                _combatant.Staggered += HandleStaggered;
                _combatant.KnockedDown += HandleKnockedDown;
                _combatant.Recovered += HandleRecovered;
                if (_combatant.Health != null) _combatant.Health.Died += HandleDied;
            }
        }

        private void OnDestroy()
        {
            if (_combatant == null) return;
            _combatant.Staggered -= HandleStaggered;
            _combatant.KnockedDown -= HandleKnockedDown;
            _combatant.Recovered -= HandleRecovered;
            if (_combatant.Health != null) _combatant.Health.Died -= HandleDied;
        }

        public void BindAnimator(ICharacterAnimator animator) => _animator = animator;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            IInputProvider input = InputService.Current;

            _jumpBuffer.Feed(input.JumpPressed);
            _dodgeBuffer.Feed(input.DodgePressed);
            ApplyVariableJumpHeight(input);

            _stateTimer += dt;

            switch (_state)
            {
                case PlayerState.Dodging:
                    TickDodge(dt);
                    break;
                case PlayerState.Staggered:
                case PlayerState.Downed:
                case PlayerState.Dead:
                    TickIncapacitated(dt);
                    break;
                default:
                    TickGroundedOrAir(input, dt);
                    break;
            }

            UpdateTurnRate(dt);
            PushAnimatorState();
        }

        // ---- states -------------------------------------------------------------

        private void TickGroundedOrAir(IInputProvider input, float dt)
        {
            PlayerLocomotionConfig c = Config;

            Vector3 wish = ResolveMoveDirection(input.Move);
            bool wantsToMove = wish.sqrMagnitude > 0.0001f;

            UpdateSprint(input, wantsToMove, dt);

            float targetSpeed = ResolveTargetSpeed(input.Move, wish, c);
            if (CombatOwnsMovement) targetSpeed = 0f;

            Vector3 targetVelocity = wish * targetSpeed;

            float accel = wantsToMove && targetSpeed > 0.01f ? c.Acceleration : c.Deceleration;
            if (!_motor.IsGrounded) accel *= c.AirControl;

            _planarVelocity = Vector3.MoveTowards(_planarVelocity, targetVelocity, accel * dt);

            // Dodge takes priority over jumping so a panic input always evades.
            if (!CombatOwnsMovement && TryStartDodge(wish)) return;
            if (!CombatOwnsMovement) TryJump(c);

            ApplyRotation(wish, dt);

            _motor.Tick(_planarVelocity, dt);

            if (_motor.JustLanded) OnLanded();

            SetState(_motor.IsGrounded ? PlayerState.Locomotion : PlayerState.Airborne);
        }

        private void TickDodge(float dt)
        {
            PlayerLocomotionConfig c = Config;
            float t = Mathf.Clamp01(_stateTimer / c.DodgeDuration);

            // The curve gives the dodge its burst: fast out of the gate, then a
            // rapid taper so it ends crisply instead of drifting.
            float speed = (c.DodgeDistance / c.DodgeDuration) * c.DodgeSpeedCurve.Evaluate(t);
            _planarVelocity = _dodgeDirection * speed;

            _motor.Tick(_planarVelocity, dt);

            // Face the dodge direction, except while locked on where the player
            // keeps facing the enemy so a side-roll reads as a sidestep.
            Vector3 face = HasLockTarget ? DirectionToTarget() : _dodgeDirection;
            ApplyRotationImmediate(face, c.LockedTurnSpeed, dt);

            if (_stateTimer >= c.DodgeDuration)
            {
                _planarVelocity = _dodgeDirection * (c.DodgeDistance / c.DodgeDuration) * 0.25f;
                _dodgeReadyAt = Time.time + c.DodgeCooldown;
                SetState(PlayerState.Locomotion);
            }
        }

        private void TickIncapacitated(float dt)
        {
            _planarVelocity = Vector3.MoveTowards(_planarVelocity, Vector3.zero, Config.Deceleration * 2f * dt);
            _motor.Tick(_planarVelocity, dt);
        }

        // ---- movement helpers -----------------------------------------------------

        /// <summary>Converts stick input into a world direction relative to the camera.</summary>
        private Vector3 ResolveMoveDirection(Vector2 move)
        {
            if (move.sqrMagnitude < 0.0001f) return Vector3.zero;

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;

            if (_cameraTransform != null)
            {
                forward = MathUtil.FlatDirection(_cameraTransform.forward);
                // Looking straight down leaves no usable forward vector; fall back
                // to the camera's up vector projected on the ground.
                if (forward.sqrMagnitude < 0.01f) forward = MathUtil.FlatDirection(_cameraTransform.up);
                right = MathUtil.FlatDirection(_cameraTransform.right);
            }

            Vector3 dir = forward * move.y + right * move.x;
            return dir.sqrMagnitude > 1f ? dir.normalized : dir;
        }

        private float ResolveTargetSpeed(Vector2 rawInput, Vector3 wish, PlayerLocomotionConfig c)
        {
            if (wish.sqrMagnitude < 0.0001f) return 0f;

            float magnitude = Mathf.Clamp01(rawInput.magnitude);

            if (_sprinting) return c.SprintSpeed * magnitude;

            // Locked on, the player strafes: full speed forwards, slower sideways
            // and backwards, which is what keeps circling an enemy readable.
            if (HasLockTarget)
            {
                Vector3 toTarget = DirectionToTarget();
                float alignment = Vector3.Dot(wish.normalized, toTarget);
                float speed = Mathf.Lerp(c.StrafeSpeed, c.JogSpeed, Mathf.InverseLerp(0.2f, 0.9f, alignment));
                return speed * magnitude;
            }

            float walkBlend = Mathf.InverseLerp(0.25f, 0.85f, magnitude);
            return Mathf.Lerp(c.WalkSpeed, c.JogSpeed, walkBlend);
        }

        private void UpdateSprint(IInputProvider input, bool moving, float dt)
        {
            Stamina stamina = _combatant != null ? _combatant.Stamina : null;

            bool wants = input.SprintHeld && moving && !CombatOwnsMovement;

            // Starting a sprint needs a real reserve; keeping it going only needs
            // a trickle, so the player is not kicked out on the very first frame.
            if (!_sprinting && wants && stamina != null)
                wants = !stamina.IsExhausted && stamina.Current >= Config.SprintStaminaThreshold;

            if (wants && stamina != null && !stamina.Drain(Config.SprintStaminaPerSecond, dt))
                wants = false;

            _sprinting = wants;
        }

        private void ApplyRotation(Vector3 wish, float dt)
        {
            PlayerLocomotionConfig c = Config;

            if (HasLockTarget && !CombatOwnsMovement)
            {
                ApplyRotationImmediate(DirectionToTarget(), c.LockedTurnSpeed, dt);
                return;
            }

            if (wish.sqrMagnitude < 0.0001f) return;
            ApplyRotationImmediate(wish, c.TurnSpeed, dt);
        }

        private void ApplyRotationImmediate(Vector3 direction, float degreesPerSecond, float dt)
        {
            if (direction.sqrMagnitude < 0.0001f) return;

            float target = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            _currentYaw = Mathf.MoveTowardsAngle(_currentYaw, target, degreesPerSecond * dt);
            transform.rotation = Quaternion.Euler(0f, _currentYaw, 0f);
        }

        private Vector3 DirectionToTarget()
        {
            if (!HasLockTarget) return transform.forward;
            Vector3 d = MathUtil.FlatDirection(LockTarget.Transform.position - transform.position);
            return d.sqrMagnitude < 0.0001f ? transform.forward : d;
        }

        private void UpdateTurnRate(float dt)
        {
            float delta = Mathf.DeltaAngle(_previousYaw, _currentYaw);
            _turnRate = MathUtil.Damp(_turnRate, delta / Mathf.Max(dt, 0.0001f), 12f, dt);
            _previousYaw = _currentYaw;
        }

        // ---- actions ----------------------------------------------------------------

        private void TryJump(PlayerLocomotionConfig c)
        {
            bool canJump = _motor.IsGrounded || _motor.TimeSinceGrounded <= c.CoyoteTime;
            if (!canJump || !_jumpBuffer.Pending) return;

            Stamina stamina = _combatant != null ? _combatant.Stamina : null;
            if (stamina != null && !stamina.TrySpend(c.JumpStaminaCost)) return;

            _jumpBuffer.Consume();
            _jumpCut = false;
            _motor.Jump(c.JumpHeight);
            _animator?.PlayAction(PoseLibrary.JumpTakeoff);
            SetState(PlayerState.Airborne);
            Jumped?.Invoke();
        }

        private bool TryStartDodge(Vector3 wish)
        {
            if (!_dodgeBuffer.Pending) return false;
            if (Time.time < _dodgeReadyAt) return false;
            if (!_motor.IsGrounded) return false;

            PlayerLocomotionConfig c = Config;
            Stamina stamina = _combatant != null ? _combatant.Stamina : null;
            if (stamina != null && !stamina.TrySpend(c.DodgeStaminaCost)) return false;

            _dodgeBuffer.Consume();

            // With no directional input the player rolls backwards away from the
            // threat, which is the read most players expect from a panic dodge.
            Vector3 dir = wish;
            if (dir.sqrMagnitude < 0.0001f)
                dir = HasLockTarget ? -DirectionToTarget() : -transform.forward;

            _dodgeDirection = dir.normalized;
            _planarVelocity = Vector3.zero;

            if (_combatant != null && _combatant.Health != null)
                _combatant.Health.GrantInvulnerability(c.DodgeInvulnerabilityDelay + c.DodgeInvulnerability);

            // A roll reads well when moving forwards; sideways and backwards
            // evades look better as a quick step.
            bool forwardish = !HasLockTarget || Vector3.Dot(_dodgeDirection, DirectionToTarget()) > 0.3f;
            string clipId = forwardish ? PoseLibrary.DodgeRoll : PoseLibrary.DodgeStep;

            // Play the clip at whatever rate makes it finish exactly with the dodge.
            PoseClip clip = PoseLibrary.Get(clipId);
            float playbackSpeed = clip != null ? clip.Duration / c.DodgeDuration : 1f;
            _animator?.PlayAction(clipId, playbackSpeed);

            SetState(PlayerState.Dodging);
            Dodged?.Invoke(_dodgeDirection);
            return true;
        }

        /// <summary>
        /// Releasing the jump button while still rising cuts the ascent short, so
        /// a tap gives a hop and a hold gives the full arc. Applied once per jump.
        /// </summary>
        private void ApplyVariableJumpHeight(IInputProvider input)
        {
            if (_jumpCut || _motor.IsGrounded || _motor.VerticalVelocity <= 0f) return;
            if (input.JumpHeld) return;

            _jumpCut = true;
            _motor.DampenJump(1f / Mathf.Max(1f, Config.LowJumpMultiplier));
        }

        private void OnLanded()
        {
            float speed = _motor.LastLandingSpeed;
            _jumpCut = false;

            if (speed > Config.HardLandingSpeed * 0.45f)
                _animator?.PlayAction(PoseLibrary.JumpLand,
                    Mathf.Clamp(speed / Config.HardLandingSpeed, 0.7f, 1.6f));

            Landed?.Invoke(speed);
        }

        // ---- external state changes ----------------------------------------------------

        private void HandleStaggered(float duration)
        {
            if (_state == PlayerState.Dead) return;
            _planarVelocity = Vector3.zero;
            _stateLockUntil = Time.time + duration;
            SetState(PlayerState.Staggered);
        }

        private void HandleKnockedDown(float duration)
        {
            if (_state == PlayerState.Dead) return;
            _planarVelocity = Vector3.zero;
            _stateLockUntil = Time.time + duration;
            SetState(PlayerState.Downed);
        }

        private void HandleRecovered()
        {
            if (_state == PlayerState.Dead) return;
            _stateLockUntil = 0f;
            SetState(PlayerState.Locomotion);
        }

        private void HandleDied()
        {
            _planarVelocity = Vector3.zero;
            _sprinting = false;
            SetState(PlayerState.Dead);
        }

        /// <summary>Puts the player back on their feet at a location, e.g. after a respawn.</summary>
        public void Respawn(Vector3 position, Quaternion rotation)
        {
            _motor.Teleport(position, rotation);
            _planarVelocity = Vector3.zero;
            _currentYaw = rotation.eulerAngles.y;
            _previousYaw = _currentYaw;
            _stateLockUntil = 0f;
            _jumpBuffer.Clear();
            _dodgeBuffer.Clear();
            _animator?.ResetPose();
            SetState(PlayerState.Locomotion);
        }

        private void SetState(PlayerState next)
        {
            if (_state == next) return;
            PlayerState previous = _state;
            _state = next;
            _stateTimer = 0f;
            StateChanged?.Invoke(previous, next);
        }

        private void PushAnimatorState()
        {
            if (_animator == null) return;

            Vector3 local = transform.InverseTransformDirection(_planarVelocity);
            _animator.SetLocomotion(local, _motor.IsGrounded, _motor.VerticalVelocity);
            _animator.SetTurnRate(_turnRate);

            CharacterStance stance;
            switch (_state)
            {
                case PlayerState.Dead:
                    stance = CharacterStance.Dead;
                    break;
                case PlayerState.Downed:
                    stance = CharacterStance.Downed;
                    break;
                default:
                    stance = HasLockTarget ? CharacterStance.Combat : CharacterStance.Relaxed;
                    break;
            }

            // The combat system may be holding a guard; it overrides the stance.
            if (_combatant != null && _combatant.IsBlocking && stance == CharacterStance.Combat)
                stance = CharacterStance.Guarding;

            _animator.SetStance(stance);
            _animator.SetLookTarget(HasLockTarget ? LockTarget.Transform : null);
        }
    }
}
