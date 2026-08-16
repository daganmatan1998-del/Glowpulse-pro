using System.Collections.Generic;
using Glowpulse.Core;
using UnityEditor;
using UnityEngine;

namespace Glowpulse.EditorTools
{
    /// <summary>
    /// Makes a fresh clone of the repository run correctly without any manual
    /// setup. On load it checks that the tags, layers and physics collision
    /// matrix the gameplay code assumes actually exist, and adds anything
    /// missing.
    ///
    /// This matters because Unity regenerates ProjectSettings assets in several
    /// situations, and a silently missing layer turns into combat hits that
    /// never register.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetupValidator
    {
        private const string TagManagerPath = "ProjectSettings/TagManager.asset";

        private static readonly string[] RequiredTags =
        {
            GameTags.Player, GameTags.Enemy, GameTags.Npc,
            GameTags.MissionMarker, GameTags.Interactable
        };

        /// <summary>Layer name by index. Empty strings mean "leave whatever is there".</summary>
        private static readonly (int Index, string Name)[] RequiredLayers =
        {
            (6, GameLayers.PlayerName),
            (7, GameLayers.EnemyName),
            (8, GameLayers.NpcName),
            (9, GameLayers.GroundName),
            (10, GameLayers.EnvironmentName),
            (11, GameLayers.PropName),
            (12, GameLayers.HitboxName),
            (13, GameLayers.InteractableName)
        };

        static ProjectSetupValidator()
        {
            // Deferred: asset serialization is not safe during the static
            // constructor itself.
            EditorApplication.delayCall += () => Validate(false);
        }

        [MenuItem("Glowpulse/Validate Project Setup", false, 1)]
        public static void ValidateFromMenu() => Validate(true);

        public static void Validate(bool verbose)
        {
            var changes = new List<string>();

            EnsureTagsAndLayers(changes);
            EnsurePlayerSettings(changes);

            // Layer indices may have moved, so re-resolve before using them.
            GameLayers.Resolve();
            GameLayers.ApplyCollisionMatrix();

            if (changes.Count > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[Glowpulse] Project setup updated:\n  - " + string.Join("\n  - ", changes));
            }
            else if (verbose)
            {
                Debug.Log("[Glowpulse] Project setup is already correct.");
            }
        }

        private static void EnsureTagsAndLayers(List<string> changes)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(TagManagerPath);
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning($"[Glowpulse] Could not open {TagManagerPath}; skipping tag and layer setup.");
                return;
            }

            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty tags = tagManager.FindProperty("tags");
            SerializedProperty layers = tagManager.FindProperty("layers");

            if (tags != null)
            {
                foreach (string tag in RequiredTags)
                {
                    if (HasTag(tags, tag)) continue;
                    tags.InsertArrayElementAtIndex(tags.arraySize);
                    tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
                    changes.Add($"added tag '{tag}'");
                }
            }

            if (layers != null)
            {
                foreach ((int index, string name) in RequiredLayers)
                {
                    if (index >= layers.arraySize) continue;

                    // Somebody may have already placed the layer elsewhere; that
                    // is fine, the code looks layers up by name.
                    if (FindLayerIndex(layers, name) >= 0) continue;

                    SerializedProperty slot = layers.GetArrayElementAtIndex(index);
                    if (!string.IsNullOrEmpty(slot.stringValue))
                    {
                        Debug.LogWarning($"[Glowpulse] Layer {index} is taken by '{slot.stringValue}', " +
                                         $"so '{name}' was not added. Assign it to a free layer manually.");
                        continue;
                    }

                    slot.stringValue = name;
                    changes.Add($"added layer {index} '{name}'");
                }
            }

            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsurePlayerSettings(List<string> changes)
        {
            // Gamma space makes every URP material and light read wrong. Unity
            // does not default new projects to Linear on every template, so it is
            // forced here rather than left to whoever opens the project.
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                changes.Add("switched the colour space to Linear");
            }

            if (PlayerSettings.productName != "Glowpulse")
            {
                PlayerSettings.productName = "Glowpulse";
                changes.Add("set the product name");
            }
        }

        private static bool HasTag(SerializedProperty tags, string tag)
        {
            for (int i = 0; i < tags.arraySize; i++)
                if (tags.GetArrayElementAtIndex(i).stringValue == tag) return true;
            return false;
        }

        private static int FindLayerIndex(SerializedProperty layers, string name)
        {
            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == name) return i;
            return -1;
        }
    }
}
