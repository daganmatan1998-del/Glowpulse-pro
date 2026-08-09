using UnityEngine;
#if URP_PRESENT
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace Glowpulse.World
{
    /// <summary>Named lighting presets for the city. Each one sets sun, ambient, fog and grading together.</summary>
    public enum TimeOfDayPreset
    {
        Noon = 0,
        GoldenHour = 1,
        Dusk = 2,
        Night = 3
    }

    /// <summary>
    /// Owns the scene's atmosphere: directional sun, ambient light, fog, sky and
    /// the post-processing stack. Keeping it in one component means the whole
    /// mood of the game can be changed from a single call, and the URP-specific
    /// parts stay behind one compile guard.
    /// </summary>
    public sealed class EnvironmentLighting : MonoBehaviour
    {
        [SerializeField] private TimeOfDayPreset _preset = TimeOfDayPreset.GoldenHour;

        private Light _sun;
        private Light _fill;

        public Light Sun => _sun;
        public TimeOfDayPreset Preset => _preset;

        /// <summary>Creates the lighting rig and applies a preset.</summary>
        public static EnvironmentLighting Create(Transform parent, TimeOfDayPreset preset)
        {
            var go = new GameObject("Environment Lighting");
            if (parent != null) go.transform.SetParent(parent, false);

            var lighting = go.AddComponent<EnvironmentLighting>();
            lighting._preset = preset;
            lighting.Build();
            return lighting;
        }

        private void Build()
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            _sun = sunGo.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.shadows = LightShadows.Soft;
            _sun.shadowStrength = 0.82f;
            _sun.shadowBias = 0.05f;
            _sun.shadowNormalBias = 0.35f;

            // A dim, cool light from the opposite side stops shadowed faces from
            // going flat black. Cheaper and more controllable than raising ambient.
            var fillGo = new GameObject("Sky Fill");
            fillGo.transform.SetParent(transform, false);
            _fill = fillGo.AddComponent<Light>();
            _fill.type = LightType.Directional;
            _fill.shadows = LightShadows.None;

            Apply(_preset);
            BuildPostProcessing();
        }

        public void Apply(TimeOfDayPreset preset)
        {
            _preset = preset;

            Color sunColor;
            float sunIntensity;
            Vector3 sunAngles;
            Color skyTint;
            Color ambientSky, ambientEquator, ambientGround;
            Color fogColor;
            float fogDensity;
            float atmosphere;
            float exposure;
            Color fillColor;
            float fillIntensity;

            switch (preset)
            {
                case TimeOfDayPreset.Noon:
                    sunColor = Hex("FFF6E8");
                    sunIntensity = 2.1f;
                    sunAngles = new Vector3(58f, 35f, 0f);
                    skyTint = Hex("7FA9D6");
                    ambientSky = Hex("8FB4D9");
                    ambientEquator = Hex("9AA0A6");
                    ambientGround = Hex("4A463F");
                    fogColor = Hex("B8C6D4");
                    fogDensity = 0.0055f;
                    atmosphere = 1.0f;
                    exposure = 1.1f;
                    fillColor = Hex("9FC0E0");
                    fillIntensity = 0.28f;
                    break;

                case TimeOfDayPreset.Dusk:
                    sunColor = Hex("FF9E5E");
                    sunIntensity = 0.95f;
                    sunAngles = new Vector3(6f, 200f, 0f);
                    skyTint = Hex("4B4E78");
                    ambientSky = Hex("3E4468");
                    ambientEquator = Hex("4A4152");
                    ambientGround = Hex("22201F");
                    fogColor = Hex("52506B");
                    fogDensity = 0.014f;
                    atmosphere = 1.5f;
                    exposure = 0.95f;
                    fillColor = Hex("6A7FC0");
                    fillIntensity = 0.35f;
                    break;

                case TimeOfDayPreset.Night:
                    sunColor = Hex("9DB2E8");
                    sunIntensity = 0.32f;
                    sunAngles = new Vector3(38f, 210f, 0f);
                    skyTint = Hex("1B2137");
                    ambientSky = Hex("1E2540");
                    ambientEquator = Hex("242433");
                    ambientGround = Hex("131316");
                    fogColor = Hex("1B1F2E");
                    fogDensity = 0.022f;
                    atmosphere = 1.7f;
                    exposure = 0.75f;
                    fillColor = Hex("3E5A9B");
                    fillIntensity = 0.22f;
                    break;

                default: // GoldenHour
                    sunColor = Hex("FFD2A1");
                    sunIntensity = 1.55f;
                    sunAngles = new Vector3(22f, 155f, 0f);
                    skyTint = Hex("6E86B5");
                    ambientSky = Hex("6F87B8");
                    ambientEquator = Hex("8A7F7A");
                    ambientGround = Hex("3B342C");
                    fogColor = Hex("A69382");
                    fogDensity = 0.0095f;
                    atmosphere = 1.25f;
                    exposure = 1.05f;
                    fillColor = Hex("7E9BD0");
                    fillIntensity = 0.3f;
                    break;
            }

            if (_sun != null)
            {
                _sun.color = sunColor;
                _sun.intensity = sunIntensity;
                _sun.transform.rotation = Quaternion.Euler(sunAngles);
            }

            if (_fill != null)
            {
                _fill.color = fillColor;
                _fill.intensity = fillIntensity;
                _fill.transform.rotation = Quaternion.Euler(sunAngles + new Vector3(20f, 180f, 0f));
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;

            ApplySkybox(skyTint, atmosphere, exposure, ambientGround);
            RenderSettings.sun = _sun;
        }

        private void ApplySkybox(Color tint, float atmosphere, float exposure, Color ground)
        {
            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null) return;

            Material sky = RenderSettings.skybox;
            if (sky == null || sky.shader != shader)
            {
                sky = new Material(shader) { name = "Glowpulse Sky" };
                RenderSettings.skybox = sky;
            }

            sky.SetFloat("_SunSize", 0.045f);
            sky.SetFloat("_SunSizeConvergence", 4f);
            sky.SetFloat("_AtmosphereThickness", atmosphere);
            sky.SetColor("_SkyTint", tint);
            sky.SetColor("_GroundColor", ground);
            sky.SetFloat("_Exposure", exposure);

            DynamicGI.UpdateEnvironment();
        }

        /// <summary>
        /// Builds the post-processing volume. Deliberately restrained: tonemapping
        /// and a touch of bloom and vignette do the heavy lifting, and nothing
        /// here obscures the action.
        /// </summary>
        private void BuildPostProcessing()
        {
#if URP_PRESENT
            var go = new GameObject("Post Processing");
            go.transform.SetParent(transform, false);

            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Glowpulse Post Profile";
            volume.sharedProfile = profile;

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.62f);
            bloom.highQualityFiltering.Override(true);

            var grading = profile.Add<ColorAdjustments>(true);
            grading.postExposure.Override(0.15f);
            grading.contrast.Override(12f);
            grading.saturation.Override(6f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.42f);
            vignette.rounded.Override(false);
#endif
        }

        private static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out Color c) ? c : Color.magenta;
        }
    }
}
