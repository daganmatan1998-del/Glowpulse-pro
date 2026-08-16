using System;
using System.Collections.Generic;
using Glowpulse.Core;
using Glowpulse.Core.InputSystem;
using Glowpulse.Player;
using UnityEngine;

namespace Glowpulse.CameraSystem
{
    /// <summary>
    /// Chooses and holds the combat target. Acquisition scores candidates by how
    /// close they are to the centre of the screen as well as by distance, because
    /// "the one I am looking at" is almost always the one the player means.
    ///
    /// The lock is dropped when the target dies, leaves the break radius, or stays
    /// out of sight long enough that holding it would feel broken.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class LockOnSystem : MonoBehaviour
    {
        [SerializeField] private PlayerController _player;
        [SerializeField] private ThirdPersonCamera _camera;

        [Header("Acquisition")]
        [Tooltip("Maximum distance at which a target can be acquired.")]
        [SerializeField] private float _acquireRadius = 18f;

        [Tooltip("Distance at which an existing lock is dropped. Larger than acquire, for hysteresis.")]
        [SerializeField] private float _breakRadius = 26f;

        [Tooltip("Candidates outside this half-angle from the camera's forward are ignored.")]
        [SerializeField, Range(10f, 180f)] private float _acquireHalfAngle = 75f;

        [Tooltip("How much screen-centre proximity is favoured over raw closeness.")]
        [SerializeField, Range(0f, 1f)] private float _aimWeight = 0.65f;

        [Header("Break conditions")]
        [Tooltip("Seconds the target may stay behind cover before the lock drops.")]
        [SerializeField] private float _occlusionGrace = 1.4f;

        [Header("Cycling")]
        [SerializeField] private float _cycleCooldown = 0.22f;

        private readonly List<ITargetable> _candidates = new List<ITargetable>(32);
        private ITargetable _current;
        private float _occludedFor;
        private float _nextCycleAt;

        /// <summary>Raised when the lock changes, including to null.</summary>
        public event Action<ITargetable> TargetChanged;

        public ITargetable Current => _current != null && _current.IsTargetable ? _current : null;
        public bool HasTarget => Current != null;

        public void Bind(PlayerController player, ThirdPersonCamera camera)
        {
            _player = player;
            _camera = camera;
        }

        private void Update()
        {
            if (_player == null) return;

            IInputProvider input = InputService.Current;

            if (input.LockOnPressed)
            {
                if (HasTarget) SetTarget(null);
                else SetTarget(FindBest(null));
            }

            if (HasTarget && input.LockTargetCycle != 0 && Time.time >= _nextCycleAt)
            {
                ITargetable next = FindBest(_current);
                if (next != null)
                {
                    SetTarget(next);
                    _nextCycleAt = Time.time + _cycleCooldown;
                }
            }

            ValidateCurrent();
            Publish();
        }

        /// <summary>Locks onto a specific target, e.g. the enemy that just hit the player.</summary>
        public void ForceTarget(ITargetable target)
        {
            if (target != null && !target.IsTargetable) return;
            SetTarget(target);
        }

        public void Clear() => SetTarget(null);

        private void ValidateCurrent()
        {
            if (_current == null) return;

            if (!_current.IsTargetable || _current.Transform == null)
            {
                SetTarget(null);
                return;
            }

            float distance = MathUtil.FlatDistance(_player.transform.position, _current.Transform.position);
            if (distance > _breakRadius)
            {
                SetTarget(null);
                return;
            }

            // Losing sight briefly is fine - people move behind lamp posts - but
            // a target that stays hidden should release the lock.
            if (HasLineOfSight(_current))
            {
                _occludedFor = 0f;
            }
            else
            {
                _occludedFor += Time.deltaTime;
                if (_occludedFor >= _occlusionGrace) SetTarget(null);
            }
        }

        private ITargetable FindBest(ITargetable exclude)
        {
            Vector3 origin = _player.transform.position;
            Faction faction = Faction.Player;

            TargetRegistry.Query(origin, _acquireRadius, faction, _candidates);
            if (_candidates.Count == 0) return null;

            Transform cam = _camera != null ? _camera.transform : _player.transform;
            Vector3 viewForward = MathUtil.FlatDirection(cam.forward);
            if (viewForward.sqrMagnitude < 0.01f) viewForward = _player.transform.forward;

            ITargetable best = null;
            float bestScore = float.MaxValue;
            float cosLimit = Mathf.Cos(_acquireHalfAngle * Mathf.Deg2Rad);

            for (int i = 0; i < _candidates.Count; i++)
            {
                ITargetable candidate = _candidates[i];
                if (candidate == exclude) continue;

                Vector3 to = candidate.Transform.position - origin;
                Vector3 flat = MathUtil.FlatDirection(to);
                if (flat.sqrMagnitude < 0.0001f) continue;

                float alignment = Vector3.Dot(viewForward, flat);
                if (alignment < cosLimit) continue;
                if (!HasLineOfSight(candidate)) continue;

                float distance01 = Mathf.Clamp01(MathUtil.Flat(to).magnitude / _acquireRadius);
                float aim01 = 1f - Mathf.Clamp01((alignment - cosLimit) / Mathf.Max(0.0001f, 1f - cosLimit));

                float score = aim01 * _aimWeight + distance01 * (1f - _aimWeight);
                if (score >= bestScore) continue;

                bestScore = score;
                best = candidate;
            }

            // Cycling past the last candidate wraps back to the current target
            // rather than dropping the lock entirely.
            return best ?? (exclude != null && exclude.IsTargetable ? exclude : null);
        }

        private bool HasLineOfSight(ITargetable target)
        {
            Vector3 from = _player.transform.position + Vector3.up * 1.4f;
            Vector3 to = target.AimPoint;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.2f) return true;

            return !Physics.Raycast(from, delta / distance, distance - 0.25f,
                GameLayers.SightBlockerMask, QueryTriggerInteraction.Ignore);
        }

        private void SetTarget(ITargetable target)
        {
            if (ReferenceEquals(_current, target)) return;
            _current = target;
            _occludedFor = 0f;
            TargetChanged?.Invoke(target);
            Publish();
        }

        private void Publish()
        {
            ITargetable target = Current;
            if (_player != null) _player.LockTarget = target;
            if (_camera != null) _camera.LockTarget = target;
        }
    }
}
