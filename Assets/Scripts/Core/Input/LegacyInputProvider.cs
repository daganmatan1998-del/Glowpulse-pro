using UnityEngine;

namespace Glowpulse.Core.InputSystem
{
    /// <summary>
    /// Default provider built on Unity's built-in Input Manager, so the project
    /// runs on a clean install with no extra packages. Only the default axes
    /// ("Horizontal", "Vertical", "Mouse X", "Mouse Y") are used; everything else
    /// reads raw keys and buttons, which avoids depending on a customised
    /// InputManager.asset.
    /// </summary>
    public sealed class LegacyInputProvider : IInputProvider
    {
        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool BlockHeld { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool DodgePressed { get; private set; }
        public bool LightAttackPressed { get; private set; }
        public bool HeavyAttackPressed { get; private set; }
        public bool GrabPressed { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool LockOnPressed { get; private set; }
        public bool PausePressed { get; private set; }
        public int LockTargetCycle { get; private set; }
        public bool UsingGamepad { get; private set; }

        public float MouseSensitivity = 2.2f;
        public float GamepadSensitivity = 220f;
        public bool InvertY;

        private float _gamepadIdleTimer;

        public void Sample(float unscaledDeltaTime)
        {
            Vector2 stick = new Vector2(
                UnityEngine.Input.GetAxisRaw("Horizontal"),
                UnityEngine.Input.GetAxisRaw("Vertical"));
            if (stick.sqrMagnitude > 1f) stick.Normalize();
            Move = stick;

            Vector2 mouse = new Vector2(
                UnityEngine.Input.GetAxisRaw("Mouse X"),
                UnityEngine.Input.GetAxisRaw("Mouse Y"));

            Vector2 pad = ReadRightStick();
            DetectDevice(pad, mouse, unscaledDeltaTime);

            Vector2 look = UsingGamepad
                ? pad * (GamepadSensitivity * unscaledDeltaTime)
                : mouse * MouseSensitivity;
            if (InvertY) look.y = -look.y;
            Look = look;

            SprintHeld = UnityEngine.Input.GetKey(KeyCode.LeftShift)
                         || UnityEngine.Input.GetKey(KeyCode.RightShift)
                         || UnityEngine.Input.GetKey(KeyCode.JoystickButton8);

            BlockHeld = UnityEngine.Input.GetMouseButton(1)
                        || UnityEngine.Input.GetKey(KeyCode.Q)
                        || UnityEngine.Input.GetKey(KeyCode.JoystickButton5);

            JumpPressed = UnityEngine.Input.GetKeyDown(KeyCode.Space)
                          || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton0);

            JumpHeld = UnityEngine.Input.GetKey(KeyCode.Space)
                       || UnityEngine.Input.GetKey(KeyCode.JoystickButton0);

            DodgePressed = UnityEngine.Input.GetKeyDown(KeyCode.LeftControl)
                           || UnityEngine.Input.GetKeyDown(KeyCode.C)
                           || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton1);

            LightAttackPressed = UnityEngine.Input.GetMouseButtonDown(0)
                                 || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton2);

            HeavyAttackPressed = UnityEngine.Input.GetKeyDown(KeyCode.F)
                                 || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton3);

            GrabPressed = UnityEngine.Input.GetKeyDown(KeyCode.G)
                          || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton4);

            InteractPressed = UnityEngine.Input.GetKeyDown(KeyCode.E);

            LockOnPressed = UnityEngine.Input.GetMouseButtonDown(2)
                            || UnityEngine.Input.GetKeyDown(KeyCode.Tab)
                            || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton9);

            PausePressed = UnityEngine.Input.GetKeyDown(KeyCode.Escape)
                           || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton7);

            int cycle = 0;
            float wheel = UnityEngine.Input.GetAxisRaw("Mouse ScrollWheel");
            if (wheel > 0.01f) cycle = 1;
            else if (wheel < -0.01f) cycle = -1;
            if (UnityEngine.Input.GetKeyDown(KeyCode.RightBracket)) cycle = 1;
            if (UnityEngine.Input.GetKeyDown(KeyCode.LeftBracket)) cycle = -1;
            LockTargetCycle = cycle;
        }

        public void Flush()
        {
            Move = Vector2.zero;
            Look = Vector2.zero;
            SprintHeld = BlockHeld = JumpHeld = false;
            JumpPressed = DodgePressed = LightAttackPressed = HeavyAttackPressed = false;
            GrabPressed = InteractPressed = LockOnPressed = PausePressed = false;
            LockTargetCycle = 0;
        }

        private void DetectDevice(Vector2 pad, Vector2 mouse, float dt)
        {
            if (pad.sqrMagnitude > 0.04f)
            {
                UsingGamepad = true;
                _gamepadIdleTimer = 0f;
                return;
            }

            if (mouse.sqrMagnitude > 0.0001f)
            {
                UsingGamepad = false;
                return;
            }

            if (UsingGamepad)
            {
                _gamepadIdleTimer += dt;
                if (_gamepadIdleTimer > 4f) UsingGamepad = false;
            }
        }

        // The right-stick axes only exist on a customised InputManager.asset.
        // Probing costs an exception, so it happens exactly once per session
        // rather than every frame.
        private static bool _rightStickProbed;
        private static bool _hasRightStick;

        private static Vector2 ReadRightStick()
        {
            if (!_rightStickProbed)
            {
                _rightStickProbed = true;
                try
                {
                    UnityEngine.Input.GetAxisRaw(RightStickX);
                    UnityEngine.Input.GetAxisRaw(RightStickY);
                    _hasRightStick = true;
                }
                catch (System.ArgumentException)
                {
                    _hasRightStick = false;
                }
            }

            if (!_hasRightStick) return Vector2.zero;
            return new Vector2(
                UnityEngine.Input.GetAxisRaw(RightStickX),
                UnityEngine.Input.GetAxisRaw(RightStickY));
        }

        private const string RightStickX = "RightStickX";
        private const string RightStickY = "RightStickY";
    }
}
