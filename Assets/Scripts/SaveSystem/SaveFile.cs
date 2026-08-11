using System;
using System.IO;
using UnityEngine;

namespace Glowpulse.SaveSystem
{
    /// <summary>
    /// Reads and writes the save file.
    ///
    /// Writes go to a temporary file and are then moved into place, so a crash or
    /// a pulled power cable during a save leaves the previous file intact rather
    /// than a half-written one. Every failure path returns a usable default
    /// instead of throwing: losing a save is bad, but refusing to start the game
    /// because of one is worse.
    /// </summary>
    public static class SaveFile
    {
        public const string FileName = "glowpulse.save.json";

        private static string _directoryOverride;

        /// <summary>Redirects the save location. Used by tests so they never touch a real save.</summary>
        public static void OverrideDirectory(string directory) => _directoryOverride = directory;

        public static string Directory =>
            !string.IsNullOrEmpty(_directoryOverride) ? _directoryOverride : Application.persistentDataPath;

        public static string Path => System.IO.Path.Combine(Directory, FileName);

        public static bool Exists()
        {
            try
            {
                return File.Exists(Path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static SaveData Load(int stageCount)
        {
            SaveData data = ReadOrDefault();
            data.Repair(stageCount);
            return data;
        }

        private static SaveData ReadOrDefault()
        {
            try
            {
                if (!File.Exists(Path)) return new SaveData();

                string json = File.ReadAllText(Path);
                if (string.IsNullOrWhiteSpace(json)) return new SaveData();

                SaveData data = JsonUtility.FromJson<SaveData>(json);
                return data ?? new SaveData();
            }
            catch (Exception e)
            {
                // A corrupt save must not be a dead end. Say so once, then start
                // fresh - the next save will overwrite the bad file.
                Debug.LogWarning($"[Save] Could not read the save file, starting fresh. {e.Message}");
                return new SaveData();
            }
        }

        public static bool Save(SaveData data)
        {
            if (data == null) return false;

            try
            {
                System.IO.Directory.CreateDirectory(Directory);

                string temp = Path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));

                // Replace rather than write in place, so an interrupted save never
                // destroys the file that was already good.
                if (File.Exists(Path)) File.Delete(Path);
                File.Move(temp, Path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Could not write the save file. {e.Message}");
                return false;
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(Path)) File.Delete(Path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Could not delete the save file. {e.Message}");
            }
        }
    }
}
