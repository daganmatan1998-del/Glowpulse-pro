using Glowpulse.Core;
using Glowpulse.Core.InputSystem;
using UnityEngine;

namespace Glowpulse.CameraSystem
{
    /// <summary>
    /// Orbiting third-person camera with wall avoidance, combat framing and
    /// lock-on. Runs in LateUpdate after the character has moved, so the frame
    /// never lags a step behind the player.
    ///
    /// Wall avoidance uses an asymmetric response: the camera snaps inward almost
    /// instantly when geometry intrudes (clipping is unforgivable) but eases back
    /// out slowly once clear (snapping out is nauseating).
    /// </summary>
    [DefaultExecutionOrder(500)]
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private CameraConfig _config;
        [SerializeField] private Transform _target;

        private UnityEngine.Camera _camera;
        private CameraShaker _shaker;

        private Vector3 _pivot;
        private float _yaw;
        private float _pitch;
        private float _distance;
        private float _occludedDistance;
        private float _fov;
        private bool _initialised;

        private float _combatWeight;
        private float _speedNormalized;
        private ITargetable _lockTarget;

        private readonly RaycastHit[] _hits = new RaycastHit[8];

        public CameraConfig Config => _config != null ? _config : CameraConfig.Default;

        public Transform Target
        {
            get => _target;
            set
            {
                _target = value;
                _initialised = false;
            }
        }

        /// <summary>Target being framed by lock-on, or null for free look.</summary>
        public ITargetable LockTarget
        {
            get => _lockTarget != null && _lockTarget.IsTargetable ? _lockTarget : null;
            set => _lockTarget = value;
        }

        /// <summary>0..1 blend toward the tighter combat framing.</summary>
        public float CombatWeight
        {
            get => _combatWeight;
            set => _combatWeight = Mathf.Clamp01(value);
        }

        /// <summary>Current yaw in degrees. Movement input is resolved against this.</summary>
        public float Yaw => _yaw;

        private void Awake()
        {
            _camera = GetComponent<UnityEngine.Camera>();
            _shaker = GetComponent<CameraShaker>();
            if (_shaker == null) _shaker = gameObject.AddComponent<CameraShaker>();

            CameraConfig c = Config;
            _pitch = c.DefaultPitch;
            _distance = c.Distance;
            _occludedDistance = c.Distance;
            _fov = c.BaseFov;
            _camera.fieldOfView = _fov;
        }

        /// <summary>Feeds the camera the player's speed so it can widen at a sprint.</summary>
        public void SetSpeedNormalized(float value) => _speedNormalized = Mathf.Clamp01(value);

        /// <summary>Points the camera at a yaw immediately - used when respawning or teleporting.</summary>
        public void SnapTo(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, Config.MinPitch, Config.MaxPitch);
            _initialised = false;
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            CameraConfig c = Config;
            float dt = Time.deltaTime;
            float unscaled = Time.unscaledDeltaTime;

            // Rotation runs on unscaled time so looking around stays responsive
            // during hit-stop and slow motion.
            UpdateRotation(c, unscaled);

            Vector3 desiredPivot = ResolvePivot(c);
            if (!_initialised)
            {
                _pivot = desiredPivot;
                _initialised = true;
            }
            else
            {
                // Horizontal and vertical follow are damped separately: a tight
                // horizontal chase keeps the character centred, while a looser
                // vertical one stops stairs and jumps from jolting the frame.
                Vector3 flat = MathUtil.Damp(
                    new Vector3(_pivot.x, 0f, _pivot.z),
                    new Vector3(desiredPivot.x, 0f, desiredPivot.z),
                    c.FollowSharpness, dt);
                float y = MathUtil.Damp(_pivot.y, desiredPivot.y, c.VerticalFollowSharpness, dt);
                _pivot = new Vector3(flat.x, y, flat.z);
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            float targetDistance = ResolveDistance(c);
            _distance = MathUtil.Damp(_distance, targetDistance, 8f, dt);

            Vector3 shoulder = rotation * new Vector3(c.ShoulderOffset * (1f - _combatWeight * 0.35f), 0f, 0f);
            Vector3 anchor = _pivot + shoulder;

            float allowed = ResolveOccludedDistance(c, anchor, rotation, _distance, dt);

            Vector3 position = anchor - rotation * Vector3.forward * allowed;

            ApplyShake(ref position, ref rotation);

            transform.SetPositionAndRotation(position, rotation);

            UpdateFov(c, dt);
        }

        private void UpdateRotation(CameraConfig c, float dt)
        {
            IInputProvider input = InputService.Current;
            Vector2 look = input.Look;

            ITargetable locked = LockTarget;
            if (locked != null)
            {
                // Locked on, the camera owns the yaw: it stays behind the player
                // looking at the target, with the stick only nudging the pitch.
                Vector3 toTarget = MathUtil.FlatDirection(locked.Transform.position - _target.position);
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    float desiredYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                    _yaw = MathUtil.DampAngle(_yaw, desiredYaw, c.RotationSharpness * 0.5f, dt);
                }

                float desiredPitch = c.LockPitch - look.y * c.PitchSensitivity * 0.4f;
                _pitch = Mathf.Clamp(MathUtil.Damp(_pitch, desiredPitch, c.RotationSharpness * 0.4f, dt),
                    c.MinPitch, c.MaxPitch);
                return;
            }

            _yaw += look.x * c.YawSensitivity;
            _pitch = Mathf.Clamp(_pitch - look.y * c.PitchSensitivity, c.MinPitch, c.MaxPitch);
        }

        private Vector3 ResolvePivot(CameraConfig c)
        {
            Vector3 basePivot = _target.position + Vector3.up * c.PivotHeight;

            ITargetable locked = LockTarget;
            if (locked == null) return basePivot;

            // Bias the framing toward the enemy so both fighters stay on screen.
            Vector3 targetPoint = locked.AimPoint;
            return Vector3.Lerp(basePivot, targetPoint, c.LockFramingBias);
        }

        private float ResolveDistance(CameraConfig c)
        {
            float distance = Mathf.Lerp(c.Distance, c.CombatDistance, _combatWeight);
            distance += c.SprintDistanceBoost * _speedNormalized * (1f - _combatWeight * 0.6f);

            ITargetable locked = LockTarget;
            if (locked != null)
            {
                float separation = MathUtil.FlatDistance(_target.position, locked.Transform.position);
                distance += Mathf.Min(separation * c.LockDistancePerMetre, c.LockMaxExtraDistance);
            }

            return Mathf.Max(c.MinDistance, distance);
        }

        /// <summary>
        /// Sweeps a sphere from the pivot out to the camera and shortens the boom
        /// to the nearest blocker.
        /// </summary>
        private float ResolveOccludedDistance(CameraConfig c, Vector3 anchor, Quaternion rotation,
            float desired, float dt)
        {
            Vector3 direction = -(rotation * Vector3.forward);
            float maxDistance = desired;
            float allowed = maxDistance;

            int count = Physics.SphereCastNonAlloc(anchor, c.CollisionRadius, direction, _hits,
                maxDistance, GameLayers.CameraBlockerMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];

                // A zero distance means the sphere started inside the collider.
                // Ignore it rather than slamming the camera into the player.
                if (hit.distance <= 0.0001f) continue;
                if (hit.collider.transform.IsChildOf(_target)) continue;

                float candidate = hit.distance - c.CollisionPadding;
                if (candidate < allowed) allowed = candidate;
            }

            allowed = Mathf.Max(c.MinDistance, allowed);

            // Snap in hard, ease out gently.
            float sharpness = allowed < _occludedDistance
                ? c.CollisionPullInSharpness
                : c.CollisionPushOutSharpness;
            _occludedDistance = MathUtil.Damp(_occludedDistance, allowed, sharpness, dt);

            return Mathf.Clamp(_occludedDistance, c.MinDistance, maxDistance);
        }

        private void ApplyShake(ref Vector3 position, ref Quaternion rotation)
        {
            if (_shaker == null) return;

            Vector3 offset = _shaker.PositionOffset;
            if (offset != Vector3.zero) position += rotation * offset;

            Vector3 rot = _shaker.RotationOffset;
            if (rot != Vector3.zero) rotation *= Quaternion.Euler(rot);
        }

        private void UpdateFov(CameraConfig c, float dt)
        {
            float target = c.BaseFov + c.SprintFovBoost * _speedNormalized * (1f - _combatWeight * 0.5f);
            _fov = MathUtil.Damp(_fov, target, c.FovSharpness, dt);
            _camera.fieldOfView = _fov;
        }
    }
}
