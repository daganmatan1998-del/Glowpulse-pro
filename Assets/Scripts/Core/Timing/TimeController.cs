using UnityEngine;

namespace Glowpulse.Core.Timing
{
    /// <summary>
    /// The single owner of <see cref="Time.timeScale"/>.
    ///
    /// Hit stop, slow motion and the pause menu all want to bend time, and if
    /// each of them writes timeScale directly they overwrite one another - the
    /// classic symptom being a game stuck at 0.2x because a hit landed as the
    /// player paused. Routing everything through here gives a clear priority:
    /// pause beats hit stop beats slow motion.
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    public sealed class TimeController : MonoBehaviour
    {
        private static TimeController _instance;

        private float _hitStopUntil;
        private float _hitStopScale = 1f;

        private float _slowMotionUntil;
        private float _slowMotionScale = 1f;
        private float _slowMotionBlendIn;
        private float _slowMotionStartedAt;

        private bool _paused;
        private float _baseFixedDelta = 0.02f;

        public static TimeController Instance => _instance;

        /// <summary>True while any effect is currently bending time.</summary>
        public bool IsAltered => _paused || IsHitStopped || IsSlowMotion;

        public bool IsHitStopped => Time.unscaledTime < _hitStopUntil;
        public bool IsSlowMotion => Time.unscaledTime < _slowMotionUntil;
        public bool IsPaused => _paused;

        public static TimeController Install(GameObject host)
        {
            if (_instance != null) return _instance;
            _instance = host.GetComponent<TimeController>() ?? host.AddComponent<TimeController>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            _baseFixedDelta = Time.fixedDeltaTime;
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            _instance = null;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _baseFixedDelta;
        }

        /// <summary>
        /// Freezes the action briefly. This is the single most important piece of
        /// hit feel: a few frames of stillness at the moment of contact is what
        /// makes a punch feel like it connected with something solid.
        /// </summary>
        public void HitStop(float duration, float scale = 0.02f)
        {
            if (duration <= 0f) return;
            float until = Time.unscaledTime + duration;

            // Overlapping hits extend rather than restart, and the harder hit wins.
            if (until > _hitStopUntil) _hitStopUntil = until;
            _hitStopScale = Mathf.Min(_hitStopScale, Mathf.Clamp01(scale));
        }

        /// <summary>Short cinematic slow motion, e.g. on a finisher or a parry.</summary>
        public void SlowMotion(float duration, float scale = 0.35f, float blendIn = 0.04f)
        {
            if (duration <= 0f) return;
            _slowMotionUntil = Mathf.Max(_slowMotionUntil, Time.unscaledTime + duration);
            _slowMotionScale = Mathf.Min(_slowMotionScale, Mathf.Clamp(scale, 0.01f, 1f));
            _slowMotionBlendIn = Mathf.Max(0.001f, blendIn);
            _slowMotionStartedAt = Time.unscaledTime;
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
            Apply();
        }

        /// <summary>Cancels every time effect. Used on death, scene change and respawn.</summary>
        public void ClearEffects()
        {
            _hitStopUntil = 0f;
            _hitStopScale = 1f;
            _slowMotionUntil = 0f;
            _slowMotionScale = 1f;
            Apply();
        }

        private void Update() => Apply();

        private void Apply()
        {
            float scale = 1f;

            if (_paused)
            {
                scale = 0f;
            }
            else if (IsHitStopped)
            {
                scale = _hitStopScale;
            }
            else
            {
                if (_hitStopScale < 1f) _hitStopScale = 1f;

                if (IsSlowMotion)
                {
                    // Easing in stops slow motion from feeling like a dropped frame.
                    float t = Mathf.Clamp01((Time.unscaledTime - _slowMotionStartedAt) / _slowMotionBlendIn);
                    scale = Mathf.Lerp(1f, _slowMotionScale, t);
                }
                else if (_slowMotionScale < 1f)
                {
                    _slowMotionScale = 1f;
                }
            }

            Time.timeScale = scale;

            // Physics must step at the same rate as the visible world, or
            // characters slide during hit stop.
            Time.fixedDeltaTime = _baseFixedDelta * Mathf.Max(scale, 0.0001f);
        }

        // ---- static conveniences used by combat code -------------------------------

        public static void RequestHitStop(float duration, float scale = 0.02f)
        {
            if (_instance != null) _instance.HitStop(duration, scale);
        }

        public static void RequestSlowMotion(float duration, float scale = 0.35f)
        {
            if (_instance != null) _instance.SlowMotion(duration, scale);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }
}
