using Glowpulse.Core;
using Glowpulse.Core.Bootstrap;
using Glowpulse.Core.Characters;
using UnityEngine;

namespace Glowpulse.World.Npc
{
    /// <summary>
    /// Builds pedestrians out of the same character shell as the player and the
    /// enemies, with a seeded palette so a crowd looks like a crowd rather than a
    /// row of clones.
    /// </summary>
    public static class CivilianFactory
    {
        public static CivilianBrain Create(int seed, Vector3 position, Quaternion rotation,
            Transform parent = null)
        {
            CharacterStyle style = CharacterStyle.Civilian(seed);

            CharacterFactory.Spec spec = CharacterFactory.Spec.For($"Civilian {seed:X4}", style,
                GameLayers.Npc, GameTags.Npc);

            // Bystanders never cast the shadows that matter, and there are a lot
            // more of them than there are fighters.
            spec.CastShadows = false;

            CharacterFactory.Build build = CharacterFactory.Begin(spec, position, rotation);
            if (parent != null) build.GameObject.transform.SetParent(parent, true);

            var combatant = build.GameObject.AddComponent<CivilianCombatant>();
            var brain = build.GameObject.AddComponent<CivilianBrain>();

            combatant.DisplayName = "Civilian";
            combatant.Faction = Faction.Civilian;

            CharacterFactory.Finish(build, combatant);

            // The whole hierarchy has to be on the NPC layer, not just the root,
            // or the limb colliders stay on Default and start blocking combat
            // hit queries meant for fighters.
            GameLayers.SetLayerRecursive(build.GameObject, GameLayers.Npc);

            // The brain stays dormant until it is given a network to walk, which
            // the crowd director does when it places them. A civilian with no
            // network simply stands still rather than misbehaving.
            return brain;
        }
    }
}
