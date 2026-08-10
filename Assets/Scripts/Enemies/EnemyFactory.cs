using Glowpulse.Core;
using Glowpulse.Core.Bootstrap;
using Glowpulse.Core.Characters;
using UnityEngine;

namespace Glowpulse.Enemies
{
    /// <summary>Builds enemies from an archetype using the shared character shell.</summary>
    public static class EnemyFactory
    {
        public static EnemyBrain Create(EnemyKind kind, Vector3 position, Quaternion rotation,
            Transform parent = null)
        {
            return Create(EnemyArchetype.Get(kind), position, rotation, parent);
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
