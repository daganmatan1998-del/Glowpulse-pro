using System;
using Glowpulse.AI;
using Glowpulse.Combat;
using Glowpulse.Core;
using Glowpulse.Core.Characters;
using Glowpulse.Core.Movement;
using UnityEngine;

namespace Glowpulse.Enemies
{
    /// <summary>
    /// An enemy's mind and body: perception, movement, attack execution, and the
    /// state machine that decides between them.
    ///
    /// The brain owns everything shared across states - where the target is, how
    /// to walk somewhere, how to throw a punch - and the states in
    /// <see cref="EnemyStates"/> only decide *what* to do. States are shared
    /// singletons, so all the per-enemy data lives here.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    [RequireComponent(typeof(EnemyCombatant))]
    [DefaultExecutionOrder(-30)]
    public sealed class EnemyBrain : MonoBehaviour, ICombatParticipant, IAlertable
    {
        [Header("Perception")]
        [Tooltip("Seconds between perception checks. Staggered per enemy to spread the cost.")]
        [SerializeField] private float _perceptionInterval = 0.18f;

        [Tooltip("Seconds the enemy keeps hunting after losing sight of the target.")]
        [SerializeField] private float _memoryDuration = 7f;

        [Header("Steering")]
        [SerializeField] private float _separationRadius = 1.5f;
        [SerializeField] private float _separationStrength = 3.4f;
        [SerializeField] private float _obstacleProbeDistance = 1.7f;

        [Header("Patrol")]
        [Tooltip("Radius around the spawn point the enemy wanders while unaware.")]
        [SerializeField] private float _patrolRadius = 6f;

        [SerializeField] private float _patrolPauseMin = 1.5f;
        [SerializeField] private float _patrolPauseMax = 4f;

        private EnemyArchetype _archetype;
        private CharacterMotor _motor;
        private EnemyCombatant _combatant;
        private ICharacterAnimator _animator;

        private AiStateMachine<EnemyBrain> _machine;
        private readonly AttackRunner _runner = new AttackRunner();

        private Vector3 _desiredVelocity;
        private Vector3 _facing;
        private bool _facingSet;

        private ITargetable _target;
        private float _nextPerceptionAt;
        private float _lastSeenAt = float.NegativeInfinity;
        private Vector3 _lastKnownPosition;
        private bool _canSee;

        private float _nextAttackAt;
        private int _orbitDirection = 1;
        private float _nextOrbitFlipAt;

        private LayerMask _characterMask;
        private LayerMask _obstacleMask;

        /// <summary>Raised on every state transition. Useful for debugging and for audio.</summary>
        public event Action<string, string> StateChanged;

        // ---- accessors used by states ------------------------------------------

        public EnemyArchetype Archetype => _archetype;
        public EnemyCombatant Combatant => _combatant;
        public CharacterMotor Motor => _motor;
        public ICharacterAnimator Animator => _animator;
        public AttackRunner Runner => _runner;
        public AiStateMachine<EnemyBrain> Machine => _machine;

        public Vector3 HomePosition { get; private set; }
        public Vector3 PatrolTarget { get; set; }
        public float PatrolPauseUntil { get; set; }

        public ITargetable Target => _target != null && _target.IsTargetable ? _target : null;
        public bool HasTarget => Target != null;
        public bool CanSeeTarget => _canSee && HasTarget;
        public Vector3 LastKnownTargetPosition => _lastKnownPosition;
        public float TimeSinceSeen => Time.time - _lastSeenAt;

        /// <summary>True while the enemy still believes there is something to fight.</summary>
        public bool RemembersTarget => HasTarget && TimeSinceSeen < _memoryDuration;

        public float DistanceToTarget => HasTarget
            ? MathUtil.FlatDistance(transform.position, Target.Transform.position)
            : float.MaxValue;

        public bool IsAttacking => _runner.IsRunning;
        public bool AttackReady => Time.time >= _nextAttackAt;
        public int OrbitDirection => _orbitDirection;

        // ---- ICombatParticipant --------------------------------------------------

        public Transform Transform => transform;
        public bool IsAlive => _combatant != null && _combatant.IsAlive;

        public bool IsEngaged => IsAlive && _machine != null
                                 && (_machine.Is(EnemyStates.Combat)
                                     || _machine.Is(EnemyStates.Chase)
                                     || _machine.Is(EnemyStates.Attack));

        public float AttackUrgency => HasTarget
            ? Mathf.Clamp01(1f - DistanceToTarget / Mathf.Max(1f, _archetype.PreferredRange * 2f))
            : 0f;

        public float FlankPreference => _archetype?.FlankPreference ?? 0f;

        // ---- lifecycle ---------------------------------------------------------------

        private void Awake()
        {
            _motor = GetComponent<CharacterMotor>();
            _combatant = GetComponent<EnemyCombatant>();
            _animator = GetComponent<ICharacterAnimator>();

            _characterMask = GameLayers.CharacterMask;
            _obstacleMask = GameLayers.WorldMask;

            _machine = new AiStateMachine<EnemyBrain>(this);
            _machine.Changed += (from, to) => StateChanged?.Invoke(from, to);

            HomePosition = transform.position;
            PatrolTarget = HomePosition;

            // Spread perception checks across frames so a crowd does not all
            // raycast on the same one.
            _nextPerceptionAt = Time.time + UnityEngine.Random.value * _perceptionInterval;

            _runner.Landed += HandleAttackLanded;
        }

        public void Configure(EnemyArchetype archetype, ICharacterAnimator animator)
        {
            _archetype = archetype;
            if (animator != null) _animator = animator;
            _combatant.Configure(archetype);

            // The archetype carries the difficulty scaling, so every move this
            // enemy throws inherits it without the move table knowing anything
            // about difficulty.
            _runner.DamageMultiplier = Mathf.Max(0.05f, archetype.DamageMultiplier);
        }

        private void OnEnable()
        {
            _combatant.Staggered += HandleStaggered;
            _combatant.KnockedDown += HandleKnockedDown;
            _combatant.Recovered += HandleRecovered;
            _combatant.Died += HandleDied;

            CombatDirector.Instance?.Register(this);
        }

        private void OnDisable()
        {
            _combatant.Staggered -= HandleStaggered;
            _combatant.KnockedDown -= HandleKnockedDown;
            _combatant.Recovered -= HandleRecovered;
            _combatant.Died -= HandleDied;

            CombatDirector.Instance?.Unregister(this);
        }

        private void Start()
        {
            if (_archetype == null) Configure(EnemyArchetype.Brawler(), _animator);
            _machine.ChangeNow(EnemyStates.Idle);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (!IsAlive)
            {
                if (!_machine.Is(EnemyStates.Dead)) _machine.ChangeNow(EnemyStates.Dead);
                return;
            }

            // A grabbed enemy is being puppeted by whoever holds them.
            if (_combatant.IsGrabbed)
            {
                _desiredVelocity = Vector3.zero;
                return;
            }

            UpdatePerception();

            _desiredVelocity = Vector3.zero;
            _facingSet = false;

            _machine.Tick(dt);

            ApplyMovement(dt);
            PushAnimatorState(dt);
        }

        // ---- perception -----------------------------------------------------------------

        private void UpdatePerception()
        {
            if (Time.time < _nextPerceptionAt) return;
            _nextPerceptionAt = Time.time + _perceptionInterval;

            if (_target == null || !_target.IsTargetable)
                _target = TargetRegistry.Nearest(transform.position, _archetype.SightRange * 1.4f,
                    Faction.Hostile);

            if (_target == null)
            {
                _canSee = false;
                return;
            }

            _canSee = EvaluateSight(_target);
            if (!_canSee) return;

            _lastSeenAt = Time.time;
            _lastKnownPosition = _target.Transform.position;
        }

        private bool EvaluateSight(ITargetable target)
        {
            Vector3 eye = transform.position + Vector3.up * 1.55f;
            Vector3 to = target.AimPoint - eye;
            float distance = to.magnitude;

            if (distance > _archetype.SightRange) return false;

            // Very close counts as noticed regardless of facing - you feel someone
            // standing behind you.
            bool withinCone = distance < 2.2f ||
                              Vector3.Angle(transform.forward, MathUtil.FlatDirection(to))
                              <= _archetype.SightHalfAngle;
            if (!withinCone) return false;

            return !Physics.Raycast(eye, to / distance, distance - 0.3f,
                GameLayers.SightBlockerMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Told about trouble by an ally or a loud noise.</summary>
        public void Alert(Vector3 position)
        {
            if (!IsAlive) return;

            _lastKnownPosition = position;
            _lastSeenAt = Time.time;

            if (_target == null)
                _target = TargetRegistry.Nearest(position, 6f, Faction.Hostile);

            if (_machine.Is(EnemyStates.Idle) || _machine.Is(EnemyStates.Patrol))
                _machine.Change(EnemyStates.Alert);
        }

        // ---- movement, driven by states -----------------------------------------------------

        /// <summary>Requests a world-space velocity for this frame.</summary>
        public void SetDesiredVelocity(Vector3 velocity) => _desiredVelocity = velocity;

        /// <summary>Requests a facing direction for this frame.</summary>
        public void FaceTowards(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            _facing = MathUtil.FlatDirection(direction);
            _facingSet = true;
        }

        public void FaceTarget()
        {
            if (!HasTarget) return;
            FaceTowards(Target.Transform.position - transform.position);
        }

        /// <summary>Heads for a point, easing off as it arrives.</summary>
        public void MoveTo(Vector3 point, float speed)
        {
            SetDesiredVelocity(Steering.Arrive(transform.position, point, speed));
        }

        /// <summary>Circles the target at its preferred range.</summary>
        public void Circle(float speed)
        {
            if (!HasTarget) return;

            if (Time.time >= _nextOrbitFlipAt)
            {
                // Reversing occasionally stops a ring of enemies from turning into
                // a carousel all going the same way.
                _nextOrbitFlipAt = Time.time + UnityEngine.Random.Range(2.5f, 6f);
                if (UnityEngine.Random.value < 0.4f) _orbitDirection = -_orbitDirection;
            }

            SetDesiredVelocity(Steering.Orbit(transform.position, Target.Transform.position,
                _archetype.PreferredRange, speed, _orbitDirection));
        }

        /// <summary>Backs away from the target while continuing to face it.</summary>
        public void BackAway(float speed)
        {
            if (!HasTarget) return;
            Vector3 away = MathUtil.FlatDirection(transform.position - Target.Transform.position);
            SetDesiredVelocity(away * speed);
        }

        private void ApplyMovement(float dt)
        {
            Vector3 velocity = Steering.Resolve(transform, _desiredVelocity,
                Mathf.Max(_archetype.ChaseSpeed, _archetype.StrafeSpeed),
                _separationRadius, _separationStrength,
                _characterMask, _obstacleMask, _obstacleProbeDistance);

            _motor.Tick(velocity, dt);

            if (!_facingSet)
            {
                // Nothing asked for a facing, so look where we are going.
                if (velocity.sqrMagnitude > 0.04f) _facing = MathUtil.FlatDirection(velocity);
                else return;
            }

            float turn = _archetype.TurnSpeed * dt;
            Quaternion desired = Quaternion.LookRotation(_facing, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, turn);
        }

        private void PushAnimatorState(float dt)
        {
            if (_animator == null) return;

            Vector3 local = transform.InverseTransformDirection(_motor.PlanarVelocity);
            _animator.SetLocomotion(local, _motor.IsGrounded, _motor.VerticalVelocity);

            CharacterStance stance;
            if (!IsAlive) stance = CharacterStance.Dead;
            else if (_combatant.IsDown) stance = CharacterStance.Downed;
            else if (_combatant.IsBlocking) stance = CharacterStance.Guarding;
            else if (RemembersTarget) stance = CharacterStance.Combat;
            else stance = CharacterStance.Relaxed;

            _animator.SetStance(stance);
            _animator.SetLookTarget(RemembersTarget && HasTarget ? Target.Transform : null);
        }

        // ---- attacking ---------------------------------------------------------------------

        /// <summary>
        /// Starts a move if the director allows it and stamina permits. The
        /// director's permission is what keeps a crowd from swinging at once.
        /// </summary>
        public bool TryBeginAttack(string moveId = null)
        {
            if (IsAttacking || !AttackReady || !HasTarget) return false;

            MoveSet moves = _archetype.Moves;
            AttackDefinition move = moveId != null ? moves.Get(moveId) : ChooseMove(moves);
            if (move == null) return false;

            CombatDirector director = CombatDirector.Instance;
            if (director != null && !director.RequestAttack(this)) return false;

            Stamina stamina = _combatant.Stamina;
            if (stamina != null && move.StaminaCost > 0f && !stamina.TrySpend(move.StaminaCost))
            {
                director?.ReleaseToken(this);
                return false;
            }

            _combatant.GuardUp = false;
            _runner.Begin(move);
            AttackRunner.PlayClip(_animator, move);
            FaceTarget();
            return true;
        }

        /// <summary>Picks a move based on range: the reach of the move has to fit the gap.</summary>
        private AttackDefinition ChooseMove(MoveSet moves)
        {
            float distance = DistanceToTarget;
            AttackDefinition best = null;
            float bestFit = float.MaxValue;

            System.Collections.Generic.IReadOnlyList<AttackDefinition> candidates = moves.All;
            for (int i = 0; i < candidates.Count; i++)
            {
                AttackDefinition candidate = candidates[i];
                float reach = candidate.HitboxOffset.z + candidate.HitboxRadius
                              + candidate.LungeDistance + candidate.LungeTrackingBonus;
                if (reach < distance) continue;

                // Prefer the move whose reach is the tightest fit for the gap, so
                // a charge is saved for a target that is actually far away.
                float fit = reach - distance;
                if (fit >= bestFit) continue;

                bestFit = fit;
                best = candidate;
            }

            return best;
        }

        /// <summary>Advances the running move. Returns true while it is still going.</summary>
        public bool TickAttack(float dt)
        {
            if (!_runner.IsRunning) return false;

            Vector3 lunge = _runner.Tick(dt, transform, _combatant.Faction, gameObject, Target);
            SetDesiredVelocity(lunge);

            // Track the target through the wind-up, then commit.
            if (_runner.Time < _runner.Move.ActiveStart) FaceTarget();

            if (!_runner.Finished) return true;

            FinishAttack();
            return false;
        }

        public void FinishAttack()
        {
            _runner.Cancel();
            _nextAttackAt = Time.time + _archetype.RollAttackCooldown();
            CombatDirector.Instance?.ReleaseToken(this);
        }

        public void CancelAttack()
        {
            if (_runner.IsRunning) _runner.Cancel();
            CombatDirector.Instance?.ReleaseToken(this);
        }

        /// <summary>Delays the next attack, e.g. after being staggered.</summary>
        public void DelayNextAttack(float seconds)
        {
            _nextAttackAt = Mathf.Max(_nextAttackAt, Time.time + seconds);
        }

        private void HandleAttackLanded(AttackDefinition move, Combatant victim, HitResult result,
            DamageInfo info)
        {
            // Landing a hit tells nearby allies the fight is on.
            if (result == HitResult.Hit || result == HitResult.Killed)
                CombatDirector.Instance?.RaiseAlarm(transform.position, _archetype.HearingRange);
        }

        // ---- reactions ------------------------------------------------------------------------

        private void HandleStaggered(float duration)
        {
            CancelAttack();
            DelayNextAttack(duration + 0.25f);
            _combatant.GuardUp = false;
            _machine.ChangeNow(EnemyStates.React);
        }

        private void HandleKnockedDown(float duration)
        {
            CancelAttack();
            DelayNextAttack(duration + 0.6f);
            _combatant.GuardUp = false;
            _machine.ChangeNow(EnemyStates.React);
        }

        private void HandleRecovered()
        {
            if (!IsAlive) return;
            _machine.ChangeNow(EnemyStates.Recover);
        }

        private void HandleDied(Combatant combatant)
        {
            CancelAttack();
            _machine.ChangeNow(EnemyStates.Dead);
            CombatDirector.Instance?.Unregister(this);
        }

        // ---- patrol helpers ----------------------------------------------------------------------

        /// <summary>Picks a new wander destination near home that is actually reachable.</summary>
        public Vector3 PickPatrolPoint()
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector3 candidate = MathUtil.RandomPointInRing(HomePosition, 1.5f, _patrolRadius);
                if (!MathUtil.GroundPoint(candidate, out Vector3 grounded, 4f, 12f, _obstacleMask))
                    continue;

                Vector3 toPoint = grounded - transform.position;
                float distance = MathUtil.Flat(toPoint).magnitude;
                if (distance < 0.5f) continue;

                if (Physics.Raycast(transform.position + Vector3.up, MathUtil.FlatDirection(toPoint),
                        distance, _obstacleMask, QueryTriggerInteraction.Ignore)) continue;

                return grounded;
            }

            return HomePosition;
        }

        public float RollPatrolPause() => UnityEngine.Random.Range(_patrolPauseMin, _patrolPauseMax);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.4f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(Application.isPlaying ? HomePosition : transform.position, _patrolRadius);

            if (_archetype == null) return;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, _archetype.PreferredRange);
        }
    }
}
