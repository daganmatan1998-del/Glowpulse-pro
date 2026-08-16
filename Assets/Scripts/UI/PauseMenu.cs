using Glowpulse.Core.Bootstrap;
using Glowpulse.Core.InputSystem;
using Glowpulse.Core.Settings;
using Glowpulse.Core.Timing;
using UnityEngine;
using UnityEngine.UI;

namespace Glowpulse.UI
{
    /// <summary>
    /// Escape opens this; it stops the world, frees the cursor and gets out of the
    /// way again.
    ///
    /// Pausing goes through <see cref="TimeController"/> rather than setting
    /// <c>Time.timeScale</c> here, because that controller already arbitrates
    /// between pause, hit stop and slow motion - writing the scale directly is how
    /// a game ends up permanently in slow motion after being paused mid-punch.
    /// </summary>
    [DefaultExecutionOrder(-25)]
    public sealed class PauseMenu : MonoBehaviour
    {
        private RectTransform _root;
        private RectTransform _buttons;
        private SettingsScreen _settings;
        private bool _open;

        public bool IsOpen => _open;

        public static PauseMenu Install(Transform parent)
        {
            var go = new GameObject("Pause Menu");
            go.transform.SetParent(parent, false);
            var menu = go.AddComponent<PauseMenu>();
            menu.Build();
            return menu;
        }

        private void Build()
        {
            _root = UiKit.Stretch("Root", transform);
            UiKit.Panel("Veil", _root, UiKit.Veil);

            RectTransform panel = UiKit.Rect("Panel", _root);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(420f, 400f);

            var background = panel.gameObject.AddComponent<Image>();
            background.color = UiKit.PanelFill;

            _buttons = UiKit.Stretch("Content", panel, 30f);
            UiKit.Column(_buttons, 12f, TextAnchor.MiddleCenter);

            Text title = UiKit.Label("Title", _buttons, "PAUSED", UiKit.TitleSize, UiKit.Bone,
                TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            UiKit.Size(title.gameObject, height: 60f);

            UiKit.Button("Resume", _buttons, Close, accent: true);
            UiKit.Button("Settings", _buttons, OpenSettings);

            UiKit.Divider(_buttons);

            Text hint = UiKit.Label("Hint", _buttons,
                "Escape closes this menu.", UiKit.SmallSize, UiKit.Muted, TextAnchor.MiddleCenter);
            UiKit.Size(hint.gameObject, height: 24f);

            _settings = SettingsScreen.Create(transform);

            _root.gameObject.SetActive(false);
        }

        private void Update()
        {
            IInputProvider input = InputService.Current;
            if (input == null || !input.PausePressed) return;

            // While a rebind is armed the settings screen owns Escape - it uses it
            // to cancel the capture, and closing the whole menu instead would be a
            // surprise.
            if (_settings != null && _settings.IsCapturingKey) return;

            if (_settings != null && _settings.IsOpen)
            {
                CloseSettings();
                return;
            }

            Toggle();
        }

        public void Toggle()
        {
            if (_open) Close();
            else Open();
        }

        public void Open()
        {
            if (_open) return;
            _open = true;

            _root.gameObject.SetActive(true);
            TimeController.Instance?.SetPaused(true);

            // The cursor has to come back or none of these buttons can be clicked.
            GameBootstrap.SetCursorCaptured(false);
            InputService.Current?.Flush();
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;

            CloseSettings();
            _root.gameObject.SetActive(false);
            TimeController.Instance?.SetPaused(false);

            GameBootstrap.SetCursorCaptured(true);

            // Settings are written when the menu closes, so a player who alt-F4s
            // from the desktop still keeps what they just changed.
            GameSettings.Flush();
        }

        private void OpenSettings()
        {
            _buttons.gameObject.SetActive(false);
            _settings.Show();
        }

        private void CloseSettings()
        {
            if (_settings == null || !_settings.IsOpen) return;
            _settings.Hide();
            _buttons.gameObject.SetActive(true);
        }
    }
}
