using Glowpulse.AI;
using Glowpulse.Audio;
using Glowpulse.CameraSystem;
using Glowpulse.Core.Characters;
using Glowpulse.Core.InputSystem;
using Glowpulse.Core.Settings;
using Glowpulse.Core.Timing;
using Glowpulse.Enemies;
using Glowpulse.Player;
using Glowpulse.VFX;
using Glowpulse.World;
using Glowpulse.World.City;
using Glowpulse.World.Npc;
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
        [SerializeField] private WorldMode _worldMode = WorldMode.City;
        [SerializeField] private TimeOfDayPreset _timeOfDay = TimeOfDayPreset.GoldenHour;

        [Tooltip("Seed for the city layout. The same seed always produces the same city.")]
        [SerializeField] private int _citySeed = 20260810;

        [SerializeField] private CitySettings _citySettings;

        [Tooltip("Populates the streets with pedestrians. City mode only.")]
        [SerializeField] private bool _populateCity = true;

        [Tooltip("How many civilians exist at once. They follow the player rather than living in the city.")]
        [Range(0, 80)] [SerializeField] private int _crowdSize = 26;

        [Header("Player")]
        [SerializeField] private Vector3 _spawnPosition = new Vector3(0f, 1.5f, -6f);
        [SerializeField] private float _spawnYaw;
        [SerializeField] private PlayerLocomotionConfig _locomotionConfig;

        [Header("Camera")]
        [SerializeField] private CameraConfig _cameraConfig;

        [Header("Training")]
        [Tooltip("Spawns practice dummies. Useful in the proving ground, noise in the city.")]
        [SerializeField] private bool _spawnTrainingDummies;

        [SerializeField] private int _trainingDummyCount = 2;

        [Tooltip("Spawns a live encounter of real enemies in the proving ground.")]
        [SerializeField] private bool _spawnTestEncounter = true;

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

        /// <summary>The generated city, or null in the proving ground.</summary>
        public CityLayout City { get; private set; }

        /// <summary>The pavement graph the crowd walks, or null without a city.</summary>
        public PedestrianNetwork Pavements { get; private set; }

        public CrowdDirector Crowd { get; private set; }

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

            // Settings come first: the camera, the input provider and the enemy
            // factory all read them while they are being built.
            GameSettings.Load();

            ApplyQualityDefaults();
            GameLayers.ApplyCollisionMatrix();

            BuildServices();

            // After the services exist, because this pushes settings into them.
            ApplyPlayerSettings();

            BuildWorld();
            BuildPlayerAndCamera();

            StartAmbience();

            if (_captureCursor) SetCursorCaptured(true);

            Ready?.Invoke(this);
        }

        private void OnEnable() => GameSettings.Changed += ApplyPlayerSettings;

        private void OnDisable() => GameSettings.Changed -= ApplyPlayerSettings;

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// Last chance to write settings out. Unity calls this on quit and when
        /// a mobile app is backgrounded, which is the only save point some
        /// players ever reach.
        /// </summary>
        private void OnApplicationQuit() => GameSettings.Flush();

        private void OnApplicationPause(bool paused)
        {
            if (paused) GameSettings.Flush();
        }

        /// <summary>
        /// Pushes the saved settings into the systems that cache them. Also called
        /// whenever the settings change, so a slider moved in the pause menu is
        /// felt without leaving the menu.
        /// </summary>
        private static void ApplyPlayerSettings()
        {
            Combat.CombatFeedback.HitStopScale = 1f;
            Combat.CombatFeedback.ShakeScale = GameSettings.ShakeScale;
            Combat.CombatFeedback.SlowMotionEnabled = GameSettings.SlowMotionEnabled;

            AudioManager audio = AudioManager.Instance;
            if (audio != null) audio.MasterVolume = GameSettings.MasterVolume;

            // How many enemies may swing at once is the single biggest lever on
            // how hard a fight feels, so difficulty owns it.
            CombatDirector director = CombatDirector.Instance;
            if (director != null)
                director.MaxSimultaneousAttackers = GameSettings.Profile.SimultaneousAttackers;
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
            TimeController.Install(services);
            AudioManager.Install(services);
            ImpactEffects.Install(services);
            CombatDirector.Install(services);

            Crowd = services.AddComponent<CrowdDirector>();

            Combat.CombatPoses.EnsureRegistered();
            World.Npc.CivilianPoses.EnsureRegistered();
        }

        private void BuildWorld()
        {
            var worldGo = new GameObject("~World");
            WorldRoot = worldGo.transform;

            Lighting = EnvironmentLighting.Create(WorldRoot, _timeOfDay);

            switch (_worldMode)
            {
                case WorldMode.City:
                    BuildCity();
                    break;

                default:
                    ProvingGround.Build(WorldRoot);
                    break;
            }
        }

        private void BuildCity()
        {
            CitySettings settings = _citySettings != null ? _citySettings : CitySettings.Default;
            City = CityLayout.Generate(settings, _citySeed);

            // A malformed layout would show up as buildings inside each other, so
            // it is worth one check at load rather than a puzzling screenshot.
            System.Collections.Generic.List<string> problems = City.Validate();
            if (problems.Count > 0)
                Debug.LogWarning($"[City] layout problems:\n  - {string.Join("\n  - ", problems)}");

            new CityBuilder(City).Build(WorldRoot);
            _spawnPosition = City.PlayerSpawn + Vector3.up * 1.5f;

            Pavements = PedestrianNetwork.Build(City);

            // A pedestrian who walks onto a stranded node stands there for the
            // rest of the game, so the graph is worth the same load-time check the
            // layout gets.
            System.Collections.Generic.List<string> walkProblems = Pavements.Validate();
            if (walkProblems.Count > 0)
                Debug.LogWarning("[City] pedestrian network problems:\n  - " +
                                 string.Join("\n  - ", walkProblems));
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

            // Every enemy arranges itself around the player, so the director needs
            // to know who that is before any of them spawn.
            CombatDirector director = CombatDirector.Instance;
            if (director != null) director.Focus = Player.transform;

            PopulateStreets();

            if (_spawnTrainingDummies) SpawnTrainingDummies(spawn);
            if (_spawnTestEncounter) SpawnTestEncounter(spawn);
        }

        /// <summary>
        /// Hands the crowd its pavements and someone to follow. Skipped entirely
        /// without a city, because the proving ground has no streets to walk.
        /// </summary>
        private void PopulateStreets()
        {
            if (Crowd == null) return;

            if (!_populateCity || Pavements == null || Pavements.NodeCount == 0)
            {
                Crowd.Population = 0;
                return;
            }

            Crowd.Population = _crowdSize;
            Crowd.Configure(Pavements, Player.transform, _citySeed ^ 0x5F3A);
        }

        /// <summary>
        /// Stages encounters in the city's own open spaces - the square, the
        /// loading docks - so a fight always has room to breathe, and always
        /// somewhere the player has to walk to.
        /// </summary>
        private void SpawnTestEncounter(Vector3 playerSpawn)
        {
            if (City == null)
            {
                Vector3 center = playerSpawn + new Vector3(0f, 0f, 22f);
                if (MathUtil.GroundPoint(center, out Vector3 grounded, 8f, 40f, GameLayers.WorldMask))
                    center = grounded;

                EncounterSpawner.Create("Test Encounter", center, WorldRoot, triggerRadius: 15f,
                    new EnemyGroup(EnemyKind.Brawler, 2),
                    new EnemyGroup(EnemyKind.Runner, 2),
                    new EnemyGroup(EnemyKind.Bruiser, 1));
                return;
            }

            CityLocation? arena = City.PickArena(playerSpawn, minDistance: 12f);
            if (arena == null) return;

            EncounterSpawner.Create($"Encounter - {arena.Value.Name}", arena.Value.Position,
                WorldRoot, triggerRadius: Mathf.Max(14f, arena.Value.Radius + 6f),
                new EnemyGroup(EnemyKind.Brawler, 2),
                new EnemyGroup(EnemyKind.Runner, 2),
                new EnemyGroup(EnemyKind.Bruiser, 1));
        }

        /// <summary>
        /// Puts a few dummies in a loose arc in front of the spawn: one that
        /// blocks, and the rest open, which covers most of what needs checking
        /// when tuning a move.
        /// </summary>
        private void SpawnTrainingDummies(Vector3 playerSpawn)
        {
            var root = new GameObject("~Training Dummies");
            root.transform.SetParent(WorldRoot, false);

            int count = Mathf.Clamp(_trainingDummyCount, 0, 12);
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.Lerp(-52f, 52f, count <= 1 ? 0.5f : i / (float)(count - 1));
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 position = playerSpawn + direction * Mathf.Lerp(5.5f, 8.5f, (i % 3) / 2f);

                if (MathUtil.GroundPoint(position, out Vector3 grounded, 6f, 30f, GameLayers.WorldMask))
                    position = grounded;

                CharacterStyle style = i % 3 == 0
                    ? CharacterStyle.Bruiser()
                    : (i % 3 == 1 ? CharacterStyle.Thug() : CharacterStyle.Runner());

                DummyFactory.Create($"Training Dummy {i + 1}", position,
                    Quaternion.LookRotation(-direction, Vector3.up), style, root.transform,
                    blocks: i == count - 1);
            }
        }

        /// <summary>Drops the spawn point onto the ground so a bad Y never buries the player.</summary>
        private Vector3 ResolveSpawnPoint()
        {
            if (MathUtil.GroundPoint(_spawnPosition, out Vector3 point, 6f, 40f, GameLayers.WorldMask))
                return point + Vector3.up * 0.05f;
            return _spawnPosition;
        }

        private static void StartAmbience()
        {
            AudioManager audio = AudioManager.Instance;
            if (audio == null) return;
            audio.PlayAmbience(ProceduralAudio.CityAmbience());
        }

        public static void SetCursorCaptured(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }
    }
}
