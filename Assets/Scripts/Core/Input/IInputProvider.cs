using UnityEngine;

namespace Glowpulse.Core.InputSystem
{
    /// <summary>
    /// Every gameplay system reads input through this interface, never through
    /// UnityEngine.Input directly. Swapping to the new Input System, a gamepad
    /// rebinding layer or a replay/AI driver is a matter of assigning a different
    /// implementation to <see cref="InputService.Current"/>.
    /// </summary>
    public interface IInputProvider
    {
        /// <summary>Left stick / WASD, magnitude clamped to 1.</summary>
        Vector2 Move { get; }

        /// <summary>Right stick / mouse delta, already scaled to degrees per frame.</summary>
        Vector2 Look { get; }

        bool SprintHeld { get; }
        bool BlockHeld { get; }

        /// <summary>True while the jump button is down, which drives variable jump height.</summary>
        bool JumpHeld { get; }

        bool JumpPressed { get; }
        bool DodgePressed { get; }
        bool LightAttackPressed { get; }
        bool HeavyAttackPressed { get; }
        bool GrabPressed { get; }
        bool InteractPressed { get; }
        bool LockOnPressed { get; }
        bool PausePressed { get; }

        /// <summary>-1 / 0 / +1 edge signal for cycling lock-on targets.</summary>
        int LockTargetCycle { get; }

        /// <summary>True when the provider is reading from a gamepad, so UI can swap prompts.</summary>
        bool UsingGamepad { get; }

        /// <summary>Called once per frame by the input pump before any consumer reads it.</summary>
        void Sample(float unscaledDeltaTime);

        /// <summary>Clears held state, e.g. when the game is paused or a cutscene starts.</summary>
        void Flush();
    }
}
