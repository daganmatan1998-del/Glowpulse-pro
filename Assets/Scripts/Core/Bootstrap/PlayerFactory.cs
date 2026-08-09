using Glowpulse.CameraSystem;
using Glowpulse.Core.Characters;
using Glowpulse.Player;
using UnityEngine;

namespace Glowpulse.Core.Bootstrap
{
    /// <summary>Assembles the player from the shared character shell plus its own controllers.</summary>
    public static class PlayerFactory
    {
        public static PlayerController Create(Vector3 position, Quaternion rotation,
            PlayerLocomotionConfig config = null)
        {
            PlayerLocomotionConfig cfg = config != null ? config : PlayerLocomotionConfig.Default;

            CharacterStyle style = CharacterStyle.Player();
            style.Height = cfg.Height;

            CharacterFactory.Spec spec = CharacterFactory.Spec.For("Player", style,
                GameLayers.Player, GameTags.Player);
            spec.Radius = cfg.Radius;
            spec.StepOffset = cfg.StepOffset;
            spec.SlopeLimit = cfg.SlopeLimit;
            spec.Gravity = cfg.Gravity;
            spec.FallMultiplier = cfg.FallMultiplier;

            CharacterFactory.Build build = CharacterFactory.Begin(spec, position, rotation);
            GameObject go = build.GameObject;

            var combatant = go.AddComponent<PlayerCombatant>();
            var player = go.AddComponent<PlayerController>();
            var combat = go.AddComponent<PlayerCombatController>();
            var lockOn = go.AddComponent<LockOnSystem>();

            CharacterFactory.Finish(build, combatant);

            player.BindAnimator(build.Animator);
            combat.BindAnimator(build.Animator);

            // The camera does not exist yet; the bootstrap re-binds once it does.
            lockOn.Bind(player, null);

            return player;
        }

        /// <summary>Kept for callers that only need the tag helper.</summary>
        public static void TrySetTag(GameObject go, string tag) => CharacterFactory.TrySetTag(go, tag);
    }
}
