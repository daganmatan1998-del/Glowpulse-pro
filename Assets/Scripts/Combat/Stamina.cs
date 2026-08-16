using System;
using UnityEngine;

namespace Glowpulse.Combat
{
    /// <summary>
    /// The resource behind sprinting, dodging, attacking and blocking.
    ///
    /// Draining it to zero puts the character into an exhausted state that only
    /// clears once a recovery threshold is reached. That single rule is what
    /// stops players from mashing dodge and makes stamina a real decision.
    /// </summary>
    public sealed class Stamina : MonoBehaviour
    {
        [SerializeField] private float _max = 100f;
        [SerializeField] private float _current = 100f;

        [Header("Regeneration")]
        [SerializeField] private float _regenPerSecond = 24f;

        [Tooltip("Seconds after spending before regeneration resumes.")]
        [SerializeField] private float _regenDelay = 0.7f;

        [Tooltip("Regeneration multiplier while exhausted - recovery is deliberately slow.")]
        [SerializeField, Range(0.1f, 2f)] private float _exhaustedRegenScale = 0.6f;

        [Tooltip("Fraction of maximum that must be restored to clear exhaustion.")]
        [SerializeField, Range(0.05f, 1f)] private float _exhaustionRecovery = 0.28f;

        private float _lastSpendTime = float.NegativeInfinity;
        private bool _exhausted;
        private float _regenBlockedUntil = float.NegativeInfinity;

        public event Action<float> Spent;
        public event Action<Stamina> Changed;
        public event Action<bool> ExhaustedChanged;

        public float Max => _max;
        public float Current => _current;
        public float Normalized => _max <= 0f ? 0f : Mathf.Clamp01(_current / _max);

        /// <summary>True while the character has bottomed out and must recover before acting.</summary>
        public bool IsExhausted => _exhausted;

        public bool IsFull => _current >= _max - 0.001f;

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

        public void SetMax(float max, bool preserveRatio)
        {
            float ratio = Normalized;
            _max = Mathf.Max(1f, max);
            _current = preserveRatio ? _max * ratio : Mathf.Min(_current, _max);
            Changed?.Invoke(this);
        }

        /// <summary>True when the character could afford <paramref name="cost"/> right now.</summary>
        public bool CanSpend(float cost) => !_exhausted && _current >= cost;

        /// <summary>Spends stamina if it is available. Returns false and changes nothing otherwise.</summary>
        public bool TrySpend(float cost)
        {
            if (cost <= 0f) return true;
            if (!CanSpend(cost)) return false;
            SpendUnchecked(cost);
            return true;
        }

        /// <summary>
        /// Spends stamina even if it takes the character to zero - used for costs
        /// that are forced on you, like absorbing a heavy hit on your guard.
        /// </summary>
        public void SpendUnchecked(float cost)
        {
            if (cost <= 0f) return;

            _current = Mathf.Max(0f, _current - cost);
            _lastSpendTime = Time.time;
            Spent?.Invoke(cost);
            Changed?.Invoke(this);

            if (!_exhausted && _current <= 0.001f) SetExhausted(true);
        }

        /// <summary>Continuous drain, e.g. sprinting. Returns false once nothing is left.</summary>
        public bool Drain(float perSecond, float deltaTime)
        {
            if (_exhausted || _current <= 0f) return false;
            SpendUnchecked(perSecond * deltaTime);
            return _current > 0f;
        }

        public void Restore(float amount)
        {
            if (amount <= 0f) return;
            _current = Mathf.Min(_max, _current + amount);
            Changed?.Invoke(this);
            if (_exhausted && _current >= _max * _exhaustionRecovery) SetExhausted(false);
        }

        public void Refill()
        {
            _current = _max;
            SetExhausted(false);
            Changed?.Invoke(this);
        }

        /// <summary>Holds regeneration off for a while, e.g. during a combo.</summary>
        public void BlockRegen(float seconds)
        {
            _regenBlockedUntil = Mathf.Max(_regenBlockedUntil, Time.time + seconds);
        }

        private void Update()
        {
            if (_current >= _max) return;
            if (Time.time - _lastSpendTime < _regenDelay) return;
            if (Time.time < _regenBlockedUntil) return;

            float rate = _regenPerSecond * (_exhausted ? _exhaustedRegenScale : 1f);
            _current = Mathf.Min(_max, _current + rate * Time.deltaTime);
            Changed?.Invoke(this);

            if (_exhausted && _current >= _max * _exhaustionRecovery) SetExhausted(false);
        }

        private void SetExhausted(bool value)
        {
            if (_exhausted == value) return;
            _exhausted = value;
            ExhaustedChanged?.Invoke(value);
        }
    }
}
