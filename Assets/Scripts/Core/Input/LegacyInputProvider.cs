using Glowpulse.Core.Settings;
using UnityEngine;

namespace Glowpulse.Core.InputSystem
{
    /// <summary>
    /// Default provider built on Unity's built-in Input Manager, so the project
    /// runs on a clean install with no extra packages. Only the default axes
    /// ("Horizontal", "Vertical", "Mouse X", "Mouse Y") are used; everything else
    /// reads raw keys and buttons, which avoids depending on a customised
    /// InputManager.asset.
    ///
    /// Keys come from <see cref="GameSettings.Bindings"/> rather than being
    /// hard-coded, so rebinding is a data change; the gamepad buttons stay fixed,
    /// which means a player who rebinds their keyboard into a corner can still
    /// play. Movement reads the bound keys directly instead of the "Horizontal"
    /// and "Vertical" axes, because those axes live in InputManager.asset and
    /// cannot be rebound at runtime.
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

        /// <summary>Base mouse scale. The player's setting multiplies this.</summary>
        public float MouseSensitivity = 2.2f;

        public float GamepadSensitivity = 220f;

        /// <summary>Forced inversion, on top of the player's setting.</summary>
        public bool InvertY;

        private float _gamepadIdleTimer;

        public void Sample(float unscaledDeltaTime)
        {
            InputBindings binds = GameSettings.Bindings;

            // Read the bound movement keys, then fall back to the axes for a
            // gamepad stick, which has no keys to bind.
            var stick = new Vector2(
                Axis(binds, GameAction.MoveRight, GameAction.MoveLeft),
                Axis(binds, GameAction.MoveForward, GameAction.MoveBackward));

            if (stick.sqrMagnitude < 0.01f)
                stick = new Vector2(
                    UnityEngine.Input.GetAxisRaw("Horizontal"),
                    UnityEngine.Input.GetAxisRaw("Vertical"));

            if (stick.sqrMagnitude > 1f) stick.Normalize();
            Move = stick;

            Vector2 mouse = new Vector2(
                UnityEngine.Input.GetAxisRaw("Mouse X"),
                UnityEngine.Input.GetAxisRaw("Mouse Y"));

            Vector2 pad = ReadRightStick();
            DetectDevice(pad, mouse, unscaledDeltaTime);

            // The player's sensitivity multiplies the base scale, so 1 is exactly
            // the feel the camera was tuned with and the slider cannot produce a
            // camera that will not turn.
            float sensitivity = GameSettings.MouseSensitivity;

            Vector2 look = UsingGamepad
                ? pad * (GamepadSensitivity * sensitivity * unscaledDeltaTime)
                : mouse * (MouseSensitivity * sensitivity);

            if (InvertY != GameSettings.InvertY) look.y = -look.y;
            Look = look;

            SprintHeld = Held(binds, GameAction.Sprint)
                         || UnityEngine.Input.GetKey(KeyCode.JoystickButton8);

            BlockHeld = Held(binds, GameAction.Block)
                        || UnityEngine.Input.GetKey(KeyCode.JoystickButton5);

            JumpPressed = Pressed(binds, GameAction.Jump)
                          || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton0);

            JumpHeld = Held(binds, GameAction.Jump)
                       || UnityEngine.Input.GetKey(KeyCode.JoystickButton0);

            DodgePressed = Pressed(binds, GameAction.Dodge)
                           || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton1);

            LightAttackPressed = Pressed(binds, GameAction.Attack)
                                 || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton2);

            HeavyAttackPressed = Pressed(binds, GameAction.HeavyAttack)
                                 || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton3);

            GrabPressed = Pressed(binds, GameAction.Grab)
                          || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton4);

            InteractPressed = Pressed(binds, GameAction.Interact);

            LockOnPressed = Pressed(binds, GameAction.LockOn)
                            || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton9);

            // Pause is deliberately not rebindable. It is the way out of a menu
            // that can rebind everything else, so it has to stay somewhere known.
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

        // ---- binding lookups -------------------------------------------------

        private static bool Held(InputBindings binds, GameAction action)
        {
            KeyCode a = binds.Primary(action), b = binds.Secondary(action);
            return (a != KeyCode.None && UnityEngine.Input.GetKey(a))
                   || (b != KeyCode.None && UnityEngine.Input.GetKey(b));
        }

        private static bool Pressed(InputBindings binds, GameAction action)
        {
            KeyCode a = binds.Primary(action), b = binds.Secondary(action);
            return (a != KeyCode.None && UnityEngine.Input.GetKeyDown(a))
                   || (b != KeyCode.None && UnityEngine.Input.GetKeyDown(b));
        }

        private static float Axis(InputBindings binds, GameAction positive, GameAction negative)
        {
            float value = 0f;
            if (Held(binds, positive)) value += 1f;
            if (Held(binds, negative)) value -= 1f;
            return value;
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
