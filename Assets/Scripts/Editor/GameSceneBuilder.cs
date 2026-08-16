using System.Collections.Generic;
using System.IO;
using Glowpulse.Core.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Glowpulse.EditorTools
{
    /// <summary>
    /// Creates the game scene. The scene is deliberately almost empty - a single
    /// <see cref="GameBootstrap"/> builds everything at runtime - which keeps the
    /// project free of large binary scene files that cannot be reviewed or merged.
    /// </summary>
    public static class GameSceneBuilder
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string ScenePath = ScenesFolder + "/Game.unity";

        [MenuItem("Glowpulse/Setup/Create Game Scene", false, 21)]
        public static void CreateGameScene()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Replace the game scene?",
                    $"{ScenePath} already exists. Replace it?", "Replace", "Cancel"))
                return;

            if (!Directory.Exists(ScenesFolder))
            {
                Directory.CreateDirectory(ScenesFolder);
                AssetDatabase.Refresh();
            }

            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("Game Bootstrap");
            go.AddComponent<GameBootstrap>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);

            Debug.Log($"[Glowpulse] Created {ScenePath}. Press Play to run the game.");
        }

        [MenuItem("Glowpulse/Setup/Run Full Setup", false, 0)]
        public static void RunFullSetup()
        {
            ProjectSetupValidator.Validate(true);
            RenderPipelineSetup.EnsurePipeline(true);
            if (!File.Exists(ScenePath)) CreateGameScene();
            else AddToBuildSettings(ScenePath);
        }

        private static void AddToBuildSettings(string path)
        {
            EditorBuildSettingsScene[] current = EditorBuildSettings.scenes ??
                                                 new EditorBuildSettingsScene[0];

            for (int i = 0; i < current.Length; i++)
                if (current[i] != null && current[i].path == path)
                    return;

            var list = new List<EditorBuildSettingsScene>(current)
            {
                new EditorBuildSettingsScene(path, true)
            };

            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
