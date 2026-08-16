using System;
using Glowpulse.SaveSystem;
using UnityEngine;

namespace Glowpulse.Core.Settings
{
    /// <summary>
    /// The live settings every system reads, backed by the save file.
    ///
    /// A static service rather than a component, because the camera, the input
    /// provider, the enemy factory and the settings screen all need the same
    /// answer and none of them should have to find a GameObject to get it.
    /// Changes raise <see cref="Changed"/> so a slider moved in the menu is felt
    /// in the same frame, without anybody polling.
    ///
    /// Saving is deferred: dragging a slider must not write a file per frame, so
    /// changes mark the settings dirty and <see cref="Flush"/> writes once.
    /// </summary>
    public static class GameSettings
    {
        private static SaveData _data;
        private static bool _dirty;
        private static int _stageCount = 1;

        /// <summary>Raised whenever any setting changes, including on load.</summary>
        public static event Action Changed;

        public static SaveData Data
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        public static bool IsLoaded => _data != null;

        /// <summary>
        /// Reads the save file. Safe to call more than once; the second call is a
        /// no-op so a system that loads defensively on Awake cannot wipe settings
        /// the menu has already changed.
        /// </summary>
        public static void Load(int stageCount = 1, bool force = false)
        {
            if (_data != null && !force)
            {
                _stageCount = Mathf.Max(_stageCount, stageCount);
                return;
            }

            _stageCount = Mathf.Max(1, stageCount);
            _data = SaveFile.Load(_stageCount);
            _dirty = false;
            Changed?.Invoke();
        }

        /// <summary>Writes the save file if anything changed since the last write.</summary>
        public static void Flush()
        {
            if (_data == null || !_dirty) return;
            _dirty = SaveFile.Save(_data) ? false : _dirty;
        }

        /// <summary>Writes immediately, changed or not. Used when leaving a stage.</summary>
        public static void SaveNow()
        {
            if (_data == null) return;
            if (SaveFile.Save(_data)) _dirty = false;
        }

        public static void MarkDirty()
        {
            _dirty = true;
            Changed?.Invoke();
        }

        // ---- camera and input ----------------------------------------------------

        /// <summary>
        /// Multiplier on mouse look. 1 is the shipped feel; the menu offers roughly
        /// a quarter to three times that, which covers everyone without ever
        /// producing a camera that cannot be aimed.
        /// </summary>
        public static float MouseSensitivity
        {
            get => Data.MouseSensitivity;
            set
            {
                float clamped = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
                if (Mathf.Approximately(Data.MouseSensitivity, clamped)) return;
                Data.MouseSensitivity = clamped;
                MarkDirty();
            }
        }

        public const float MinSensitivity = 0.25f;
        public const float MaxSensitivity = 3f;

        public static bool InvertY
        {
            get => Data.InvertY;
            set
            {
                if (Data.InvertY == value) return;
                Data.InvertY = value;
                MarkDirty();
            }
        }

        public static InputBindings Bindings => Data.Bindings;

        public static void Rebind(GameAction action, KeyCode key, bool secondary = false)
        {
            Data.Bindings.Rebind(action, key, secondary);
            MarkDirty();
        }

        public static void ResetBindings()
        {
            Data.Bindings.ResetToDefaults();
            MarkDirty();
        }

        // ---- difficulty ----------------------------------------------------------

        public static Difficulty Difficulty
        {
            get => Data.Difficulty;
            set
            {
                if (Data.Difficulty == value) return;
                Data.Difficulty = value;
                MarkDirty();
            }
        }

        /// <summary>The multipliers for the current difficulty.</summary>
        public static DifficultyProfile Profile => DifficultyProfile.For(Difficulty);

        // ---- feel and volume -----------------------------------------------------

        public static float MasterVolume
        {
            get => Data.MasterVolume;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(Data.MasterVolume, clamped)) return;
                Data.MasterVolume = clamped;
                MarkDirty();
            }
        }

        public static float ShakeScale
        {
            get => Data.ShakeScale;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(Data.ShakeScale, clamped)) return;
                Data.ShakeScale = clamped;
                MarkDirty();
            }
        }

        public static bool SlowMotionEnabled
        {
            get => Data.SlowMotionEnabled;
            set
            {
                if (Data.SlowMotionEnabled == value) return;
                Data.SlowMotionEnabled = value;
                MarkDirty();
            }
        }

        // ---- progression ---------------------------------------------------------

        public static void CompleteStage(int index)
        {
            Data.CompleteStage(index, _stageCount);
            MarkDirty();
            SaveNow();
        }

        public static void SetCurrentStage(int index)
        {
            if (Data.CurrentStage == index) return;
            Data.CurrentStage = index;
            MarkDirty();
        }

        /// <summary>Throws away every setting and all progress. Used by the menu's wipe.</summary>
        public static void ResetAll()
        {
            _data = new SaveData();
            _dirty = true;
            SaveNow();
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _data = null;
            _dirty = false;
            _stageCount = 1;
            Changed = null;
        }
    }
}
