using System;
using System.Collections.Generic;
using UnityEngine;

namespace Glowpulse.Core.Settings
{
    /// <summary>
    /// Which key each action is on.
    ///
    /// Two keys per action, because the shipped controls genuinely use two for
    /// several of them - dodge is Ctrl or C, block is right mouse or Q - and
    /// collapsing that to one would quietly change the game while claiming only
    /// to add rebinding. Gamepad buttons are deliberately not in here: they are
    /// fixed, so a player who rebinds their keyboard into a corner can still
    /// play, and so the controls list stays about the keyboard it is showing.
    ///
    /// <see cref="KeyCode"/> covers mouse buttons as well as keys, so
    /// "left mouse" is just another binding rather than a special case.
    /// </summary>
    [Serializable]
    public sealed class InputBindings
    {
        [SerializeField] private KeyCode[] _primary = new KeyCode[(int)GameAction.Count];
        [SerializeField] private KeyCode[] _secondary = new KeyCode[(int)GameAction.Count];

        public InputBindings()
        {
            ResetToDefaults();
        }

        public KeyCode Primary(GameAction action) => Get(_primary, action);

        public KeyCode Secondary(GameAction action) => Get(_secondary, action);

        private static KeyCode Get(KeyCode[] table, GameAction action)
        {
            int index = (int)action;
            return index >= 0 && index < table.Length ? table[index] : KeyCode.None;
        }

        /// <summary>
        /// Puts an action on a key, clearing that key from wherever else it was.
        ///
        /// Stealing rather than refusing is the behaviour people expect: you press
        /// the key you want and it becomes yours. The action it was taken from is
        /// left visibly unbound, which is a problem the player can see and fix,
        /// unlike two actions silently sharing one key.
        /// </summary>
        public void Rebind(GameAction action, KeyCode key, bool secondary = false)
        {
            int index = (int)action;
            if (index < 0 || index >= (int)GameAction.Count) return;
            if (key == KeyCode.None) return;

            ClearKey(key, action);

            if (secondary) _secondary[index] = key;
            else _primary[index] = key;

            // A slot cannot hold the same key twice; that would look like a
            // conflict with itself in the controls list.
            if (secondary && _primary[index] == key) _primary[index] = KeyCode.None;
            else if (!secondary && _secondary[index] == key) _secondary[index] = KeyCode.None;
        }

        /// <summary>Removes a key from every action except the one claiming it.</summary>
        private void ClearKey(KeyCode key, GameAction except)
        {
            for (int i = 0; i < (int)GameAction.Count; i++)
            {
                if (i == (int)except) continue;
                if (_primary[i] == key) _primary[i] = KeyCode.None;
                if (_secondary[i] == key) _secondary[i] = KeyCode.None;
            }
        }

        /// <summary>The action already using a key, or null when it is free.</summary>
        public GameAction? Conflict(KeyCode key, GameAction ignoring)
        {
            if (key == KeyCode.None) return null;

            for (int i = 0; i < (int)GameAction.Count; i++)
            {
                if (i == (int)ignoring) continue;
                if (_primary[i] == key || _secondary[i] == key) return (GameAction)i;
            }

            return null;
        }

        /// <summary>True when no action is left with nothing on it.</summary>
        public bool IsComplete()
        {
            for (int i = 0; i < (int)GameAction.Count; i++)
                if (_primary[i] == KeyCode.None && _secondary[i] == KeyCode.None) return false;

            return true;
        }

        /// <summary>Actions currently left unbound, for the settings screen to flag.</summary>
        public void CollectUnbound(List<GameAction> into)
        {
            if (into == null) return;
            into.Clear();

            for (int i = 0; i < (int)GameAction.Count; i++)
                if (_primary[i] == KeyCode.None && _secondary[i] == KeyCode.None)
                    into.Add((GameAction)i);
        }

        /// <summary>Human-readable label for the controls list, e.g. "LMB" or "Left Ctrl".</summary>
        public string Label(GameAction action)
        {
            KeyCode a = Primary(action);
            KeyCode b = Secondary(action);

            if (a == KeyCode.None && b == KeyCode.None) return "Unbound";
            if (b == KeyCode.None) return KeyName(a);
            if (a == KeyCode.None) return KeyName(b);
            return KeyName(a) + " / " + KeyName(b);
        }

        public static string KeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.None: return "-";
                case KeyCode.Mouse0: return "LMB";
                case KeyCode.Mouse1: return "RMB";
                case KeyCode.Mouse2: return "MMB";
                case KeyCode.LeftControl: return "Left Ctrl";
                case KeyCode.RightControl: return "Right Ctrl";
                case KeyCode.LeftShift: return "Left Shift";
                case KeyCode.RightShift: return "Right Shift";
                case KeyCode.LeftAlt: return "Left Alt";
                case KeyCode.RightAlt: return "Right Alt";
                case KeyCode.Space: return "Space";
                case KeyCode.Return: return "Enter";
                case KeyCode.Escape: return "Esc";
                case KeyCode.UpArrow: return "Up";
                case KeyCode.DownArrow: return "Down";
                case KeyCode.LeftArrow: return "Left";
                case KeyCode.RightArrow: return "Right";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                default: return key.ToString();
            }
        }

        /// <summary>
        /// The shipped controls, exactly as they were before rebinding existed.
        /// Reset has to land back on the game people already know.
        /// </summary>
        public void ResetToDefaults()
        {
            if (_primary == null || _primary.Length != (int)GameAction.Count)
                _primary = new KeyCode[(int)GameAction.Count];
            if (_secondary == null || _secondary.Length != (int)GameAction.Count)
                _secondary = new KeyCode[(int)GameAction.Count];

            Set(GameAction.MoveForward, KeyCode.W, KeyCode.UpArrow);
            Set(GameAction.MoveBackward, KeyCode.S, KeyCode.DownArrow);
            Set(GameAction.MoveLeft, KeyCode.A, KeyCode.LeftArrow);
            Set(GameAction.MoveRight, KeyCode.D, KeyCode.RightArrow);
            Set(GameAction.Sprint, KeyCode.LeftShift, KeyCode.RightShift);
            Set(GameAction.Jump, KeyCode.Space, KeyCode.None);
            Set(GameAction.Dodge, KeyCode.LeftControl, KeyCode.C);
            Set(GameAction.Attack, KeyCode.Mouse0, KeyCode.None);
            Set(GameAction.HeavyAttack, KeyCode.F, KeyCode.None);
            Set(GameAction.Block, KeyCode.Mouse1, KeyCode.Q);
            Set(GameAction.Grab, KeyCode.G, KeyCode.None);
            Set(GameAction.LockOn, KeyCode.Tab, KeyCode.Mouse2);
            Set(GameAction.Interact, KeyCode.E, KeyCode.None);
        }

        private void Set(GameAction action, KeyCode primary, KeyCode secondary)
        {
            _primary[(int)action] = primary;
            _secondary[(int)action] = secondary;
        }

        public InputBindings Clone()
        {
            var copy = new InputBindings();
            Array.Copy(_primary, copy._primary, _primary.Length);
            Array.Copy(_secondary, copy._secondary, _secondary.Length);
            return copy;
        }

        public void CopyFrom(InputBindings other)
        {
            if (other == null) return;
            Array.Copy(other._primary, _primary, _primary.Length);
            Array.Copy(other._secondary, _secondary, _secondary.Length);
        }

        /// <summary>
        /// Repairs a table loaded from a save written by a different version -
        /// a shorter array, or an action added since. Missing entries fall back to
        /// the default for that action rather than leaving it unplayable.
        /// </summary>
        public void Repair()
        {
            var defaults = new InputBindings();

            _primary = Resize(_primary, defaults._primary);
            _secondary = Resize(_secondary, defaults._secondary);

            for (int i = 0; i < (int)GameAction.Count; i++)
            {
                if (_primary[i] != KeyCode.None || _secondary[i] != KeyCode.None) continue;
                _primary[i] = defaults._primary[i];
                _secondary[i] = defaults._secondary[i];
            }
        }

        private static KeyCode[] Resize(KeyCode[] current, KeyCode[] defaults)
        {
            if (current != null && current.Length == defaults.Length) return current;

            var grown = new KeyCode[defaults.Length];
            for (int i = 0; i < grown.Length; i++)
                grown[i] = current != null && i < current.Length ? current[i] : defaults[i];

            return grown;
        }
    }
}
