using Glowpulse.Core;
using UnityEngine;

namespace Glowpulse.CameraSystem
{
    /// <summary>
    /// Trauma-based camera shake. Callers add trauma; the shake amplitude is
    /// trauma squared, so small hits barely register while big ones land hard,
    /// and everything decays back to nothing on its own.
    ///
    /// Directional kicks are separate: a punch should shove the frame a specific
    /// way, not just rattle it.
    /// </summary>
    [DefaultExecutionOrder(-5)]
    public sealed class CameraShaker : MonoBehaviour
    {
        [SerializeField] private float _traumaDecay = 1.6f;
        [SerializeField] private float _maxPositionShake = 0.26f;
        [SerializeField] private float _maxRotationShake = 3.4f;
        [SerializeField] private float _frequency = 22f;
        [SerializeField] private float _kickDecay = 9f;

        private float _trauma;
        private float _time;
        private Vector3 _kick;
        private Vector3 _kickVelocity;
        private readonly float[] _seeds = new float[4];

        /// <summary>The shaker attached to the active gameplay camera.</summary>
        public static CameraShaker Active { get; private set; }

        /// <summary>Positional offset to add to the camera this frame, in camera space.</summary>
        public Vector3 PositionOffset { get; private set; }

        /// <summary>Rotational offset in degrees to add this frame.</summary>
        public Vector3 RotationOffset { get; private set; }

        public float Trauma => _trauma;

        private void Awake()
        {
            for (int i = 0; i < _seeds.Length; i++) _seeds[i] = Random.value * 1000f;
            Active = this;
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        /// <summary>Adds shake. 0.15 is a light hit, 0.4 a heavy one, 1.0 is the cap.</summary>
        public void AddTrauma(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + Mathf.Max(0f, amount));
        }

        /// <summary>
        /// Shoves the camera in a world direction and lets it spring back.
        /// Use for impacts where the direction of the blow should be readable.
        /// </summary>
        public void AddKick(Vector3 worldDirection, float strength)
        {
            if (strength <= 0f) return;
            _kickVelocity += worldDirection.normalized * strength;
        }

        public void Clear()
        {
            _trauma = 0f;
            _kick = Vector3.zero;
            _kickVelocity = Vector3.zero;
            PositionOffset = Vector3.zero;
            RotationOffset = Vector3.zero;
        }

        private void LateUpdate()
        {
            // Unscaled so hit-stop and slow motion do not stretch the shake out.
            float dt = Time.unscaledDeltaTime;
            _time += dt * _frequency;

            _trauma = Mathf.Max(0f, _trauma - _traumaDecay * dt);

            const float stiffness = 90f;
            _kickVelocity += (-stiffness * _kick - 2f * Mathf.Sqrt(stiffness) * _kickVelocity) * dt;
            _kick += _kickVelocity * dt;
            if (_kick.sqrMagnitude < 1e-6f && _kickVelocity.sqrMagnitude < 1e-6f) _kick = Vector3.zero;

            if (_trauma <= 0.0001f && _kick == Vector3.zero)
            {
                PositionOffset = Vector3.zero;
                RotationOffset = Vector3.zero;
                return;
            }

            float shake = _trauma * _trauma;

            PositionOffset = new Vector3(
                MathUtil.NoiseSigned(_time, _seeds[0]) * _maxPositionShake * shake,
                MathUtil.NoiseSigned(_time, _seeds[1]) * _maxPositionShake * shake,
                0f) + _kick;

            RotationOffset = new Vector3(
                MathUtil.NoiseSigned(_time, _seeds[2]) * _maxRotationShake * shake,
                MathUtil.NoiseSigned(_time, _seeds[3]) * _maxRotationShake * shake,
                MathUtil.NoiseSigned(_time * 0.7f, _seeds[0]) * _maxRotationShake * 1.4f * shake);
        }

        /// <summary>Convenience for combat code that does not hold a reference.</summary>
        public static void Shake(float trauma)
        {
            if (Active != null) Active.AddTrauma(trauma);
        }

        public static void Kick(Vector3 worldDirection, float strength)
        {
            if (Active != null) Active.AddKick(worldDirection, strength);
        }
    }
}
