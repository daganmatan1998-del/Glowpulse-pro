using UnityEngine;

namespace Glowpulse.Player
{
    /// <summary>
    /// All movement tuning in one asset so it can be balanced without touching
    /// code. The defaults here are the shipped tuning - creating an asset is
    /// optional, and the controller falls back to a default instance.
    /// </summary>
    [CreateAssetMenu(menuName = "Glowpulse/Player Locomotion Config", fileName = "PlayerLocomotionConfig")]
    public sealed class PlayerLocomotionConfig : ScriptableObject
    {
        [Header("Speeds (m/s)")]
        public float WalkSpeed = 2.1f;
        public float JogSpeed = 4.6f;
        public float SprintSpeed = 7.4f;

        [Tooltip("Speed cap while locked on and strafing sideways or backwards.")]
        public float StrafeSpeed = 3.4f;

        [Header("Acceleration")]
        [Tooltip("How sharply the character reaches its target speed. Higher is snappier.")]
        public float Acceleration = 14f;

        public float Deceleration = 18f;

        [Tooltip("Acceleration multiplier while airborne - low values give committed jumps.")]
        [Range(0f, 1f)] public float AirControl = 0.35f;

        [Header("Rotation")]
        [Tooltip("Degrees per second the body turns toward the movement direction.")]
        public float TurnSpeed = 900f;

        [Tooltip("Turn speed while locked onto a target.")]
        public float LockedTurnSpeed = 1400f;

        [Header("Gravity and jumping")]
        public float Gravity = -24f;

        [Tooltip("Extra gravity multiplier while falling. Makes jumps feel less floaty.")]
        public float FallMultiplier = 1.7f;

        [Tooltip("Extra gravity applied when the jump button is released early.")]
        public float LowJumpMultiplier = 2.4f;

        public float JumpHeight = 1.25f;

        [Tooltip("Seconds after leaving a ledge during which a jump still works.")]
        public float CoyoteTime = 0.14f;

        [Tooltip("Seconds a jump press is remembered before landing.")]
        public float JumpBuffer = 0.16f;

        [Header("Dodge")]
        public float DodgeDistance = 4.2f;
        public float DodgeDuration = 0.42f;

        [Tooltip("Seconds of invulnerability, starting shortly after the dodge begins.")]
        public float DodgeInvulnerability = 0.28f;

        [Tooltip("Delay before i-frames start, so a dodge cannot be used as an instant panic button.")]
        public float DodgeInvulnerabilityDelay = 0.06f;

        public float DodgeCooldown = 0.22f;
        public float DodgeStaminaCost = 22f;

        [Tooltip("Speed curve of the dodge: 1 at the start, tapering to 0.")]
        public AnimationCurve DodgeSpeedCurve = AnimationCurve.EaseInOut(0f, 1.35f, 1f, 0.05f);

        [Header("Stamina")]
        public float SprintStaminaPerSecond = 12f;

        [Tooltip("Minimum stamina needed to break into a sprint.")]
        public float SprintStaminaThreshold = 12f;

        public float JumpStaminaCost = 8f;

        [Header("Character body")]
        public float Height = 1.82f;
        public float Radius = 0.32f;
        public float StepOffset = 0.42f;
        public float SlopeLimit = 52f;

        [Header("Feel")]
        [Tooltip("Speed below which the character is considered stopped.")]
        public float StopThreshold = 0.12f;

        [Tooltip("Landing impact above this fall speed plays the heavy landing.")]
        public float HardLandingSpeed = 11f;

        private static PlayerLocomotionConfig _default;

        /// <summary>Shared fallback so the player works with no asset assigned.</summary>
        public static PlayerLocomotionConfig Default
        {
            get
            {
                if (_default == null)
                {
                    _default = CreateInstance<PlayerLocomotionConfig>();
                    _default.name = "PlayerLocomotionConfig (Default)";
                }

                return _default;
            }
        }

        /// <summary>Top speed the character can reach on the ground, for animation blending.</summary>
        public float MaxGroundSpeed => Mathf.Max(SprintSpeed, JogSpeed);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _default = null;
    }
}
