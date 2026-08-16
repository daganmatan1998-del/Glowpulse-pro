using System.Collections.Generic;
using Glowpulse.Core.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace Glowpulse.UI
{
    /// <summary>
    /// The settings panel: difficulty, mouse look, feel, and the controls list.
    ///
    /// Changes apply the moment they are made rather than on an Apply button -
    /// a sensitivity slider you cannot feel while dragging is useless, and the
    /// same is true of shake and volume. Writing to disk is deferred by
    /// <see cref="GameSettings"/>, so immediate feedback does not mean a file
    /// write per frame.
    /// </summary>
    public sealed class SettingsScreen : MonoBehaviour
    {
        /// <summary>Keys that are never accepted as a binding.</summary>
        private static readonly KeyCode[] Reserved = { KeyCode.Escape };

        private RectTransform _root;
        private RectTransform _controlsList;

        private readonly List<Button> _difficultyButtons = new List<Button>(3);
        private readonly List<Text> _bindingLabels = new List<Text>(16);
        private readonly List<GameAction> _unbound = new List<GameAction>(4);

        private Text _sensitivityReadout;
        private Text _shakeReadout;
        private Text _volumeReadout;
        private Text _difficultyBlurb;
        private Text _status;

        private Slider _sensitivity;
        private Slider _shake;
        private Slider _volume;
        private Toggle _invert;
        private Toggle _slowMotion;

        /// <summary>The action currently waiting for a key, or null.</summary>
        private GameAction? _listeningFor;

        private bool _listeningSecondary;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        /// <summary>True while a rebind is capturing keys, so the pause menu ignores Escape.</summary>
        public bool IsCapturingKey => _listeningFor.HasValue;

        public static SettingsScreen Create(Transform parent)
        {
            var go = new GameObject("Settings");
            go.transform.SetParent(parent, false);
            var screen = go.AddComponent<SettingsScreen>();
            screen.Build();
            screen.Hide();
            return screen;
        }

        public void Show()
        {
            Refresh();
            _root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            CancelCapture();

            // Leaving the menu is the natural moment to commit; the player may
            // quit from the desktop rather than through the game.
            GameSettings.Flush();
            _root.gameObject.SetActive(false);
        }

        // ---- construction --------------------------------------------------------

        private void Build()
        {
            _root = UiKit.Stretch("Root", transform);
            UiKit.Panel("Veil", _root, UiKit.Veil);

            RectTransform panel = UiKit.Rect("Panel", _root);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(900f, 880f);

            var background = panel.gameObject.AddComponent<Image>();
            background.color = UiKit.PanelFill;

            RectTransform content = UiKit.Stretch("Content", panel, 34f);
            UiKit.Column(content, 10f);

            Text title = UiKit.Label("Title", content, "SETTINGS", UiKit.TitleSize, UiKit.Bone);
            title.fontStyle = FontStyle.Bold;
            UiKit.Size(title.gameObject, height: 56f);

            BuildDifficulty(content);
            UiKit.Divider(content);
            BuildLook(content);
            UiKit.Divider(content);
            BuildFeel(content);
            UiKit.Divider(content);
            BuildControls(content);

            _status = UiKit.Label("Status", content, string.Empty, UiKit.SmallSize, UiKit.Sun);
            UiKit.Size(_status.gameObject, height: 22f);
        }

        private void BuildDifficulty(Transform parent)
        {
            UiKit.Heading("Difficulty", parent);

            RectTransform row = UiKit.Rect("Difficulty Row", parent);
            UiKit.Row(row, 10f);
            UiKit.Size(row.gameObject, height: 44f);

            _difficultyButtons.Clear();
            foreach (Difficulty d in System.Enum.GetValues(typeof(Difficulty)))
            {
                Difficulty captured = d;
                Button button = UiKit.Button(DifficultyProfile.DisplayName(d), row,
                    () => SetDifficulty(captured));
                UiKit.Size(button.gameObject, flexibleWidth: 1f);
                _difficultyButtons.Add(button);
            }

            _difficultyBlurb = UiKit.Label("Blurb", parent, string.Empty, UiKit.SmallSize, UiKit.Muted);
            UiKit.Size(_difficultyBlurb.gameObject, height: 24f);
        }

        private void BuildLook(Transform parent)
        {
            UiKit.Heading("Camera", parent);

            _sensitivity = UiKit.Slider("Mouse Sensitivity", parent,
                GameSettings.MinSensitivity, GameSettings.MaxSensitivity,
                GameSettings.MouseSensitivity,
                v => { GameSettings.MouseSensitivity = v; RefreshReadouts(); },
                out _sensitivityReadout);

            _invert = Checkbox("Invert Vertical Look", parent, GameSettings.InvertY,
                v => GameSettings.InvertY = v);
        }

        private void BuildFeel(Transform parent)
        {
            UiKit.Heading("Feel and sound", parent);

            _shake = UiKit.Slider("Camera Shake", parent, 0f, 1f, GameSettings.ShakeScale,
                v => { GameSettings.ShakeScale = v; RefreshReadouts(); }, out _shakeReadout);

            _volume = UiKit.Slider("Master Volume", parent, 0f, 1f, GameSettings.MasterVolume,
                v => { GameSettings.MasterVolume = v; RefreshReadouts(); }, out _volumeReadout);

            _slowMotion = Checkbox("Slow Motion On Big Hits", parent, GameSettings.SlowMotionEnabled,
                v => GameSettings.SlowMotionEnabled = v);
        }

        private void BuildControls(Transform parent)
        {
            RectTransform header = UiKit.Rect("Controls Header", parent);
            UiKit.Row(header, 10f);
            UiKit.Size(header.gameObject, height: 34f);

            Text heading = UiKit.Label("Heading", header, "CONTROLS", UiKit.HeadingSize, UiKit.Sun);
            heading.fontStyle = FontStyle.Bold;
            UiKit.Size(heading.gameObject, flexibleWidth: 1f);

            Button reset = UiKit.Button("Reset to Defaults", header, ResetBindings);
            UiKit.Size(reset.gameObject, width: 200f, height: 32f);

            _controlsList = UiKit.Rect("Controls", parent);
            UiKit.Column(_controlsList, 3f);
            UiKit.Size(_controlsList.gameObject, height: 330f);

            _bindingLabels.Clear();
            foreach (GameAction action in GameActions.Listed)
                BuildBindingRow(action);
        }

        private void BuildBindingRow(GameAction action)
        {
            RectTransform row = UiKit.Rect("Bind " + action, _controlsList);
            UiKit.Row(row, 10f);
            UiKit.Size(row.gameObject, height: 24f);

            Text name = UiKit.Label("Name", row, GameActions.DisplayName(action), UiKit.SmallSize,
                UiKit.Bone);
            UiKit.Size(name.gameObject, width: 220f);

            Text keys = UiKit.Label("Keys", row, string.Empty, UiKit.SmallSize, UiKit.Cyan);
            UiKit.Size(keys.gameObject, flexibleWidth: 1f);
            _bindingLabels.Add(keys);

            GameAction captured = action;
            Button primary = UiKit.Button("Set", row, () => BeginCapture(captured, false));
            UiKit.Size(primary.gameObject, width: 74f, height: 22f);

            Button secondary = UiKit.Button("Alt", row, () => BeginCapture(captured, true));
            UiKit.Size(secondary.gameObject, width: 62f, height: 22f);
        }

        private Toggle Checkbox(string label, Transform parent, bool value,
            System.Action<bool> onChanged)
        {
            RectTransform row = UiKit.Rect("Toggle " + label, parent);
            UiKit.Row(row, 12f);
            UiKit.Size(row.gameObject, height: 30f);

            Text name = UiKit.Label("Label", row, label, UiKit.BodySize, UiKit.Bone);
            UiKit.Size(name.gameObject, width: 320f);

            RectTransform box = UiKit.Rect("Box", row);
            UiKit.Size(box.gameObject, width: 22f, height: 22f);
            var boxImage = box.gameObject.AddComponent<Image>();
            boxImage.color = new Color(0f, 0f, 0f, 0.55f);

            RectTransform tick = UiKit.Stretch("Tick", box, 4f);
            var tickImage = tick.gameObject.AddComponent<Image>();
            tickImage.color = UiKit.Cyan;

            var toggle = box.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = boxImage;
            toggle.graphic = tickImage;
            toggle.isOn = value;
            toggle.onValueChanged.AddListener(v => onChanged(v));
            return toggle;
        }

        // ---- behaviour -----------------------------------------------------------

        private void SetDifficulty(Difficulty difficulty)
        {
            GameSettings.Difficulty = difficulty;

            // Difficulty scales enemies as they spawn, so anyone already on the
            // street keeps the stats they were built with. Saying so beats a
            // player concluding the setting does nothing.
            Message($"Difficulty set to {DifficultyProfile.DisplayName(difficulty)}. " +
                    "Applies to enemies from the next stage.");
            Refresh();
        }

        private void ResetBindings()
        {
            GameSettings.ResetBindings();
            CancelCapture();
            Message("Controls reset to defaults.");
            Refresh();
        }

        private void BeginCapture(GameAction action, bool secondary)
        {
            _listeningFor = action;
            _listeningSecondary = secondary;
            Message($"Press a key for {GameActions.DisplayName(action)}. Escape cancels.");
            Refresh();
        }

        private void CancelCapture()
        {
            _listeningFor = null;
            _listeningSecondary = false;
        }

        private void Update()
        {
            if (!IsOpen || !_listeningFor.HasValue) return;
            if (!Input.anyKeyDown) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelCapture();
                Message("Rebinding cancelled.");
                Refresh();
                return;
            }

            KeyCode pressed = ReadPressedKey();
            if (pressed == KeyCode.None) return;

            GameAction action = _listeningFor.Value;
            GameAction? stolenFrom = GameSettings.Bindings.Conflict(pressed, action);

            GameSettings.Rebind(action, pressed, _listeningSecondary);
            CancelCapture();

            Message(stolenFrom.HasValue
                ? $"{InputBindings.KeyName(pressed)} taken from {GameActions.DisplayName(stolenFrom.Value)}."
                : $"{GameActions.DisplayName(action)} is now {InputBindings.KeyName(pressed)}.");

            Refresh();
        }

        /// <summary>
        /// The key the player just pressed, or None if it was one we refuse.
        ///
        /// Scanning the enum is the only way to identify a key with the legacy
        /// input manager, and it only happens on the frames a rebind is armed and
        /// something was actually pressed.
        /// </summary>
        private static KeyCode ReadPressedKey()
        {
            foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (!Input.GetKeyDown(key)) continue;
                if (IsReserved(key)) continue;

                // Joystick buttons are not rebindable: the gamepad layout is
                // fixed so a keyboard rebind can never strand a pad player.
                if (key >= KeyCode.JoystickButton0) continue;

                return key;
            }

            return KeyCode.None;
        }

        private static bool IsReserved(KeyCode key)
        {
            for (int i = 0; i < Reserved.Length; i++)
                if (Reserved[i] == key) return true;
            return false;
        }

        private void Message(string text)
        {
            if (_status != null) _status.text = text;
        }

        public void Refresh()
        {
            RefreshDifficulty();
            RefreshReadouts();
            RefreshBindings();
        }

        private void RefreshDifficulty()
        {
            for (int i = 0; i < _difficultyButtons.Count; i++)
            {
                bool selected = (Difficulty)i == GameSettings.Difficulty;
                Image image = _difficultyButtons[i].targetGraphic as Image;
                if (image != null)
                    image.color = selected ? UiKit.Sun : new Color(1f, 1f, 1f, 0.06f);

                var text = _difficultyButtons[i].GetComponentInChildren<Text>();
                if (text != null) text.color = selected ? UiKit.Ink : UiKit.Bone;
            }

            if (_difficultyBlurb != null)
                _difficultyBlurb.text = DifficultyProfile.Describe(GameSettings.Difficulty);
        }

        private void RefreshReadouts()
        {
            if (_sensitivityReadout != null)
                _sensitivityReadout.text = GameSettings.MouseSensitivity.ToString("0.00");
            if (_shakeReadout != null)
                _shakeReadout.text = Mathf.RoundToInt(GameSettings.ShakeScale * 100f) + "%";
            if (_volumeReadout != null)
                _volumeReadout.text = Mathf.RoundToInt(GameSettings.MasterVolume * 100f) + "%";

            // The sliders themselves are refreshed too, so a reset or a load is
            // reflected rather than leaving the handle where the player left it.
            if (_sensitivity != null) _sensitivity.SetValueWithoutNotify(GameSettings.MouseSensitivity);
            if (_shake != null) _shake.SetValueWithoutNotify(GameSettings.ShakeScale);
            if (_volume != null) _volume.SetValueWithoutNotify(GameSettings.MasterVolume);
            if (_invert != null) _invert.SetIsOnWithoutNotify(GameSettings.InvertY);
            if (_slowMotion != null) _slowMotion.SetIsOnWithoutNotify(GameSettings.SlowMotionEnabled);
        }

        private void RefreshBindings()
        {
            InputBindings binds = GameSettings.Bindings;
            binds.CollectUnbound(_unbound);

            for (int i = 0; i < _bindingLabels.Count && i < GameActions.Listed.Length; i++)
            {
                GameAction action = GameActions.Listed[i];
                Text label = _bindingLabels[i];

                bool listening = _listeningFor.HasValue && _listeningFor.Value == action;
                bool missing = _unbound.Contains(action);

                label.text = listening ? "Press a key..." : binds.Label(action);
                label.color = listening ? UiKit.Sun : missing ? UiKit.Danger : UiKit.Cyan;
            }
        }
    }
}
