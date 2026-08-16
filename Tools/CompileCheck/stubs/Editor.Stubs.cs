// Compile-check stubs for UnityEditor / package editor assemblies.
// Only the surface used by Assets/Scripts/Editor is declared here.
#pragma warning disable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class InitializeOnLoadAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class InitializeOnLoadMethodAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItem : Attribute
    {
        public MenuItem(string itemName) { }
        public MenuItem(string itemName, bool isValidateFunction) { }
        public MenuItem(string itemName, bool isValidateFunction, int priority) { }
    }

    public static class AssetDatabase
    {
        public static void CreateAsset(UnityEngine.Object asset, string path) { }
        public static void AddObjectToAsset(UnityEngine.Object o, UnityEngine.Object parent) { }
        public static void SaveAssets() { }
        public static void SaveAssetIfDirty(UnityEngine.Object o) { }
        public static void Refresh() { }
        public static string CreateFolder(string parent, string newFolderName) => string.Empty;
        public static bool IsValidFolder(string path) => true;
        public static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object => null;
        public static UnityEngine.Object LoadAssetAtPath(string path, Type t) => null;
        public static UnityEngine.Object[] LoadAllAssetsAtPath(string path) => Array.Empty<UnityEngine.Object>();
        public static string[] FindAssets(string filter) => Array.Empty<string>();
        public static string[] FindAssets(string filter, string[] searchInFolders) => Array.Empty<string>();
        public static string GUIDToAssetPath(string guid) => string.Empty;
        public static string AssetPathToGUID(string path) => string.Empty;
        public static bool DeleteAsset(string path) => true;
        public static void StartAssetEditing() { }
        public static void StopAssetEditing() { }
    }

    public static class EditorUtility
    {
        public static void SetDirty(UnityEngine.Object target) { }
        public static bool DisplayDialog(string title, string message, string ok) => true;
        public static bool DisplayDialog(string title, string message, string ok, string cancel) => true;
        public static void DisplayProgressBar(string title, string info, float progress) { }
        public static void ClearProgressBar() { }
    }

    public static class EditorApplication
    {
        public static event Action update;
        public static event Action delayCall;
        public static bool isPlaying { get; }
        public static bool isPlayingOrWillChangePlaymode { get; }
    }

    public static class EditorGUIUtility
    {
        public static float singleLineHeight => 18f;
    }

    public static class EditorGUILayout
    {
        public static void LabelField(string label) { }
        public static void LabelField(string label, GUIStyle style) { }
        public static void HelpBox(string message, MessageType type) { }
        public static void Space() { }
        public static void Space(float pixels) { }
        public static bool Toggle(string label, bool value) => value;
    }

    public enum MessageType { None, Info, Warning, Error }

    public static class EditorPrefs
    {
        public static bool GetBool(string key, bool def = false) => def;
        public static void SetBool(string key, bool value) { }
        public static string GetString(string key, string def = "") => def;
        public static void SetString(string key, string value) { }
    }

    public class EditorWindow : ScriptableObject
    {
        public string title { get; set; }
        public Rect position { get; set; }
        public static T GetWindow<T>() where T : EditorWindow => null;
        public static T GetWindow<T>(string title) where T : EditorWindow => null;
        public void Show() { }
        public void Close() { }
        public void Repaint() { }
        public GUIContent titleContent { get; set; }
    }

    public static class Undo
    {
        public static void RegisterCreatedObjectUndo(UnityEngine.Object o, string name) { }
        public static void RecordObject(UnityEngine.Object o, string name) { }
        public static T AddComponent<T>(GameObject go) where T : Component => null;
    }

    public static class Selection
    {
        public static GameObject activeGameObject { get; set; }
        public static UnityEngine.Object activeObject { get; set; }
        public static GameObject[] gameObjects { get; }
    }

    public class SerializedObject
    {
        public SerializedObject(UnityEngine.Object obj) { }
        public SerializedProperty FindProperty(string path) => null;
        public bool ApplyModifiedProperties() => true;
        public bool ApplyModifiedPropertiesWithoutUndo() => true;
        public void Update() { }
    }

    public class SerializedProperty
    {
        public int arraySize { get; set; }
        public string stringValue { get; set; }
        public int intValue { get; set; }
        public bool boolValue { get; set; }
        public float floatValue { get; set; }
        public UnityEngine.Object objectReferenceValue { get; set; }
        public SerializedProperty GetArrayElementAtIndex(int index) => null;
        public void InsertArrayElementAtIndex(int index) { }
        public void DeleteArrayElementAtIndex(int index) { }
    }

    public static class PrefabUtility
    {
        public static GameObject SaveAsPrefabAsset(GameObject go, string path) => go;
        public static GameObject InstantiatePrefab(UnityEngine.Object o) => null;
    }

    [Serializable]
    public class EditorBuildSettingsScene
    {
        public EditorBuildSettingsScene(string path, bool enabled) { }
        public string path { get; set; }
        public bool enabled { get; set; }
    }

    public static class EditorBuildSettings
    {
        public static EditorBuildSettingsScene[] scenes { get; set; }
    }

    public static class PlayerSettings
    {
        public static string productName { get; set; }
        public static string companyName { get; set; }

        // ColorSpace itself lives in UnityEngine, not UnityEditor.
        public static UnityEngine.ColorSpace colorSpace { get; set; }
    }

    public static class Lightmapping
    {
        public static bool Bake() => true;
    }

    public static class GameObjectUtility
    {
        public static void SetStaticEditorFlags(GameObject go, StaticEditorFlags flags) { }
    }

    [Flags]
    public enum StaticEditorFlags
    {
        ContributeGI = 1,
        OccluderStatic = 2,
        BatchingStatic = 4,
        NavigationStatic = 8,
        OccludeeStatic = 16,
        OffMeshLinkGeneration = 32,
        ReflectionProbeStatic = 64
    }
}

namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode { Single, Additive }

    public static class EditorSceneManager
    {
        public static UnityEngine.SceneManagement.Scene NewScene(NewSceneSetup setup, NewSceneMode mode) => default;
        public static bool SaveScene(UnityEngine.SceneManagement.Scene scene, string path) => true;
        public static bool SaveOpenScenes() => true;
        public static UnityEngine.SceneManagement.Scene OpenScene(string path) => default;
        public static void MarkSceneDirty(UnityEngine.SceneManagement.Scene scene) { }
    }
}
