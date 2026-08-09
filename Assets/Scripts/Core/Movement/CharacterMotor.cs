using UnityEngine;

namespace Glowpulse.Core.Movement
{
    /// <summary>
    /// Thin, reusable movement layer over <see cref="CharacterController"/>.
    /// It owns gravity, ground detection, slope projection and externally applied
    /// impulses (knockback, dodge lunges) so that neither the player controller
    /// nor the enemy AI has to reimplement any of it.
    ///
    /// Unity's own <c>CharacterController.isGrounded</c> flickers on slopes and
    /// steps, so grounding is done with a sphere cast and a small amount of
    /// hysteresis instead.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterMotor : MonoBehaviour
    {
        [SerializeField] private float _gravity = -24f;
        [SerializeField] private float _fallMultiplier = 1.7f;

        [Tooltip("Extra downward speed applied while grounded to keep the capsule glued to slopes.")]
        [SerializeField] private float _groundStick = 4f;

        [Tooltip("How quickly externally applied velocity (knockback) bleeds off.")]
        [SerializeField] private float _externalDamping = 6f;

        private CharacterController _cc;
        private LayerMask _groundMask;

        private Vector3 _external;
        private float _verticalVelocity;
        private bool _grounded;
        private bool _wasGrounded;
        private Vector3 _groundNormal = Vector3.up;
        private float _lastGroundedTime = float.NegativeInfinity;
        private float _airborneSince;

        /// <summary>Planar velocity actually achieved last frame, in world space.</summary>
        public Vector3 PlanarVelocity { get; private set; }

        public float VerticalVelocity => _verticalVelocity;
        public bool IsGrounded => _grounded;
        public bool WasGroundedLastFrame => _wasGrounded;
        public Vector3 GroundNormal => _groundNormal;
        public float TimeSinceGrounded => Time.time - _lastGroundedTime;
        public CharacterController Controller => _cc;

        /// <summary>Downward speed at the moment of the most recent landing.</summary>
        public float LastLandingSpeed { get; private set; }

        /// <summary>True on the single frame the character touched down.</summary>
        public bool JustLanded { get; private set; }

        /// <summary>Set while an external impulse is still meaningfully pushing the character.</summary>
        public bool HasExternalMotion => _external.sqrMagnitude > 0.04f;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _groundMask = GameLayers.WorldMask;
        }

        public void Configure(float height, float radius, float stepOffset, float slopeLimit,
            float gravity, float fallMultiplier)
        {
            if (_cc == null) _cc = GetComponent<CharacterController>();
            _cc.height = height;
            _cc.radius = radius;
            _cc.center = new Vector3(0f, height * 0.5f, 0f);
            _cc.stepOffset = Mathf.Min(stepOffset, height * 0.4f);
            _cc.slopeLimit = slopeLimit;
            _cc.skinWidth = Mathf.Max(0.01f, radius * 0.08f);
            _cc.minMoveDistance = 0f;
            _gravity = gravity;
            _fallMultiplier = fallMultiplier;
            _groundMask = GameLayers.WorldMask;
        }

        /// <summary>
        /// Advances the character by one step.
        /// </summary>
        /// <param name="desiredPlanarVelocity">World-space horizontal velocity the owner wants.</param>
        /// <param name="dt">Delta time.</param>
        /// <param name="applyGravity">False while a move drives vertical motion itself.</param>
        public void Tick(Vector3 desiredPlanarVelocity, float dt, bool applyGravity = true)
        {
            if (dt <= 0f) return;

            _wasGrounded = _grounded;
            JustLanded = false;

            ProbeGround();

            if (_grounded && !_wasGrounded)
            {
                JustLanded = true;
                LastLandingSpeed = Mathf.Abs(_verticalVelocity);
            }
            else if (!_grounded && _wasGrounded)
            {
                _airborneSince = Time.time;
            }

            if (applyGravity) ApplyGravity(dt);

            desiredPlanarVelocity.y = 0f;

            // On a walkable slope, redirect movement along the surface so the
            // character neither bounces down inclines nor climbs them slower.
            if (_grounded && _verticalVelocity <= 0f && _groundNormal != Vector3.up)
                desiredPlanarVelocity = Vector3.ProjectOnPlane(desiredPlanarVelocity, _groundNormal);

            Vector3 motion = desiredPlanarVelocity + _external;
            motion.y += _verticalVelocity;

            CollisionFlags flags = _cc.Move(motion * dt);

            // Head bonk: stop climbing immediately rather than hanging under the ceiling.
            if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
                _verticalVelocity = 0f;

            PlanarVelocity = MathUtil.Flat(_cc.velocity);

            _external = Vector3.Lerp(_external, Vector3.zero, 1f - Mathf.Exp(-_externalDamping * dt));
            if (_external.sqrMagnitude < 0.01f) _external = Vector3.zero;
        }

        private void ApplyGravity(float dt)
        {
            if (_grounded && _verticalVelocity <= 0f)
            {
                // A constant small downward bias keeps the controller in contact
                // with the floor so isGrounded does not chatter on ramps.
                _verticalVelocity = -_groundStick;
                return;
            }

            float g = _gravity * (_verticalVelocity < 0f ? _fallMultiplier : 1f);
            _verticalVelocity += g * dt;
            _verticalVelocity = Mathf.Max(_verticalVelocity, -60f);
        }

        /// <summary>Launches the character to reach roughly <paramref name="height"/> metres.</summary>
        public void Jump(float height)
        {
            _verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(_gravity) * Mathf.Max(0.05f, height));
            _grounded = false;
            _airborneSince = Time.time;
        }

        /// <summary>Cuts an ascent short when the jump button is released.</summary>
        public void DampenJump(float multiplier)
        {
            if (_verticalVelocity > 0f) _verticalVelocity *= Mathf.Clamp01(multiplier);
        }

        /// <summary>Adds a one-off velocity, e.g. knockback. Decays over time.</summary>
        public void AddImpulse(Vector3 velocity)
        {
            _external += MathUtil.Flat(velocity);
            if (velocity.y > 0.01f)
            {
                _verticalVelocity = Mathf.Max(_verticalVelocity, velocity.y);
                _grounded = false;
            }
        }

        public void ClearImpulse() => _external = Vector3.zero;

        public void SetVerticalVelocity(float value) => _verticalVelocity = value;

        /// <summary>Moves the character without fighting the collider. Use for respawns.</summary>
        public void Teleport(Vector3 position, Quaternion? rotation = null)
        {
            _cc.enabled = false;
            transform.position = position;
            if (rotation.HasValue) transform.rotation = rotation.Value;
            _cc.enabled = true;

            _external = Vector3.zero;
            _verticalVelocity = 0f;
            PlanarVelocity = Vector3.zero;
        }

        private void ProbeGround()
        {
            float radius = _cc.radius * 0.92f;
            Vector3 bottom = transform.position + _cc.center - Vector3.up * (_cc.height * 0.5f - _cc.radius);

            // Probe further when already grounded so walking down slopes and stairs
            // does not read as leaving the ground for a frame.
            float distance = _grounded ? _cc.stepOffset + 0.12f : _cc.skinWidth + 0.06f;

            bool rising = _verticalVelocity > 0.05f;
            if (rising)
            {
                _grounded = false;
                _groundNormal = Vector3.up;
                return;
            }

            if (Physics.SphereCast(bottom, radius, Vector3.down, out RaycastHit hit, distance,
                    _groundMask, QueryTriggerInteraction.Ignore))
            {
                float angle = Vector3.Angle(hit.normal, Vector3.up);
                _grounded = angle <= _cc.slopeLimit + 0.5f;
                _groundNormal = _grounded ? hit.normal : Vector3.up;
            }
            else
            {
                _grounded = false;
                _groundNormal = Vector3.up;
            }

            if (_grounded) _lastGroundedTime = Time.time;
        }

        /// <summary>Seconds the character has been in the air, or zero when grounded.</summary>
        public float AirTime => _grounded ? 0f : Mathf.Max(0f, Time.time - _airborneSince);
    }
}
