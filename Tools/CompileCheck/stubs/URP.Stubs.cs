// Compile-check stubs for the Universal Render Pipeline + SRP Core volume
// framework, and for com.unity.ai.navigation. Only the surface used by the
// project's URP_PRESENT / AI_NAVIGATION_PRESENT guarded code is declared.
#pragma warning disable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Rendering
{
    public abstract class VolumeParameter
    {
        public bool overrideState { get; set; }
    }

    public class VolumeParameter<T> : VolumeParameter
    {
        public T value { get; set; }
        public void Override(T x) { }
    }

    public class BoolParameter : VolumeParameter<bool> { }
    public class IntParameter : VolumeParameter<int> { }
    public class ClampedIntParameter : IntParameter { }
    public class MinIntParameter : IntParameter { }
    public class NoInterpIntParameter : VolumeParameter<int> { }
    public class FloatParameter : VolumeParameter<float> { }
    public class MinFloatParameter : FloatParameter { }
    public class MaxFloatParameter : FloatParameter { }
    public class ClampedFloatParameter : FloatParameter { }
    public class ColorParameter : VolumeParameter<Color> { }
    public class Vector2Parameter : VolumeParameter<Vector2> { }
    public class Vector3Parameter : VolumeParameter<Vector3> { }
    public class TextureParameter : VolumeParameter<Texture> { }

    public abstract class VolumeComponent : ScriptableObject
    {
        public bool active { get; set; }
        public void SetAllOverridesTo(bool state) { }
    }

    public sealed class VolumeProfile : ScriptableObject
    {
        public List<VolumeComponent> components { get; }
        public T Add<T>(bool overrides = false) where T : VolumeComponent => null;
        public bool TryGet<T>(out T component) where T : VolumeComponent { component = null; return false; }
        public bool Has<T>() where T : VolumeComponent => false;
        public void Remove<T>() where T : VolumeComponent { }
    }

    public class Volume : MonoBehaviour
    {
        public bool isGlobal { get; set; }
        public float priority { get; set; }
        public float blendDistance { get; set; }
        public float weight { get; set; }
        public VolumeProfile profile { get; set; }
        public VolumeProfile sharedProfile { get; set; }
    }
}

namespace UnityEngine.Rendering.Universal
{
    public enum TonemappingMode { None, Neutral, ACES }
    public class TonemappingModeParameter : VolumeParameter<TonemappingMode> { }

    public sealed class Tonemapping : VolumeComponent
    {
        public TonemappingModeParameter mode;
    }

    public sealed class Bloom : VolumeComponent
    {
        public MinFloatParameter threshold;
        public MinFloatParameter intensity;
        public ClampedFloatParameter scatter;
        public ColorParameter tint;
        public BoolParameter highQualityFiltering;
        public ClampedFloatParameter dirtIntensity;
    }

    public sealed class ColorAdjustments : VolumeComponent
    {
        public FloatParameter postExposure;
        public ClampedFloatParameter contrast;
        public ColorParameter colorFilter;
        public ClampedFloatParameter hueShift;
        public ClampedFloatParameter saturation;
    }

    public enum VignetteMode { Procedural, Masked }

    public sealed class Vignette : VolumeComponent
    {
        public ColorParameter color;
        public Vector2Parameter center;
        public ClampedFloatParameter intensity;
        public ClampedFloatParameter smoothness;
        public BoolParameter rounded;
    }

    public enum MotionBlurMode { CameraOnly, CameraAndObjects }
    public class MotionBlurModeParameter : VolumeParameter<MotionBlurMode> { }
    public enum MotionBlurQuality { Low, Medium, High }
    public class MotionBlurQualityParameter : VolumeParameter<MotionBlurQuality> { }

    public sealed class MotionBlur : VolumeComponent
    {
        public MotionBlurModeParameter mode;
        public MotionBlurQualityParameter quality;
        public ClampedFloatParameter intensity;
        public ClampedFloatParameter clamp;
    }

    public enum DepthOfFieldMode { Off, Gaussian, Bokeh }
    public class DepthOfFieldModeParameter : VolumeParameter<DepthOfFieldMode> { }

    public sealed class DepthOfField : VolumeComponent
    {
        public DepthOfFieldModeParameter mode;
        public MinFloatParameter gaussianStart;
        public MinFloatParameter gaussianEnd;
        public ClampedFloatParameter gaussianMaxRadius;
        public MinFloatParameter focusDistance;
        public ClampedFloatParameter aperture;
        public ClampedFloatParameter focalLength;
    }

    public sealed class FilmGrain : VolumeComponent
    {
        public ClampedFloatParameter intensity;
        public ClampedFloatParameter response;
    }

    public sealed class ChromaticAberration : VolumeComponent
    {
        public ClampedFloatParameter intensity;
    }

    public enum CameraRenderType { Base, Overlay }
    public enum AntialiasingMode { None, FastApproximateAntialiasing, SubpixelMorphologicalAntiAliasing }
    public enum AntialiasingQuality { Low, Medium, High }

    public class UniversalAdditionalCameraData : MonoBehaviour
    {
        public bool renderPostProcessing { get; set; }
        public AntialiasingMode antialiasing { get; set; }
        public AntialiasingQuality antialiasingQuality { get; set; }
        public bool renderShadows { get; set; }
        public bool requiresDepthOption { get; set; }
        public CameraRenderType renderType { get; set; }
        public bool dithering { get; set; }
        public float volumeTrigger { get; set; }
    }

    public class UniversalAdditionalLightData : MonoBehaviour
    {
        public bool usePipelineSettings { get; set; }
    }

    public abstract class ScriptableRendererData : ScriptableObject { }

    public class UniversalRendererData : ScriptableRendererData { }

    // Only Create is declared: the asset's own properties differ in accessibility
    // between URP versions, so the setup tool leaves them at their defaults and
    // lets the inspector own the tuning.
    public class UniversalRenderPipelineAsset : RenderPipelineAsset
    {
        public static UniversalRenderPipelineAsset Create(ScriptableRendererData data = null) => null;
        public override RenderPipeline CreatePipeline() => null;
    }
}

namespace Unity.AI.Navigation
{
    public enum CollectObjects { All = 0, Volume = 1, Children = 2 }

    public class NavMeshSurface : MonoBehaviour
    {
        public int agentTypeID { get; set; }
        public CollectObjects collectObjects { get; set; }
        public Vector3 size { get; set; }
        public Vector3 center { get; set; }
        public LayerMask layerMask { get; set; }
        public int defaultArea { get; set; }
        public bool ignoreNavMeshAgent { get; set; }
        public bool ignoreNavMeshObstacle { get; set; }
        public bool overrideTileSize { get; set; }
        public int tileSize { get; set; }
        public bool overrideVoxelSize { get; set; }
        public float voxelSize { get; set; }
        public UnityEngine.AI.NavMeshData navMeshData { get; set; }
        public void BuildNavMesh() { }
        public void RemoveData() { }
        public void AddData() { }
        public void UpdateNavMesh(UnityEngine.AI.NavMeshData data) { }
    }

    public class NavMeshModifier : MonoBehaviour
    {
        public bool overrideArea { get; set; }
        public int area { get; set; }
        public bool ignoreFromBuild { get; set; }
    }
}
