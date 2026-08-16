using System;
using Glowpulse.Core.Settings;
using Glowpulse.Stages;
using UnityEngine;
using UnityEngine.UI;

namespace Glowpulse.UI
{
    /// <summary>
    /// The screen after a stage is won or lost.
    ///
    /// One screen for both outcomes, because they need the same three things -
    /// what happened, what it earned, and where to go next - and two screens that
    /// differ only in a heading is two screens to keep in step.
    /// </summary>
    public sealed class StageCompleteScreen : MonoBehaviour
    {
        private RectTransform _root;
        private Text _heading;
        private Text _stageName;
        private Text _rewards;
        private Text _note;
        private Button _primary;
        private Text _primaryLabel;

        private Action _onNext;
        private Action _onSelect;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        public static StageCompleteScreen Create(Transform parent, Action onNext, Action onSelect)
        {
            var go = new GameObject("Stage Complete");
            go.transform.SetParent(parent, false);
            var screen = go.AddComponent<StageCompleteScreen>();
            screen._onNext = onNext;
            screen._onSelect = onSelect;
            screen.Build();
            screen.Hide();
            return screen;
        }

        private void Build()
        {
            _root = UiKit.Stretch("Root", transform);
            UiKit.Panel("Veil", _root, UiKit.Veil);

            RectTransform panel = UiKit.Rect("Panel", _root);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(620f, 470f);

            var background = panel.gameObject.AddComponent<Image>();
            background.color = UiKit.PanelFill;

            RectTransform content = UiKit.Stretch("Content", panel, 34f);
            UiKit.Column(content, 10f, TextAnchor.UpperCenter);

            _heading = UiKit.Label("Heading", content, "STAGE COMPLETE", 40, UiKit.Sun,
                TextAnchor.MiddleCenter);
            _heading.fontStyle = FontStyle.Bold;
            UiKit.Size(_heading.gameObject, height: 52f);

            _stageName = UiKit.Label("Stage", content, string.Empty, 26, UiKit.Bone,
                TextAnchor.MiddleCenter);
            UiKit.Size(_stageName.gameObject, height: 34f);

            UiKit.Divider(content);

            _rewards = UiKit.Label("Rewards", content, string.Empty, UiKit.BodySize, UiKit.Cyan,
                TextAnchor.MiddleCenter);
            UiKit.Size(_rewards.gameObject, height: 72f);

            _note = UiKit.Label("Note", content, string.Empty, UiKit.SmallSize, UiKit.Muted,
                TextAnchor.MiddleCenter);
            UiKit.Size(_note.gameObject, height: 30f);

            UiKit.Divider(content);

            _primary = UiKit.Button("NEXT STAGE", content, () => { Hide(); _onNext?.Invoke(); },
                accent: true);
            _primaryLabel = _primary.GetComponentInChildren<Text>();

            UiKit.Button("STAGE SELECT", content, () => { Hide(); _onSelect?.Invoke(); });
        }

        /// <summary>Shown on a win, with what the stage paid out.</summary>
        public void ShowVictory(StageDefinition stage)
        {
            _heading.text = "STAGE COMPLETE";
            _heading.color = UiKit.Sun;
            _stageName.text = stage != null ? stage.Name : string.Empty;

            _rewards.text = stage == null
                ? string.Empty
                : $"+{stage.ExperienceReward} XP\n+${stage.MoneyReward}";

            bool hasNext = stage != null && stage.Index + 1 < StageCatalogue.Count;

            _note.text = hasNext
                ? $"{StageCatalogue.Get(stage.Index + 1).DisplayNumber} unlocked."
                : "That was the last of them. The city is yours.";

            // With nothing left to unlock, offering NEXT STAGE would be a button
            // that cannot do anything.
            _primaryLabel.text = hasNext ? "NEXT STAGE" : "FIGHT IT AGAIN";

            Show();
        }

        /// <summary>Shown on a loss. The stage stays available; nothing is taken away.</summary>
        public void ShowDefeat(StageDefinition stage)
        {
            _heading.text = "YOU WENT DOWN";
            _heading.color = UiKit.Danger;
            _stageName.text = stage != null ? stage.Name : string.Empty;

            _rewards.text = $"XP {GameSettings.Data.Xp}\n${GameSettings.Data.Money}";
            _note.text = "Everything you earned on the way is kept.";
            _primaryLabel.text = "TRY AGAIN";

            Show();
        }

        public void Show()
        {
            _root.gameObject.SetActive(true);
            Core.Bootstrap.GameBootstrap.SetCursorCaptured(false);
        }

        public void Hide()
        {
            _root.gameObject.SetActive(false);
        }
    }
}
