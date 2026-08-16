using UnityEngine;

namespace Glowpulse.CameraSystem
{
    /// <summary>Tuning for the third-person camera. Lives in an asset so it can be balanced live.</summary>
    [CreateAssetMenu(menuName = "Glowpulse/Camera Config", fileName = "CameraConfig")]
    public sealed class CameraConfig : ScriptableObject
    {
        [Header("Framing")]
        [Tooltip("Height above the character's feet that the camera orbits around.")]
        public float PivotHeight = 1.5f;

        [Tooltip("Sideways offset of the pivot - a small over-the-shoulder bias.")]
        public float ShoulderOffset = 0.45f;

        public float Distance = 4.6f;

        [Tooltip("Distance while in combat. Pulling in tightens the read on the fight.")]
        public float CombatDistance = 3.9f;

        [Tooltip("Extra distance added at full sprint, which sells the speed.")]
        public float SprintDistanceBoost = 1.1f;

        public float MinDistance = 1.1f;

        [Header("Rotation")]
        public float MinPitch = -38f;
        public float MaxPitch = 68f;
        public float DefaultPitch = 12f;

        [Tooltip("How sharply the camera settles onto its target rotation.")]
        public float RotationSharpness = 22f;

        [Header("Follow")]
        [Tooltip("How sharply the pivot chases the character. Higher is tighter.")]
        public float FollowSharpness = 16f;

        [Tooltip("Vertical follow is softened separately so stairs and jumps do not jolt the frame.")]
        public float VerticalFollowSharpness = 9f;

        [Header("Collision")]
        [Tooltip("Radius of the sphere swept between pivot and camera.")]
        public float CollisionRadius = 0.28f;

        [Tooltip("Gap kept between the camera and whatever it hit.")]
        public float CollisionPadding = 0.14f;

        [Tooltip("How fast the camera snaps inward when something blocks it. Must be fast to avoid clipping.")]
        public float CollisionPullInSharpness = 40f;

        [Tooltip("How slowly it returns once the obstruction is gone. Slow feels calm.")]
        public float CollisionPushOutSharpness = 6f;

        [Header("Field of view")]
        public float BaseFov = 58f;

        [Tooltip("Degrees added at full sprint.")]
        public float SprintFovBoost = 9f;

        public float FovSharpness = 6f;

        [Header("Lock-on")]
        [Tooltip("Where the camera looks between the player and the target. 0 is the player.")]
        [Range(0f, 1f)] public float LockFramingBias = 0.34f;

        [Tooltip("Pitch the camera eases to while locked on.")]
        public float LockPitch = 16f;

        [Tooltip("Extra distance per metre of separation from the target, so both stay in frame.")]
        public float LockDistancePerMetre = 0.16f;

        public float LockMaxExtraDistance = 2.4f;

        [Header("Input")]
        public float YawSensitivity = 1.0f;
        public float PitchSensitivity = 1.0f;

        private static CameraConfig _default;

        public static CameraConfig Default
        {
            get
            {
                if (_default == null)
                {
                    _default = CreateInstance<CameraConfig>();
                    _default.name = "CameraConfig (Default)";
                }

                return _default;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _default = null;
    }
}
