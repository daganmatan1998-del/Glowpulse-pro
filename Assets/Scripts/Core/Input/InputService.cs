using UnityEngine;

namespace Glowpulse.Core.InputSystem
{
    /// <summary>
    /// Global access point for player input. Holds the active provider and a
    /// suppression counter so menus, cutscenes and death can silence gameplay
    /// input without every system needing to know why.
    /// </summary>
    public static class InputService
    {
        private static IInputProvider _current;
        private static readonly NullInputProvider Null = new NullInputProvider();
        private static int _suppressCount;

        /// <summary>
        /// The provider gameplay reads from. Returns a null provider while input
        /// is suppressed, so callers never have to check a flag.
        /// </summary>
        public static IInputProvider Current
        {
            get
            {
                if (_suppressCount > 0) return Null;
                return _current ?? (_current = new LegacyInputProvider());
            }
        }

        /// <summary>The real provider, ignoring suppression. Used by the pump and by menus.</summary>
        public static IInputProvider Raw => _current ?? (_current = new LegacyInputProvider());

        public static bool IsSuppressed => _suppressCount > 0;

        public static void SetProvider(IInputProvider provider)
        {
            _current = provider ?? new LegacyInputProvider();
        }

        /// <summary>Balanced with <see cref="PopSuppress"/>. Nesting is supported.</summary>
        public static void PushSuppress()
        {
            _suppressCount++;
            Raw.Flush();
        }

        public static void PopSuppress()
        {
            _suppressCount = Mathf.Max(0, _suppressCount - 1);
        }

        public static void ResetSuppression() => _suppressCount = 0;

        /// <summary>Cleared between play sessions so domain reload settings do not leak state.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _current = null;
            _suppressCount = 0;
        }
    }

    /// <summary>Reports neutral input for everything. Used while input is suppressed.</summary>
    public sealed class NullInputProvider : IInputProvider
    {
        public Vector2 Move => Vector2.zero;
        public Vector2 Look => Vector2.zero;
        public bool SprintHeld => false;
        public bool BlockHeld => false;
        public bool JumpHeld => false;
        public bool JumpPressed => false;
        public bool DodgePressed => false;
        public bool LightAttackPressed => false;
        public bool HeavyAttackPressed => false;
        public bool GrabPressed => false;
        public bool InteractPressed => false;
        public bool LockOnPressed => false;
        public bool PausePressed => false;
        public int LockTargetCycle => 0;
        public bool UsingGamepad => false;
        public void Sample(float unscaledDeltaTime) { }
        public void Flush() { }
    }
}
