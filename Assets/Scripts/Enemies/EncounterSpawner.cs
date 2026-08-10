using System;
using System.Collections.Generic;
using Glowpulse.Core;
using UnityEngine;

namespace Glowpulse.Enemies
{
    /// <summary>One entry in an encounter: how many of which enemy.</summary>
    [Serializable]
    public struct EnemyGroup
    {
        public EnemyKind Kind;
        public int Count;

        public EnemyGroup(EnemyKind kind, int count)
        {
            Kind = kind;
            Count = count;
        }
    }

    /// <summary>
    /// Spawns and tracks a group of enemies at a location.
    ///
    /// Missions and the world both need "a fight happens here", and both need to
    /// know when it is over, so the spawner owns the roster and raises an event
    /// when the last enemy falls.
    /// </summary>
    public sealed class EncounterSpawner : MonoBehaviour
    {
        [Header("Roster")]
        [SerializeField] private List<EnemyGroup> _groups = new List<EnemyGroup>();

        [Header("Placement")]
        [Tooltip("Radius enemies are scattered within when the encounter starts.")]
        [SerializeField] private float _spawnRadius = 5f;

        [Tooltip("Distance the player must come within before the encounter triggers. Zero spawns immediately.")]
        [SerializeField] private float _triggerRadius;

        [SerializeField] private bool _spawnOnStart = true;

        private readonly List<EnemyBrain> _spawned = new List<EnemyBrain>(8);
        private bool _triggered;
        private int _aliveCount;

        /// <summary>Raised once every enemy in the encounter is dead.</summary>
        public event Action<EncounterSpawner> Cleared;

        /// <summary>Raised as each enemy dies, with the rewards it carried.</summary>
        public event Action<EnemyCombatant, int, int> EnemyKilled;

        /// <summary>Raised when the encounter first spawns.</summary>
        public event Action<EncounterSpawner> Started;

        public IReadOnlyList<EnemyBrain> Enemies => _spawned;
        public int AliveCount => _aliveCount;
        public bool IsCleared => _triggered && _aliveCount == 0;
        public bool HasStarted => _triggered;

        public void SetRoster(params EnemyGroup[] groups)
        {
            _groups.Clear();
            if (groups != null) _groups.AddRange(groups);
        }

        private void Start()
        {
            if (_spawnOnStart && _triggerRadius <= 0f) Trigger();
        }

        private void Update()
        {
            if (_triggered || _triggerRadius <= 0f) return;

            ITargetable player = TargetRegistry.Nearest(transform.position, _triggerRadius, Faction.Hostile);
            if (player != null) Trigger();
        }

        /// <summary>Spawns the encounter now, if it has not already run.</summary>
        public void Trigger()
        {
            if (_triggered) return;
            _triggered = true;

            var root = new GameObject($"{name} Enemies");
            root.transform.SetParent(transform, false);

            int index = 0;
            int total = TotalCount();

            foreach (EnemyGroup group in _groups)
            {
                for (int i = 0; i < group.Count; i++)
                {
                    Vector3 position = ResolveSpawnPoint(index, total);
                    Vector3 facing = MathUtil.FlatDirection(transform.position - position);
                    if (facing.sqrMagnitude < 0.0001f) facing = Vector3.forward;

                    EnemyBrain brain = EnemyFactory.Create(group.Kind, position,
                        Quaternion.LookRotation(facing, Vector3.up), root.transform);

                    Register(brain);
                    index++;
                }
            }

            _aliveCount = _spawned.Count;
            Started?.Invoke(this);

            if (_aliveCount == 0) Cleared?.Invoke(this);
        }

        private void Register(EnemyBrain brain)
        {
            _spawned.Add(brain);

            EnemyCombatant combatant = brain.Combatant;
            if (combatant == null) return;

            combatant.DiedWithRewards += HandleEnemyDied;
        }

        private void HandleEnemyDied(EnemyCombatant combatant, int xp, int money)
        {
            combatant.DiedWithRewards -= HandleEnemyDied;

            EnemyKilled?.Invoke(combatant, xp, money);

            _aliveCount = Mathf.Max(0, _aliveCount - 1);
            if (_aliveCount == 0) Cleared?.Invoke(this);
        }

        private int TotalCount()
        {
            int total = 0;
            foreach (EnemyGroup group in _groups) total += Mathf.Max(0, group.Count);
            return Mathf.Max(1, total);
        }

        /// <summary>Spreads spawns evenly around the marker and drops them onto the ground.</summary>
        private Vector3 ResolveSpawnPoint(int index, int total)
        {
            float angle = (index / (float)total) * Mathf.PI * 2f + Mathf.PI * 0.25f;
            float radius = Mathf.Lerp(_spawnRadius * 0.45f, _spawnRadius, (index % 3) / 2f);

            Vector3 candidate = transform.position +
                                new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

            return MathUtil.GroundPoint(candidate, out Vector3 grounded, 5f, 25f, GameLayers.WorldMask)
                ? grounded
                : candidate;
        }

        /// <summary>Removes every spawned enemy. Used when a mission is abandoned.</summary>
        public void Despawn()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] == null) continue;
                Destroy(_spawned[i].gameObject);
            }

            _spawned.Clear();
            _aliveCount = 0;
            _triggered = false;
        }

        /// <summary>Convenience for building an encounter from code.</summary>
        public static EncounterSpawner Create(string name, Vector3 position, Transform parent,
            float triggerRadius, params EnemyGroup[] roster)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var spawner = go.AddComponent<EncounterSpawner>();
            spawner._triggerRadius = triggerRadius;
            spawner._spawnOnStart = true;
            spawner.SetRoster(roster);
            return spawner;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.25f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, _spawnRadius);

            if (_triggerRadius <= 0f) return;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, _triggerRadius);
        }
    }
}
