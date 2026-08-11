using Glowpulse.Stages;
using UnityEngine;
using UnityEngine.UI;

namespace Glowpulse.UI
{
    /// <summary>
    /// The always-on stage readout: which stage this is, what it wants, and how
    /// much of it is left.
    ///
    /// Pinned to the top-left rather than centred at the top, because the centre
    /// of the screen is where the fight is and a banner across it would be exactly
    /// the obstruction the brief asks to avoid. It refreshes on events rather than
    /// every frame - a counter that rebuilds its string sixty times a second to
    /// say the same thing is pure waste.
    /// </summary>
    public sealed class StageHud : MonoBehaviour
    {
        private Text _stageLine;
        private Text _nameLine;
        private Text _objectiveLine;
        private Text _countLine;
        private Image _bossBarFill;
        private RectTransform _bossPanel;
        private Text _bossName;

        private StageRunner _runner;
        private Combat.Health _bossHealth;

        public static StageHud Create(Transform parent)
        {
            var go = new GameObject("Stage HUD");
            go.transform.SetParent(parent, false);
            var hud = go.AddComponent<StageHud>();
            hud.Build();
            return hud;
        }

        private void Build()
        {
            RectTransform panel = UiKit.Rect("Panel", transform);
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(28f, -24f);
            panel.sizeDelta = new Vector2(400f, 140f);

            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.024f, 0.027f, 0.031f, 0.62f);
            background.raycastTarget = false;

            RectTransform content = UiKit.Stretch("Content", panel, 14f);
            UiKit.Column(content, 1f);

            _stageLine = UiKit.Label("Stage", content, "STAGE 1", UiKit.SmallSize, UiKit.Sun);
            _stageLine.fontStyle = FontStyle.Bold;
            UiKit.Size(_stageLine.gameObject, height: 20f);

            _nameLine = UiKit.Label("Name", content, string.Empty, 24, UiKit.Bone);
            _nameLine.fontStyle = FontStyle.Bold;
            UiKit.Size(_nameLine.gameObject, height: 30f);

            _objectiveLine = UiKit.Label("Objective", content, string.Empty, UiKit.SmallSize,
                UiKit.Muted);
            UiKit.Size(_objectiveLine.gameObject, height: 22f);

            _countLine = UiKit.Label("Count", content, string.Empty, UiKit.BodySize, UiKit.Cyan);
            _countLine.fontStyle = FontStyle.Bold;
            UiKit.Size(_countLine.gameObject, height: 26f);

            BuildBossBar();
        }

        /// <summary>
        /// A boss gets its own bar across the bottom. Nothing else in the game
        /// gets one, which is most of why a boss reads as an event rather than as
        /// a tougher enemy.
        /// </summary>
        private void BuildBossBar()
        {
            _bossPanel = UiKit.Rect("Boss", transform);
            _bossPanel.anchorMin = new Vector2(0.5f, 0f);
            _bossPanel.anchorMax = new Vector2(0.5f, 0f);
            _bossPanel.pivot = new Vector2(0.5f, 0f);
            _bossPanel.anchoredPosition = new Vector2(0f, 118f);
            _bossPanel.sizeDelta = new Vector2(720f, 54f);

            var background = _bossPanel.gameObject.AddComponent<Image>();
            background.color = new Color(0.024f, 0.027f, 0.031f, 0.72f);
            background.raycastTarget = false;

            _bossName = UiKit.Label("Name", _bossPanel, string.Empty, UiKit.BodySize, UiKit.Sun,
                TextAnchor.UpperCenter);
            _bossName.fontStyle = FontStyle.Bold;

            RectTransform track = UiKit.Rect("Track", _bossPanel);
            track.anchorMin = new Vector2(0f, 0f);
            track.anchorMax = new Vector2(1f, 0f);
            track.pivot = new Vector2(0.5f, 0f);
            track.offsetMin = new Vector2(14f, 10f);
            track.offsetMax = new Vector2(-14f, 10f);
            track.sizeDelta = new Vector2(track.sizeDelta.x, 12f);

            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(0f, 0f, 0f, 0.6f);
            trackImage.raycastTarget = false;

            RectTransform fill = UiKit.Stretch("Fill", track);
            _bossBarFill = fill.gameObject.AddComponent<Image>();
            _bossBarFill.color = UiKit.Danger;
            _bossBarFill.raycastTarget = false;
            _bossBarFill.type = Image.Type.Filled;
            _bossBarFill.fillMethod = Image.FillMethod.Horizontal;
            _bossBarFill.fillAmount = 1f;

            _bossPanel.gameObject.SetActive(false);
        }

        public void Bind(StageRunner runner)
        {
            if (_runner != null)
            {
                _runner.Progressed -= Refresh;
                _runner.BossArrived -= HandleBoss;
            }

            _runner = runner;
            HideBoss();

            if (_runner == null) return;

            _runner.Progressed += Refresh;
            _runner.BossArrived += HandleBoss;
            Refresh();
        }

        private void HandleBoss(Enemies.EnemyBrain boss)
        {
            if (boss == null) return;

            _bossHealth = boss.GetComponent<Combat.Health>();
            _bossName.text = boss.Combatant != null
                ? boss.Combatant.DisplayName.ToUpperInvariant()
                : "BOSS";

            _bossPanel.gameObject.SetActive(true);
        }

        private void HideBoss()
        {
            _bossHealth = null;
            if (_bossPanel != null) _bossPanel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_bossHealth == null) return;

            if (!_bossHealth.IsAlive)
            {
                HideBoss();
                return;
            }

            // The bar is the one thing here that does need a per-frame update, so
            // it drains smoothly as the fight goes on.
            _bossBarFill.fillAmount = Mathf.Lerp(_bossBarFill.fillAmount,
                _bossHealth.Normalized, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
        }

        private void Refresh()
        {
            if (_runner == null || _runner.Stage == null) return;

            StageDefinition stage = _runner.Stage;

            _stageLine.text = stage.DisplayNumber;
            _nameLine.text = stage.Name;
            _objectiveLine.text = "OBJECTIVE: " + stage.ObjectiveText;
            _countLine.text = $"ENEMIES  {_runner.Defeated} / {_runner.Total}";
        }

        private void OnDestroy()
        {
            if (_runner == null) return;
            _runner.Progressed -= Refresh;
            _runner.BossArrived -= HandleBoss;
        }
    }
}
