using System;
using UnityEngine;

namespace Glowpulse.Combat
{
    /// <summary>
    /// Hit points with optional out-of-combat regeneration and a short
    /// invulnerability window. Deliberately knows nothing about factions,
    /// damage types or reactions - <see cref="Combatant"/> owns that policy.
    /// </summary>
    public sealed class Health : MonoBehaviour
    {
        [SerializeField] private float _max = 100f;
        [SerializeField] private float _current = 100f;

        [Header("Regeneration")]
        [Tooltip("Hit points restored per second once regeneration kicks in. Zero disables it.")]
        [SerializeField] private float _regenPerSecond;

        [Tooltip("Seconds without taking damage before regeneration starts.")]
        [SerializeField] private float _regenDelay = 6f;

        [Tooltip("Regeneration stops at this fraction of maximum health.")]
        [SerializeField, Range(0f, 1f)] private float _regenCeiling = 0.6f;

        private float _lastDamageTime = float.NegativeInfinity;
        private float _invulnerableUntil = float.NegativeInfinity;

        /// <summary>Damage actually applied, after clamping.</summary>
        public event Action<float> Damaged;

        public event Action<float> Healed;
        public event Action Died;

        /// <summary>Fired on any change to current or max, for UI binding.</summary>
        public event Action<Health> Changed;

        public float Max => _max;
        public float Current => _current;
        public float Normalized => _max <= 0f ? 0f : Mathf.Clamp01(_current / _max);
        public bool IsAlive => _current > 0f;
        public bool IsInvulnerable => Time.time < _invulnerableUntil;
        public float TimeSinceDamage => Time.time - _lastDamageTime;

        private void Awake()
        {
            _max = Mathf.Max(1f, _max);
            _current = Mathf.Clamp(_current <= 0f ? _max : _current, 0f, _max);
        }

        public void Configure(float max, bool fill = true)
        {
            _max = Mathf.Max(1f, max);
            _current = fill ? _max : Mathf.Min(_current, _max);
            Changed?.Invoke(this);
        }

        /// <summary>Raises the ceiling, keeping the same absolute or relative amount.</summary>
        public void SetMax(float max, bool preserveRatio)
        {
            float ratio = Normalized;
            _max = Mathf.Max(1f, max);
            _current = preserveRatio ? _max * ratio : Mathf.Min(_current, _max);
            Changed?.Invoke(this);
        }

        /// <summary>Applies damage. Returns the amount actually taken.</summary>
        public float Damage(float amount)
        {
            if (amount <= 0f || !IsAlive || IsInvulnerable) return 0f;

            float applied = Mathf.Min(amount, _current);
            _current -= applied;
            _lastDamageTime = Time.time;

            Damaged?.Invoke(applied);
            Changed?.Invoke(this);

            if (_current <= 0f)
            {
                _current = 0f;
                Died?.Invoke();
            }

            return applied;
        }

        /// <summary>Damage that bypasses the invulnerability window (scripted deaths, environment).</summary>
        public float DamageIgnoringInvulnerability(float amount)
        {
            float until = _invulnerableUntil;
            _invulnerableUntil = float.NegativeInfinity;
            float applied = Damage(amount);
            _invulnerableUntil = until;
            return applied;
        }

        public float Heal(float amount)
        {
            if (amount <= 0f || !IsAlive) return 0f;
            float applied = Mathf.Min(amount, _max - _current);
            if (applied <= 0f) return 0f;

            _current += applied;
            Healed?.Invoke(applied);
            Changed?.Invoke(this);
            return applied;
        }

        public void Kill()
        {
            if (!IsAlive) return;
            _current = 0f;
            _lastDamageTime = Time.time;
            Changed?.Invoke(this);
            Died?.Invoke();
        }

        public void Revive(float fraction = 1f)
        {
            _current = Mathf.Clamp(_max * Mathf.Clamp01(fraction), 1f, _max);
            _lastDamageTime = float.NegativeInfinity;
            Changed?.Invoke(this);
        }

        /// <summary>Grants invulnerability for a window, e.g. dodge i-frames.</summary>
        public void GrantInvulnerability(float duration)
        {
            _invulnerableUntil = Mathf.Max(_invulnerableUntil, Time.time + duration);
        }

        public void ClearInvulnerability() => _invulnerableUntil = float.NegativeInfinity;

        private void Update()
        {
            if (_regenPerSecond <= 0f || !IsAlive) return;
            if (TimeSinceDamage < _regenDelay) return;

            float ceiling = _max * _regenCeiling;
            if (_current >= ceiling) return;

            _current = Mathf.Min(ceiling, _current + _regenPerSecond * Time.deltaTime);
            Changed?.Invoke(this);
        }
    }
}
