using UnityEngine;

namespace Glowpulse.Core.InputSystem
{
    /// <summary>
    /// Remembers a button press for a short window so an action queued slightly
    /// too early still fires. This is what makes combos and jump-off-a-ledge feel
    /// forgiving rather than dropped.
    /// </summary>
    public struct InputBuffer
    {
        private float _pressedAt;
        private float _window;
        private bool _armed;

        public InputBuffer(float window)
        {
            _window = window;
            _pressedAt = float.NegativeInfinity;
            _armed = false;
        }

        public float Window
        {
            get => _window <= 0f ? 0.18f : _window;
            set => _window = value;
        }

        /// <summary>Feeds this frame's edge signal into the buffer.</summary>
        public void Feed(bool pressedThisFrame)
        {
            if (!pressedThisFrame) return;
            _pressedAt = Time.time;
            _armed = true;
        }

        /// <summary>True while a press is still fresh enough to act on.</summary>
        public bool Pending => _armed && Time.time - _pressedAt <= Window;

        /// <summary>Consumes a pending press. Returns false if nothing was buffered.</summary>
        public bool Consume()
        {
            if (!Pending) return false;
            _armed = false;
            return true;
        }

        public void Clear()
        {
            _armed = false;
            _pressedAt = float.NegativeInfinity;
        }

        /// <summary>How long ago the press happened, for scoring "just barely" timings.</summary>
        public float Age => _armed ? Time.time - _pressedAt : float.PositiveInfinity;
    }

    /// <summary>
    /// A window of time that stays open after a condition stops being true.
    /// Used for coyote time on ledges and for parry windows.
    /// </summary>
    public struct GraceTimer
    {
        private float _lastTrue;
        private bool _ever;

        public void Set(bool condition)
        {
            if (!condition) return;
            _lastTrue = Time.time;
            _ever = true;
        }

        public bool WithinLast(float seconds) => _ever && Time.time - _lastTrue <= seconds;

        public void Clear()
        {
            _ever = false;
            _lastTrue = float.NegativeInfinity;
        }
    }
}
