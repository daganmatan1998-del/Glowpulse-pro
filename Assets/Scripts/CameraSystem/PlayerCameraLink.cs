using Glowpulse.Core;
using Glowpulse.Player;
using UnityEngine;

namespace Glowpulse.CameraSystem
{
    /// <summary>
    /// Feeds the camera what it needs to know about the player each frame -
    /// speed, and whether a fight is happening - without the camera holding a
    /// reference to gameplay code or the player knowing about framing.
    /// </summary>
    [DefaultExecutionOrder(400)]
    public sealed class PlayerCameraLink : MonoBehaviour
    {
        [SerializeField] private PlayerController _player;
        [SerializeField] private ThirdPersonCamera _camera;

        [Tooltip("Radius searched for hostiles when deciding to tighten the framing.")]
        [SerializeField] private float _combatRadius = 14f;

        [Tooltip("Seconds the combat framing lingers after the last enemy leaves.")]
        [SerializeField] private float _combatHoldTime = 3.5f;

        [SerializeField] private float _combatBlendSharpness = 2.5f;

        private float _lastHostileSeenAt = float.NegativeInfinity;
        private float _scanTimer;

        public void Bind(PlayerController player, ThirdPersonCamera camera)
        {
            _player = player;
            _camera = camera;
        }

        private void LateUpdate()
        {
            if (_player == null || _camera == null) return;

            float dt = Time.deltaTime;

            _camera.SetSpeedNormalized(_player.SpeedNormalized);

            // Scanning a few times a second is plenty for a framing decision.
            _scanTimer -= dt;
            if (_scanTimer <= 0f)
            {
                _scanTimer = 0.25f;
                if (TargetRegistry.CountHostiles(_player.transform.position, _combatRadius, Faction.Player) > 0)
                    _lastHostileSeenAt = Time.time;
            }

            bool inCombat = _player.HasLockTarget || Time.time - _lastHostileSeenAt < _combatHoldTime;
            _camera.CombatWeight = MathUtil.Damp(_camera.CombatWeight, inCombat ? 1f : 0f,
                _combatBlendSharpness, dt);
        }
    }
}
