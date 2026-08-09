using Glowpulse.CameraSystem;
using Glowpulse.Combat;
using Glowpulse.Core.Characters;
using Glowpulse.Core.Movement;
using Glowpulse.Player;
using UnityEngine;

namespace Glowpulse.Core.Bootstrap
{
    /// <summary>
    /// Assembles the player from its parts. Building the object while it is
    /// inactive matters: Unity runs Awake the instant a component is added to a
    /// live GameObject, so components would otherwise wake up before their
    /// siblings exist and fail to find each other.
    /// </summary>
    public static class PlayerFactory
    {
        public static PlayerController Create(Vector3 position, Quaternion rotation,
            PlayerLocomotionConfig config = null)
        {
            var go = new GameObject("Player");
            go.SetActive(false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.layer = GameLayers.Player;
            TrySetTag(go, GameTags.Player);

            PlayerLocomotionConfig cfg = config != null ? config : PlayerLocomotionConfig.Default;

            var controller = go.AddComponent<CharacterController>();
            controller.height = cfg.Height;
            controller.radius = cfg.Radius;
            controller.center = new Vector3(0f, cfg.Height * 0.5f, 0f);
            controller.slopeLimit = cfg.SlopeLimit;
            controller.stepOffset = cfg.StepOffset;

            go.AddComponent<CharacterMotor>();
            go.AddComponent<Health>();
            go.AddComponent<Stamina>();

            CharacterStyle style = CharacterStyle.Player();
            style.Height = cfg.Height;
            CharacterRig rig = CharacterRigFactory.Build(go, style);

            var animator = go.AddComponent<ProceduralCharacterAnimator>();
            animator.Bind(rig);

            var combatant = go.AddComponent<PlayerCombatant>();
            var player = go.AddComponent<PlayerController>();
            var lockOn = go.AddComponent<LockOnSystem>();

            go.SetActive(true);

            combatant.BindVisuals(rig, animator);
            player.BindAnimator(animator);

            // The camera is wired in by the caller once it exists.
            lockOn.Bind(player, null);

            return player;
        }

        /// <summary>
        /// Sets a tag only if the project actually defines it, so a fresh clone
        /// with a regenerated TagManager logs a warning instead of throwing.
        /// </summary>
        public static void TrySetTag(GameObject go, string tag)
        {
            if (go == null || string.IsNullOrEmpty(tag)) return;
            try
            {
                go.tag = tag;
            }
            catch (UnityException)
            {
                Debug.LogWarning($"[PlayerFactory] Tag '{tag}' is not defined in this project. " +
                                 "Open Glowpulse > Validate Project Setup to add it.");
            }
        }
    }
}
