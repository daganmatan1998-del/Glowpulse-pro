using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
#if URP_PRESENT
using UnityEngine.Rendering.Universal;
#endif

namespace Glowpulse.EditorTools
{
    /// <summary>
    /// Creates and assigns the Universal Render Pipeline assets on first open.
    ///
    /// A clone of this repository has the URP package but no pipeline asset, and
    /// with URP shaders present but no pipeline active every material renders
    /// magenta. Generating the assets from URP's own factory method - rather than
    /// committing hand-written YAML - keeps the project working across URP
    /// versions, since the asset format is version specific but the API is not.
    /// </summary>
    [InitializeOnLoad]
    public static class RenderPipelineSetup
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string RendererPath = SettingsFolder + "/Glowpulse-Renderer.asset";
        private const string PipelinePath = SettingsFolder + "/Glowpulse-URP.asset";

        static RenderPipelineSetup()
        {
            EditorApplication.delayCall += () => EnsurePipeline(false);
        }

        [MenuItem("Glowpulse/Setup/Create URP Assets", false, 20)]
        public static void CreateFromMenu() => EnsurePipeline(true);

        public static void EnsurePipeline(bool force)
        {
#if URP_PRESENT
            if (!force && GraphicsSettings.defaultRenderPipeline != null) return;

            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (existing != null && !force)
            {
                Assign(existing);
                return;
            }

            if (existing != null)
            {
                Assign(existing);
                Debug.Log("[Glowpulse] URP asset already exists and has been re-assigned.");
                return;
            }

            EnsureFolder();

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            rendererData.name = "Glowpulse Renderer";
            AssetDatabase.CreateAsset(rendererData, RendererPath);

            UniversalRenderPipelineAsset pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            if (pipeline == null)
            {
                Debug.LogError("[Glowpulse] URP refused to create a pipeline asset. " +
                               "Create one manually via Assets > Create > Rendering.");
                return;
            }

            pipeline.name = "Glowpulse URP";
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
            AssetDatabase.SaveAssets();

            Assign(pipeline);

            Debug.Log($"[Glowpulse] Created and assigned the URP pipeline asset at {PipelinePath}.");
#else
            if (force)
                Debug.LogWarning("[Glowpulse] The Universal RP package is not installed, " +
                                 "so no pipeline asset was created.");
#endif
        }

        private static void Assign(RenderPipelineAsset pipeline)
        {
            if (pipeline == null) return;

            // Both need setting: the graphics default covers builds, and the
            // quality-level override is what the editor actually renders with.
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(SettingsFolder)) return;
            Directory.CreateDirectory(SettingsFolder);
            AssetDatabase.Refresh();
        }
    }
}
