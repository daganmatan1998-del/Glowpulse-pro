using Glowpulse.Core.Bootstrap;
using Glowpulse.Core.Settings;
using Glowpulse.Core.Timing;
using Glowpulse.UI;
using Glowpulse.World;
using UnityEngine;

namespace Glowpulse.Stages
{
    /// <summary>
    /// Owns which stage the player is in and everything that changes when that
    /// answer changes: the arena, the lighting, the crowd, the HUD, the title
    /// card and the screens either side of the fight.
    ///
    /// One place decides all of it, so entering a stage cannot half-happen - the
    /// commonest way a progression system breaks is the arena changing while the
    /// HUD still names the last one.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public sealed class StageDirector : MonoBehaviour
    {
        private StageRunner _runner;
        private StageHud _hud;
        private StageTitleCard _card;
        private StageCompleteScreen _complete;
        private StageSelectScreen _select;

        private Transform _arenaRoot;
        private Transform _arenaCentre;
        private GameBootstrap _boot;

        public StageDefinition Current { get; private set; }

        public static StageDirector Install(GameBootstrap boot, UiRoot ui)
        {
            var go = new GameObject("Stage Director");
            go.transform.SetParent(boot.transform, false);

            var director = go.AddComponent<StageDirector>();
            director._boot = boot;
            director._runner = go.AddComponent<StageRunner>();

            director._hud = StageHud.Create(ui.HudLayer);
            director._card = StageTitleCard.Create(ui.OverlayLayer);

            director._complete = StageCompleteScreen.Create(ui.MenuLayer,
                director.AdvanceToNext, director.OpenSelect);

            director._select = StageSelectScreen.Create(ui.MenuLayer,
                director.Enter, director.CloseSelect);

            director._runner.Completed += director.HandleCompleted;
            director._runner.Failed += director.HandleFailed;

            return director;
        }

        private void OnDestroy()
        {
            if (_runner == null) return;
            _runner.Completed -= HandleCompleted;
            _runner.Failed -= HandleFailed;
        }

        /// <summary>
        /// Starts wherever the save left off. A player who closed the game halfway
        /// through the docks comes back to the docks, not to stage one.
        /// </summary>
        public void Resume()
        {
            int index = Mathf.Clamp(GameSettings.Data.CurrentStage, 0,
                Mathf.Max(0, StageCatalogue.Count - 1));

            Enter(index);
        }

        public void Enter(int index)
        {
            StageDefinition stage = StageCatalogue.Get(index);
            if (stage == null) return;

            // A locked stage can only be reached through a bug or a hand-edited
            // save; refusing here is cheaper than trusting every caller.
            if (!GameSettings.Data.IsStageUnlocked(index))
            {
                Debug.LogWarning($"[Stage] Refused to enter locked stage {stage.Number}.");
                return;
            }

            Current = stage;
            GameSettings.SetCurrentStage(index);
            GameSettings.Flush();

            TimeController.Instance?.SetPaused(false);
            _complete.Hide();
            _select.Hide();

            BuildArena(stage);
            ApplyAtmosphere(stage);
            PlacePlayer();

            _runner.Begin(stage, _arenaCentre);
            _hud.Bind(_runner);
            _card.Play(stage);

            GameBootstrap.SetCursorCaptured(true);
        }

        private void BuildArena(StageDefinition stage)
        {
            // The previous arena goes before the next is built, so two stages'
            // geometry can never occupy the same ground.
            if (_arenaRoot != null) Destroy(_arenaRoot.gameObject);

            _arenaRoot = ArenaBuilder.Build(stage, _boot.WorldRoot, stage.Index * 7919 + 17);

            var centre = new GameObject("Arena Centre");
            centre.transform.SetParent(_arenaRoot, false);
            _arenaCentre = centre.transform;
        }

        private void ApplyAtmosphere(StageDefinition stage)
        {
            _boot.Lighting?.Apply(stage.TimeOfDay);

            // Civilians belong on a street, not in a fighting pit. Setting the
            // population to zero retires them without tearing the director down.
            if (_boot.Crowd != null)
            {
                _boot.Crowd.Population = stage.HasCivilians ? 26 : 0;
                if (!stage.HasCivilians) _boot.Crowd.Clear();
            }
        }

        private void PlacePlayer()
        {
            if (_boot.Player == null) return;

            // Just inside the arena edge, facing the middle, so the first thing
            // the player sees is the space they are about to fight in.
            Vector3 spawn = _arenaCentre.position
                            + Vector3.back * (Current.ArenaRadius * 0.72f)
                            + Vector3.up * 1.2f;

            // Respawn rather than a bare teleport: it also clears buffered input,
            // combat state and the animation pose, so the player never arrives in
            // a new arena mid-combo from the last one.
            _boot.Player.Respawn(spawn, Quaternion.LookRotation(Vector3.forward, Vector3.up));
        }

        private void HandleCompleted(StageDefinition stage)
        {
            GameSettings.SaveNow();
            _complete.ShowVictory(stage);
        }

        private void HandleFailed(StageDefinition stage)
        {
            GameSettings.SaveNow();
            _complete.ShowDefeat(stage);
        }

        private void AdvanceToNext()
        {
            if (Current == null) return;

            int next = Current.Index + 1;

            // Past the last stage there is nothing to advance to, so the button
            // replays the finale instead of doing nothing.
            Enter(next < StageCatalogue.Count ? next : Current.Index);
        }

        public void OpenSelect()
        {
            _runner.Abort();
            _select.Show();
        }

        private void CloseSelect()
        {
            // Backing out of the select screen returns to the stage that was
            // already in progress rather than leaving the player on an empty map.
            if (Current != null) Enter(Current.Index);
        }
    }
}
