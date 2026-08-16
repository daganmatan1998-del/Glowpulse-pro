using System;
using System.Collections.Generic;
using Glowpulse.Core.Settings;
using UnityEngine;

namespace Glowpulse.SaveSystem
{
    /// <summary>
    /// Everything that survives closing the game, in one serialisable object.
    ///
    /// One file rather than a settings file plus a progress file: they are written
    /// at the same moments and a half-applied pair is worse than either. Every
    /// field has a sane value at construction, so a missing or truncated save
    /// loads as a fresh game rather than as an error.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>Bumped when the shape changes, so an old file can be migrated.</summary>
        public int Version = 1;

        // ---- settings ----------------------------------------------------------
        public float MouseSensitivity = 1f;
        public bool InvertY;
        public Difficulty Difficulty = Difficulty.Normal;
        public InputBindings Bindings = new InputBindings();

        public float MasterVolume = 1f;
        public float ShakeScale = 1f;
        public bool SlowMotionEnabled = true;

        // ---- progression -------------------------------------------------------
        public int Level = 1;
        public int Xp;
        public int Money;

        /// <summary>Highest stage index the player has unlocked. Stage 1 is index 0.</summary>
        public int HighestUnlockedStage;

        /// <summary>Stage the player was last in, so "continue" lands somewhere sensible.</summary>
        public int CurrentStage;

        /// <summary>Indices of stages beaten at least once.</summary>
        public List<int> CompletedStages = new List<int>();

        public bool IsStageUnlocked(int index) => index >= 0 && index <= HighestUnlockedStage;

        public bool IsStageCompleted(int index)
        {
            return CompletedStages != null && CompletedStages.Contains(index);
        }

        /// <summary>
        /// Records a stage win and opens the next one. Idempotent, because a stage
        /// can be replayed and finishing it again must not corrupt the record or
        /// wind progression backwards.
        /// </summary>
        public void CompleteStage(int index, int stageCount)
        {
            if (index < 0) return;

            CompletedStages ??= new List<int>();
            if (!CompletedStages.Contains(index)) CompletedStages.Add(index);

            int next = index + 1;
            if (next < stageCount && next > HighestUnlockedStage) HighestUnlockedStage = next;
        }

        /// <summary>
        /// Fixes up a file written by an older or corrupted build. Called on every
        /// load, so the rest of the game can treat the data as trustworthy.
        /// </summary>
        public void Repair(int stageCount)
        {
            MouseSensitivity = Mathf.Clamp(MouseSensitivity, 0.1f, 5f);
            MasterVolume = Mathf.Clamp01(MasterVolume);
            ShakeScale = Mathf.Clamp01(ShakeScale);

            if (!Enum.IsDefined(typeof(Difficulty), Difficulty)) Difficulty = Difficulty.Normal;

            Bindings ??= new InputBindings();
            Bindings.Repair();

            CompletedStages ??= new List<int>();
            for (int i = CompletedStages.Count - 1; i >= 0; i--)
                if (CompletedStages[i] < 0 || CompletedStages[i] >= stageCount)
                    CompletedStages.RemoveAt(i);

            HighestUnlockedStage = Mathf.Clamp(HighestUnlockedStage, 0, Mathf.Max(0, stageCount - 1));
            CurrentStage = Mathf.Clamp(CurrentStage, 0, HighestUnlockedStage);

            Level = Mathf.Max(1, Level);
            Xp = Mathf.Max(0, Xp);
            Money = Mathf.Max(0, Money);
        }
    }
}
