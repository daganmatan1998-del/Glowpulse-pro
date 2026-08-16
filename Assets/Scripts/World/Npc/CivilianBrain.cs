using Glowpulse.AI;
using Glowpulse.Core;
using Glowpulse.Core.Characters;
using Glowpulse.Core.Movement;
using UnityEngine;

namespace Glowpulse.World.Npc
{
    /// <summary>How often a civilian is allowed to think. Set by the crowd director.</summary>
    public enum CivilianDetail
    {
        /// <summary>Close to the player: thinks every frame.</summary>
        Full = 0,

        /// <summary>Mid distance: a few times a second.</summary>
        Reduced = 1,

        /// <summary>Far away: barely, but still walking.</summary>
        Distant = 2
    }

    /// <summary>
    /// One pedestrian. Walks the pavement graph, stops to look at things, and gets
    /// out of the way when a fight starts.
    ///
    /// Deliberately much cheaper than <see cref="Enemies.EnemyBrain"/>: no
    /// perception raycasts, no target acquisition, no combat. Danger arrives as a
    /// push from <see cref="CrowdDirector"/> rather than being discovered, which
    /// is what lets a street hold dozens of these without the AI showing up in a
    /// profile at all.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    public sealed class CivilianBrain : MonoBehaviour
    {
        private static readonly Collider[] Bumps = new Collider[4];

        [SerializeField] private float _walkSpeed = 1.5f;
        [SerializeField] private float _hurrySpeed = 2.4f;
        [SerializeField] private float _fleeSpeed = 4.6f;
        [SerializeField] private float _turnSpeed = 420f;

        private CharacterMotor _motor;
        private CivilianCombatant _combatant;
        private ICharacterAnimator _animator;

        private AiStateMachine<CivilianBrain> _machine;
        private System.Random _rng;

        private PedestrianNetwork _network;
        private int _currentNode = -1;
        private int _previousNode = -1;
        private int _targetNode = -1;

        private Vector3 _desiredVelocity;
        private Vector3 _facing;
        private bool _facingSet;

        private float _thinkBudget;
        private float _detailInterval;

        private Vector3 _threatPoint;
        private float _threatUntil;
        private float _threatSeverity;

        private LayerMask _characterMask;
        private LayerMask _obstacleMask;
        private LayerMask _playerMask;

        // ---- identity ------------------------------------------------------------

        public PedestrianNetwork Network => _network;
        public System.Random Rng => _rng;
        public ICharacterAnimator Animator => _animator;
        public CivilianCombatant Combatant => _combatant;
        public AiStateMachine<CivilianBrain> Machine => _machine;

        public CivilianDetail Detail { get; private set; } = CivilianDetail.Full;

        public float WalkSpeed => _walkSpeed;
        public float HurrySpeed => _hurrySpeed;
        public float FleeSpeed => _fleeSpeed;

        /// <summary>Where the civilian is heading, in world space.</summary>
        public Vector3 TargetPoint { get; private set; }

        public bool HasRoute => _targetNode >= 0;

        public bool IsAlive => _combatant == null || _combatant.IsAlive;

        /// <summary>True while a stagger or knockdown has taken control away.</summary>
        public bool IsReacting => _combatant != null && (_combatant.IsReacting || _combatant.IsDown);

        // ---- danger --------------------------------------------------------------

        public bool HasThreat => Time.time < _threatUntil;

        public Vector3 ThreatPoint => _threatPoint;

        /// <summary>How alarming the nearest trouble is, 0 to 1.</summary>
        public float ThreatSeverity => HasThreat ? _threatSeverity : 0f;

        public float ThreatDistance => HasThreat
            ? Vector3.Distance(MathUtil.Flat(transform.position), MathUtil.Flat(_threatPoint))
            : float.MaxValue;

        /// <summary>
        /// Told about trouble. Only the closest current threat is kept - a civilian
        /// running from two fights at once looks confused, and running from the
        /// nearer one is the right answer anyway.
        /// </summary>
        public void Alarm(Vector3 position, float severity, float duration)
        {
            float distance = Vector3.Distance(transform.position, position);

            if (HasThreat && distance > ThreatDistance && severity <= _threatSeverity) return;

            _threatPoint = position;
            _threatSeverity = Mathf.Clamp01(severity);
            _threatUntil = Mathf.Max(_threatUntil, Time.time + duration);

            if (_machine == null) return;
            if (_machine.Is(CivilianStates.Stroll) || _machine.Is(CivilianStates.Pause))
                _machine.Change(CivilianStates.Startled);
        }

        public void ClearAlarm() => _threatUntil = 0f;

        // ---- lifecycle -----------------------------------------------------------

        private void Awake()
        {
            _motor = GetComponent<CharacterMotor>();
            _combatant = GetComponent<CivilianCombatant>();
            _animator = GetComponent<ICharacterAnimator>();
            _machine = new AiStateMachine<CivilianBrain>(this);

            _characterMask = GameLayers.CharacterMask;
            _obstacleMask = GameLayers.WorldMask;
            _playerMask = GameLayers.PlayerMask;

            CivilianPoses.EnsureRegistered();
        }

        /// <summary>
        /// Places the civilian on the network and gives it a personality. The seed
        /// drives everything random about them, so a recycled body walking a
        /// different beat is genuinely a different person.
        /// </summary>
        public void Configure(PedestrianNetwork network, int startNode, int seed,
            ICharacterAnimator animator = null)
        {
            _network = network;
            if (animator != null) _animator = animator;

            _rng = new System.Random(seed);

            _walkSpeed = Lerp(1.15f, 1.75f);
            _hurrySpeed = _walkSpeed + Lerp(0.6f, 1.1f);
            _fleeSpeed = Lerp(4.1f, 5.2f);
            _turnSpeed = Lerp(360f, 520f);

            _currentNode = startNode;
            _previousNode = -1;
            _targetNode = -1;
            ClearAlarm();

            _animator?.SetStance(CharacterStance.Relaxed);
            _machine.ChangeNow(CivilianStates.Stroll);
        }

        private float Lerp(float a, float b) => Mathf.Lerp(a, b, (float)_rng.NextDouble());

        /// <summary>Sets how often this civilian is allowed to think.</summary>
        public void SetDetail(CivilianDetail detail)
        {
            Detail = detail;
            _detailInterval = detail switch
            {
                CivilianDetail.Full => 0f,
                CivilianDetail.Reduced => 1f / 12f,
                _ => 0.25f
            };
        }

        private void Update()
        {
            if (_network == null || _rng == null) return;

            // Thinking is throttled but movement is not skipped: the accumulated
            // time is handed to the tick, so a distant civilian covers the same
            // ground as a near one, just in coarser steps.
            _thinkBudget += Time.deltaTime;
            if (_thinkBudget < _detailInterval) return;

            float dt = _thinkBudget;
            _thinkBudget = 0f;
            if (dt <= 0f) return;

            _desiredVelocity = Vector3.zero;
            _facingSet = false;

            if (IsReacting)
            {
                // Being knocked about overrides whatever they were doing. The
                // combatant owns the recovery; we just stop steering.
                _motor.Tick(Vector3.zero, dt);
                PushAnimatorState();
                return;
            }

            _machine.Tick(dt);

            CheckForBarge();
            ApplyMovement(dt);
            PushAnimatorState();
        }

        // ---- movement ------------------------------------------------------------

        public void SetDesiredVelocity(Vector3 velocity) => _desiredVelocity = velocity;

        public void Stand() => _desiredVelocity = Vector3.zero;

        public void FaceTowards(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            _facing = MathUtil.FlatDirection(direction);
            _facingSet = true;
        }

        public void MoveTowardsTarget(float speed)
        {
            if (_targetNode < 0) return;
            _desiredVelocity = Steering.Arrive(transform.position, TargetPoint, speed, 1.2f, 0.35f);
        }

        /// <summary>Metres from the current destination, ignoring height.</summary>
        public float DistanceToTarget()
        {
            if (_targetNode < 0) return float.MaxValue;
            return Vector3.Distance(MathUtil.Flat(transform.position), MathUtil.Flat(TargetPoint));
        }

        /// <summary>
        /// Picks the next node to stroll to. Returns false only when the network
        /// has nowhere to go, which the crowd director treats as a reason to
        /// recycle this civilian somewhere more useful.
        /// </summary>
        public bool ChooseNextStop()
        {
            if (_network == null || _network.NodeCount == 0) return false;

            if (_currentNode < 0) _currentNode = _network.Nearest(transform.position);
            if (_currentNode < 0) return false;

            // Keep walking the way they were already walking - that bias is what
            // stops a stroll from turning into a shuffle back and forth.
            Vector3 heading = _previousNode >= 0
                ? _network.NodePosition(_currentNode) - _network.NodePosition(_previousNode)
                : transform.forward;

            int next = _network.NextStep(_currentNode, _previousNode, heading, _rng);
            if (next < 0) return false;

            _previousNode = _currentNode;
            _currentNode = next;
            SetTarget(next);
            return true;
        }

        /// <summary>Heads for whichever neighbour is furthest from the trouble.</summary>
        public bool ChooseEscape()
        {
            if (_network == null) return false;

            if (_currentNode < 0) _currentNode = _network.Nearest(transform.position);
            if (_currentNode < 0) return false;

            int next = _network.StepAwayFrom(_currentNode, _threatPoint);
            if (next < 0) return false;

            _previousNode = _currentNode;
            _currentNode = next;
            SetTarget(next);
            return true;
        }

        /// <summary>
        /// True when there is nowhere to run: every neighbour is closer to the
        /// trouble than where they already stand. That is when a person stops
        /// running and covers up.
        /// </summary>
        public bool IsCornered()
        {
            if (_network == null || _currentNode < 0) return true;
            return _network.StepAwayFrom(_currentNode, _threatPoint) < 0;
        }

        private void SetTarget(int node)
        {
            _targetNode = node;

            // Standing exactly on a node would put every civilian on the same
            // metre of pavement at every corner, so each keeps a personal offset.
            Vector3 jitter = new Vector3(
                (float)_rng.NextDouble() - 0.5f, 0f, (float)_rng.NextDouble() - 0.5f) * 1.1f;

            TargetPoint = _network.NodePosition(node) + jitter;
        }

        private void ApplyMovement(float dt)
        {
            Vector3 velocity = Steering.Resolve(transform, _desiredVelocity,
                Mathf.Max(_fleeSpeed, _hurrySpeed), 1.1f, 1.3f,
                _characterMask, _obstacleMask, 1.6f);

            _motor.Tick(velocity, dt);

            if (!_facingSet)
            {
                if (velocity.sqrMagnitude > 0.02f) _facing = MathUtil.FlatDirection(velocity);
                else return;
            }

            Quaternion desired = Quaternion.LookRotation(_facing, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, _turnSpeed * dt);
        }

        private void PushAnimatorState()
        {
            if (_animator == null) return;
            Vector3 local = transform.InverseTransformDirection(_motor.PlanarVelocity);
            _animator.SetLocomotion(local, _motor.IsGrounded, _motor.VerticalVelocity);
        }

        /// <summary>
        /// Notices somebody running through them. Detected from the civilian's side
        /// so the player's controller does not need to know pedestrians exist.
        /// </summary>
        private void CheckForBarge()
        {
            if (_combatant == null || Detail != CivilianDetail.Full) return;

            int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.9f, 0.75f,
                Bumps, _playerMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                var motor = Bumps[i].GetComponentInParent<CharacterMotor>();
                if (motor == null) continue;

                Vector3 impact = motor.PlanarVelocity;
                if (impact.sqrMagnitude < 16f) continue; // Below a run, it is just a nudge.

                _combatant.Shove(transform.position - motor.transform.position, impact.magnitude * 0.6f);
                Alarm(motor.transform.position, 0.5f, 2.5f);
                return;
            }
        }
    }
}
