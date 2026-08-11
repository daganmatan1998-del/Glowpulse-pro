using System;
using System.Collections.Generic;
using Glowpulse.Combat;
using Glowpulse.Core;
using Glowpulse.Core.Settings;
using Glowpulse.Enemies;
using UnityEngine;

namespace Glowpulse.Stages
{
    /// <summary>
    /// Runs one stage: sends the waves in, counts what is left, and decides when
    /// the stage is won or lost.
    ///
    /// Waves arrive on a delay rather than all at once, which is what gives a
    /// stage a shape - an opening exchange, reinforcements while the player is
    /// already committed, and a boss that walks in last. The delay is measured in
    /// scaled time, so hit stop and slow motion do not quietly change the pacing
    /// of a fight they are supposed to be punctuating.
    /// </summary>
    [DefaultExecutionOrder(-15)]
    public sealed class StageRunner : MonoBehaviour
    {
        private readonly List<EnemyBrain> _alive = new List<EnemyBrain>(16);
        private readonly List<EnemyWave> _pending = new List<EnemyWave>(4);

        private StageDefinition _stage;
        private Transform _arenaCentre;
        private Transform _enemyRoot;
        private float _startedAt;
        private int _defeated;

        private bool _running;
        private bool _finished;

        /// <summary>Raised when the stage is cleared, with the stage that was cleared.</summary>
        public event Action<StageDefinition> Completed;

        /// <summary>Raised when the player dies, so the game-over screen can appear.</summary>
        public event Action<StageDefinition> Failed;

        /// <summary>Raised whenever the count changes, for the HUD.</summary>
        public event Action Progressed;

        /// <summary>Raised when a boss enters the arena, so the UI can announce it.</summary>
        public event Action<EnemyBrain> BossArrived;

        public StageDefinition Stage => _stage;
        public bool IsRunning => _running && !_finished;
        public bool IsFinished => _finished;

        public int Defeated => _defeated;
        public int Total => _stage != null ? _stage.TotalEnemies : 0;
        public int Remaining => Mathf.Max(0, Total - _defeated);

        /// <summary>The boss of this stage once it has spawned, or null.</summary>
        public EnemyBrain Boss { get; private set; }

        private void OnEnable()
        {
            // The stage cannot see the player go down by itself, so it listens
            // for it. Subscribing here rather than in Begin means a player who
            // dies between stages is still handled.
            Player.PlayerCombatant player = FindPlayerCombatant();
            if (player != null) player.Died += HandlePlayerDied;
        }

        private void OnDisable()
        {
            Player.PlayerCombatant player = FindPlayerCombatant();
            if (player != null) player.Died -= HandlePlayerDied;
        }

        private static Player.PlayerCombatant FindPlayerCombatant()
        {
            Core.Bootstrap.GameBootstrap boot = Core.Bootstrap.GameBootstrap.Instance;
            if (boot == null || boot.Player == null) return null;
            return boot.Player.GetComponent<Player.PlayerCombatant>();
        }

        private void HandlePlayerDied(Combatant _) => ReportPlayerDown();

        public void Begin(StageDefinition stage, Transform arenaCentre)
        {
            _stage = stage;
            _arenaCentre = arenaCentre;
            _startedAt = Time.time;
            _defeated = 0;
            _finished = false;
            _running = true;
            Boss = null;

            _alive.Clear();
            _pending.Clear();
            if (stage != null) _pending.AddRange(stage.Waves);

            if (_enemyRoot != null) Destroy(_enemyRoot.gameObject);
            var root = new GameObject("Stage Enemies");
            root.transform.SetParent(transform, false);
            _enemyRoot = root.transform;

            Progressed?.Invoke();
        }

        /// <summary>Tears the stage down without completing it. Used when leaving early.</summary>
        public void Abort()
        {
            _running = false;
            _finished = true;
            _pending.Clear();
            _alive.Clear();

            if (_enemyRoot != null) Destroy(_enemyRoot.gameObject);
            _enemyRoot = null;
        }

        private void Update()
        {
            if (!IsRunning) return;

            ReleaseDueWaves();
            PruneDead();

            // Cleared only once nothing is left to send and nothing is left alive.
            if (_pending.Count == 0 && _alive.Count == 0) Finish();
        }

        private void ReleaseDueWaves()
        {
            float elapsed = Time.time - _startedAt;

            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].Delay > elapsed) continue;

                EnemyWave wave = _pending[i];
                _pending.RemoveAt(i);
                Spawn(wave);
            }
        }

        private void Spawn(in EnemyWave wave)
        {
            EnemyArchetype archetype = EnemyArchetype.Get(wave.Kind).Scaled(GameSettings.Profile);
            bool boss = EnemyArchetype.IsBoss(wave.Kind);

            Vector3 centre = _arenaCentre != null ? _arenaCentre.position : transform.position;
            float radius = _stage != null ? _stage.ArenaRadius : 14f;

            for (int i = 0; i < wave.Count; i++)
            {
                Vector3 position = SpawnPoint(centre, radius, i, wave.Count, boss);
                Vector3 facing = MathUtil.FlatDirection(centre - position);
                if (facing.sqrMagnitude < 0.0001f) facing = Vector3.forward;

                EnemyBrain brain = EnemyFactory.Create(archetype, position,
                    Quaternion.LookRotation(facing, Vector3.up), _enemyRoot);

                if (boss) SetUpBoss(brain, archetype);

                Track(brain);
            }
        }

        /// <summary>
        /// Bosses get their phase controller and are announced. The archetype
        /// handed over is the difficulty-scaled one, so phase two of a Hard boss
        /// is a multiple of the Hard numbers rather than of the shipped ones.
        /// </summary>
        private void SetUpBoss(EnemyBrain brain, EnemyArchetype archetype)
        {
            var phases = brain.gameObject.AddComponent<BossBrain>();
            phases.Bind(archetype);

            Boss = brain;
            BossArrived?.Invoke(brain);

            CombatFeedback.RaiseCommotion(brain.transform.position, 1f);
        }

        /// <summary>
        /// Enemies arrive from the edge of the arena, spread around the ring so a
        /// wave never materialises as a clump. A boss walks in alone, opposite the
        /// player, because it deserves to be seen coming.
        /// </summary>
        private Vector3 SpawnPoint(Vector3 centre, float radius, int index, int count, bool boss)
        {
            float ring = boss ? radius * 0.92f : radius * Mathf.Lerp(0.7f, 0.95f, index / Mathf.Max(1f, count));
            float angle;

            if (boss)
            {
                // Directly across the arena from wherever the player is standing.
                Transform player = FindPlayer();
                Vector3 away = player != null
                    ? MathUtil.FlatDirection(centre - player.position)
                    : Vector3.forward;

                if (away.sqrMagnitude < 0.0001f) away = Vector3.forward;
                angle = Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg;
            }
            else
            {
                angle = 360f * index / Mathf.Max(1, count) + UnityEngine.Random.Range(-22f, 22f);
            }

            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * ring;
            Vector3 point = centre + offset;

            // Drop onto whatever the arena floor actually is, so a rooftop or a
            // pit does not spawn enemies in mid-air or under the deck.
            if (MathUtil.GroundPoint(point + Vector3.up * 6f, out Vector3 grounded, 10f, 40f,
                    GameLayers.WorldMask))
                point = grounded;

            return point;
        }

        private static Transform FindPlayer()
        {
            Core.Bootstrap.GameBootstrap boot = Core.Bootstrap.GameBootstrap.Instance;
            return boot != null && boot.Player != null ? boot.Player.transform : null;
        }

        private void Track(EnemyBrain brain)
        {
            if (brain == null) return;
            _alive.Add(brain);

            EnemyCombatant combatant = brain.Combatant;
            if (combatant != null) combatant.DiedWithRewards += HandleKilled;

            Progressed?.Invoke();
        }

        private void HandleKilled(EnemyCombatant combatant, int xp, int money)
        {
            combatant.DiedWithRewards -= HandleKilled;

            _defeated++;

            // Rewards land as they are earned rather than at the end, so a player
            // who dies on the last enemy still keeps what they fought for.
            GameSettings.Data.Xp += xp;
            GameSettings.Data.Money += money;
            GameSettings.MarkDirty();

            Progressed?.Invoke();
        }

        /// <summary>
        /// Drops dead or destroyed enemies from the live list. Done by sweeping
        /// rather than purely by the death event, because an enemy destroyed by
        /// anything other than damage would otherwise keep the stage open forever.
        /// </summary>
        private void PruneDead()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                EnemyBrain brain = _alive[i];
                if (brain != null && brain.IsAlive) continue;
                _alive.RemoveAt(i);
            }
        }

        private void Finish()
        {
            _finished = true;
            _running = false;

            if (_stage == null) return;

            GameSettings.Data.Xp += _stage.ExperienceReward;
            GameSettings.Data.Money += _stage.MoneyReward;
            GameSettings.CompleteStage(_stage.Index);

            Completed?.Invoke(_stage);
        }

        /// <summary>Called by the player's death, since the stage cannot detect it itself.</summary>
        public void ReportPlayerDown()
        {
            if (_finished) return;
            _finished = true;
            _running = false;
            Failed?.Invoke(_stage);
        }
    }
}
