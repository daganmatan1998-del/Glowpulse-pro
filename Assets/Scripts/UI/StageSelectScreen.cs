using System;
using System.Collections.Generic;
using Glowpulse.Core.Settings;
using Glowpulse.SaveSystem;
using Glowpulse.Stages;
using UnityEngine;
using UnityEngine.UI;

namespace Glowpulse.UI
{
    /// <summary>
    /// Where the player picks a stage and reads their own progress.
    ///
    /// The four states - completed, current, unlocked and locked - are told apart
    /// by colour, by a marker and by whether the row responds at all, rather than
    /// by colour alone. A locked row is still listed and still named: hiding it
    /// would leave the player unable to see that there is anything ahead.
    /// </summary>
    public sealed class StageSelectScreen : MonoBehaviour
    {
        private sealed class Row
        {
            public Button Button;
            public Image Background;
            public Text Marker;
            public Text Title;
            public Text Detail;
        }

        private RectTransform _root;
        private readonly List<Row> _rows = new List<Row>(8);
        private Action<int> _onPlay;
        private Action _onClose;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        public static StageSelectScreen Create(Transform parent, Action<int> onPlay, Action onClose)
        {
            var go = new GameObject("Stage Select");
            go.transform.SetParent(parent, false);
            var screen = go.AddComponent<StageSelectScreen>();
            screen._onPlay = onPlay;
            screen._onClose = onClose;
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
            panel.sizeDelta = new Vector2(860f, 680f);

            var background = panel.gameObject.AddComponent<Image>();
            background.color = UiKit.PanelFill;

            RectTransform content = UiKit.Stretch("Content", panel, 32f);
            UiKit.Column(content, 8f);

            Text title = UiKit.Label("Title", content, "STAGE SELECT", UiKit.TitleSize, UiKit.Bone);
            title.fontStyle = FontStyle.Bold;
            UiKit.Size(title.gameObject, height: 58f);

            UiKit.Divider(content);

            for (int i = 0; i < StageCatalogue.Count; i++) BuildRow(content, i);

            UiKit.Divider(content);
            UiKit.Button("BACK", content, () => { Hide(); _onClose?.Invoke(); });
        }

        private void BuildRow(Transform parent, int index)
        {
            RectTransform rect = UiKit.Rect("Stage " + index, parent);
            UiKit.Size(rect.gameObject, height: 72f);

            var background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.05f);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            int captured = index;
            button.onClick.AddListener(() =>
            {
                if (!GameSettings.Data.IsStageUnlocked(captured)) return;
                Hide();
                _onPlay?.Invoke(captured);
            });

            RectTransform inner = UiKit.Stretch("Inner", rect, 12f);
            UiKit.Row(inner, 14f);

            Text marker = UiKit.Label("Marker", inner, string.Empty, 26, UiKit.Bone);
            marker.alignment = TextAnchor.MiddleCenter;
            UiKit.Size(marker.gameObject, width: 44f);

            RectTransform column = UiKit.Rect("Text", inner);
            UiKit.Column(column, 0f);
            UiKit.Size(column.gameObject, flexibleWidth: 1f);

            Text titleText = UiKit.Label("Title", column, string.Empty, 22, UiKit.Bone);
            titleText.fontStyle = FontStyle.Bold;
            UiKit.Size(titleText.gameObject, height: 28f);

            Text detail = UiKit.Label("Detail", column, string.Empty, UiKit.SmallSize, UiKit.Muted);
            UiKit.Size(detail.gameObject, height: 22f);

            _rows.Add(new Row
            {
                Button = button,
                Background = background,
                Marker = marker,
                Title = titleText,
                Detail = detail
            });
        }

        public void Show()
        {
            Refresh();
            _root.gameObject.SetActive(true);
            Core.Bootstrap.GameBootstrap.SetCursorCaptured(false);
        }

        public void Hide() => _root.gameObject.SetActive(false);

        public void Refresh()
        {
            SaveData save = GameSettings.Data;

            for (int i = 0; i < _rows.Count && i < StageCatalogue.Count; i++)
            {
                StageDefinition stage = StageCatalogue.Get(i);
                Row row = _rows[i];

                bool unlocked = save.IsStageUnlocked(i);
                bool completed = save.IsStageCompleted(i);

                // "Current" is the furthest stage reached that has not been beaten
                // - the one the player is actually up to.
                bool current = unlocked && !completed && i == save.HighestUnlockedStage;

                row.Title.text = $"{stage.DisplayNumber}  -  {stage.Name}";

                if (!unlocked)
                {
                    row.Marker.text = "X";
                    row.Marker.color = UiKit.Muted;
                    row.Title.color = UiKit.Muted;
                    row.Detail.text = "Locked - clear the stage before it.";
                    row.Detail.color = UiKit.Muted;
                    row.Background.color = new Color(1f, 1f, 1f, 0.02f);

                    // Not merely dimmed: a locked row must not respond to a click,
                    // or the player will think the game is broken.
                    row.Button.interactable = false;
                    continue;
                }

                row.Button.interactable = true;

                if (completed)
                {
                    row.Marker.text = "✓";
                    row.Marker.color = UiKit.Cyan;
                    row.Title.color = UiKit.Bone;
                    row.Detail.text = $"Cleared  -  {stage.ObjectiveText.ToLowerInvariant()}";
                    row.Detail.color = UiKit.Cyan;
                    row.Background.color = new Color(1f, 1f, 1f, 0.05f);
                }
                else if (current)
                {
                    row.Marker.text = "▶";
                    row.Marker.color = UiKit.Sun;
                    row.Title.color = UiKit.Sun;
                    row.Detail.text = stage.Tagline;
                    row.Detail.color = UiKit.Bone;
                    row.Background.color = new Color(1f, 0.824f, 0.631f, 0.12f);
                }
                else
                {
                    row.Marker.text = "▶";
                    row.Marker.color = UiKit.Bone;
                    row.Title.color = UiKit.Bone;
                    row.Detail.text = stage.Tagline;
                    row.Detail.color = UiKit.Muted;
                    row.Background.color = new Color(1f, 1f, 1f, 0.05f);
                }
            }
        }
    }
}
