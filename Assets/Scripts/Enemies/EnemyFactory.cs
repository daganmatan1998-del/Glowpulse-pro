using Glowpulse.Core;
using Glowpulse.Core.Bootstrap;
using Glowpulse.Core.Characters;
using Glowpulse.Core.Settings;
using UnityEngine;

namespace Glowpulse.Enemies
{
    /// <summary>Builds enemies from an archetype using the shared character shell.</summary>
    public static class EnemyFactory
    {
        /// <summary>
        /// Builds an enemy of a kind, scaled to the player's chosen difficulty.
        /// Going through here rather than through <see cref="EnemyArchetype.Get"/>
        /// directly is what guarantees a new spawn site cannot forget difficulty.
        /// </summary>
        public static EnemyBrain Create(EnemyKind kind, Vector3 position, Quaternion rotation,
            Transform parent = null)
        {
            return Create(EnemyArchetype.Get(kind).Scaled(GameSettings.Profile), position, rotation, parent);
        }

        public static EnemyBrain Create(EnemyArchetype archetype, Vector3 position, Quaternion rotation,
            Transform parent = null)
        {
            CharacterFactory.Spec spec = CharacterFactory.Spec.For(archetype.DisplayName,
                archetype.Style, GameLayers.Enemy, GameTags.Enemy);

            CharacterFactory.Build build = CharacterFactory.Begin(spec, position, rotation);
            if (parent != null) build.GameObject.transform.SetParent(parent, true);

            var combatant = build.GameObject.AddComponent<EnemyCombatant>();
            var brain = build.GameObject.AddComponent<EnemyBrain>();

            CharacterFactory.Finish(build, combatant);

            brain.Configure(archetype, build.Animator);
            build.Animator.SetStance(CharacterStance.Relaxed);

            return brain;
        }
    }
}
