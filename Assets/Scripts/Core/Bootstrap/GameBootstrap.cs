using Glowpulse.CameraSystem;
using Glowpulse.Core.InputSystem;
using Glowpulse.Player;
using Glowpulse.World;
using UnityEngine;

namespace Glowpulse.Core.Bootstrap
{
    /// <summary>Which world the bootstrap should assemble.</summary>
    public enum WorldMode
    {
        /// <summary>Movement and camera sandbox used while building the systems.</summary>
        ProvingGround = 0,

        /// <summary>The open-world city.</summary>
        City = 1
    }

    /// <summary>
    /// The single entry point of the game. One of these in an otherwise empty
    /// scene builds the world, the player, the camera and every service.
    ///
    /// Building the scene from code rather than storing it as a .unity asset
    /// keeps the whole game in reviewable, diffable source, and means a scene can
    /// never break by losing a reference. The editor tooling can still bake the
    /// result into a real scene for hand editing.
    /// </summary>
    [DefaultExecutionOrder(-20000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("World")]
        [SerializeField] private WorldMode _worldMode = WorldMode.ProvingGround;
        [SerializeField] private TimeOfDayPreset _timeOfDay = TimeOfDayPreset.GoldenHour;

        [Header("Player")]
        [SerializeField] private Vector3 _spawnPosition = new Vector3(0f, 1.5f, -6f);
        [SerializeField] private float _spawnYaw;
        [SerializeField] private PlayerLocomotionConfig _locomotionConfig;

        [Header("Camera")]
        [SerializeField] private CameraConfig _cameraConfig;

        [Header("Options")]
        [Tooltip("Locks and hides the cursor on start. Turn off when profiling in the editor.")]
        [SerializeField] private bool _captureCursor = true;

        [SerializeField] private int _targetFrameRate = -1;

        private static GameBootstrap _instance;

        public static GameBootstrap Instance => _instance;

        public PlayerController Player { get; private set; }
        public ThirdPersonCamera GameCamera { get; private set; }
        public EnvironmentLighting Lighting { get; private set; }
        public Transform WorldRoot { get; private set; }

        /// <summary>Raised once everything exists, for systems that need the assembled scene.</summary>
        public event System.Action<GameBootstrap> Ready;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[GameBootstrap] A second bootstrap was found and disabled.", this);
                enabled = false;
                return;
            }

            _instance = this;

            ApplyQualityDefaults();
            GameLayers.ApplyCollisionMatrix();

            BuildServices();
            BuildWorld();
            BuildPlayerAndCamera();

            if (_captureCursor) SetCursorCaptured(true);

            Ready?.Invoke(this);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void ApplyQualityDefaults()
        {
            if (_targetFrameRate > 0)
            {
                Application.targetFrameRate = _targetFrameRate;
                QualitySettings.vSyncCount = 0;
            }

            // A fixed step of 50 Hz keeps character physics stable without
            // burning time on a rate the game does not need.
            Time.fixedDeltaTime = 0.02f;
        }

        private void BuildServices()
        {
            var services = new GameObject("~Services");
            services.transform.SetParent(transform, false);
            InputPump.Install(services);
        }

        private void BuildWorld()
        {
            var worldGo = new GameObject("~World");
            WorldRoot = worldGo.transform;

            Lighting = EnvironmentLighting.Create(WorldRoot, _timeOfDay);

            switch (_worldMode)
            {
                case WorldMode.City:
                    // The city arrives in the open-world phase. Until then the
                    // proving ground stands in so the mode is already selectable.
                    Debug.Log("[GameBootstrap] City mode is not built yet; using the proving ground.");
                    ProvingGround.Build(WorldRoot);
                    break;

                default:
                    ProvingGround.Build(WorldRoot);
                    break;
            }
        }

        private void BuildPlayerAndCamera()
        {
            Vector3 spawn = ResolveSpawnPoint();
            Player = PlayerFactory.Create(spawn, Quaternion.Euler(0f, _spawnYaw, 0f), _locomotionConfig);

            GameCamera = CameraFactory.Create(Player.transform, _cameraConfig);
            Player.CameraTransform = GameCamera.transform;

            var lockOn = Player.GetComponent<LockOnSystem>();
            if (lockOn != null) lockOn.Bind(Player, GameCamera);

            var link = GameCamera.gameObject.AddComponent<PlayerCameraLink>();
            link.Bind(Player, GameCamera);
        }

        /// <summary>Drops the spawn point onto the ground so a bad Y never buries the player.</summary>
        private Vector3 ResolveSpawnPoint()
        {
            if (MathUtil.GroundPoint(_spawnPosition, out Vector3 point, 6f, 40f, GameLayers.WorldMask))
                return point + Vector3.up * 0.05f;
            return _spawnPosition;
        }

        public static void SetCursorCaptured(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }
    }
}
