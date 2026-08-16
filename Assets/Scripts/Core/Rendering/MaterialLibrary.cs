using System.Collections.Generic;
using UnityEngine;

namespace Glowpulse.Core.Rendering
{
    /// <summary>
    /// Creates and caches the materials used by the procedurally built placeholder
    /// art. Everything goes through here so that (a) identical surfaces share one
    /// material and batch together, and (b) swapping in authored art later means
    /// replacing this one class rather than hunting through the scene.
    ///
    /// Works on URP and falls back to the built-in Standard shader, so the project
    /// still renders if the render pipeline asset is missing.
    /// </summary>
    public static class MaterialLibrary
    {
        private static readonly Dictionary<int, Material> Cache = new Dictionary<int, Material>(128);

        private static Shader _litShader;
        private static Shader _unlitShader;
        private static bool _shadersResolved;
        private static bool _isUrp;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");

        public static bool IsUniversalPipeline
        {
            get
            {
                ResolveShaders();
                return _isUrp;
            }
        }

        /// <summary>Opaque physically-based surface.</summary>
        public static Material Lit(Color color, float smoothness = 0.25f, float metallic = 0f)
        {
            int key = HashKey(color, smoothness, metallic, Color.black, 0);
            if (Cache.TryGetValue(key, out Material cached) && cached != null) return cached;

            ResolveShaders();
            Material m = new Material(_litShader) { name = $"PB_Lit_{ColorHex(color)}" };
            ApplyColor(m, color);
            ApplySmoothness(m, smoothness);
            m.SetFloat(MetallicId, metallic);
            m.enableInstancing = true;
            Cache[key] = m;
            return m;
        }

        /// <summary>Surface that also emits light-coloured glow (signs, lamps, screens).</summary>
        public static Material Emissive(Color color, float intensity = 2f, float smoothness = 0.4f)
        {
            Color emission = color * Mathf.Max(0f, intensity);
            int key = HashKey(color, smoothness, 0f, emission, 1);
            if (Cache.TryGetValue(key, out Material cached) && cached != null) return cached;

            ResolveShaders();
            Material m = new Material(_litShader) { name = $"PB_Emissive_{ColorHex(color)}" };
            ApplyColor(m, color);
            ApplySmoothness(m, smoothness);
            m.SetFloat(MetallicId, 0f);
            m.EnableKeyword("_EMISSION");
            m.SetColor(EmissionColorId, emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.enableInstancing = true;
            Cache[key] = m;
            return m;
        }

        /// <summary>Unlit flat colour - used for UI-ish world markers and debug shapes.</summary>
        public static Material Unlit(Color color)
        {
            int key = HashKey(color, 0f, 0f, Color.black, 2);
            if (Cache.TryGetValue(key, out Material cached) && cached != null) return cached;

            ResolveShaders();
            Material m = new Material(_unlitShader) { name = $"PB_Unlit_{ColorHex(color)}" };
            ApplyColor(m, color);
            m.enableInstancing = true;
            Cache[key] = m;
            return m;
        }

        /// <summary>Transparent surface. Alpha comes from the colour.</summary>
        public static Material Transparent(Color color, bool unlit = true)
        {
            int key = HashKey(color, 0f, 0f, Color.black, unlit ? 3 : 4);
            if (Cache.TryGetValue(key, out Material cached) && cached != null) return cached;

            ResolveShaders();
            Material m = new Material(unlit ? _unlitShader : _litShader) { name = $"PB_Trans_{ColorHex(color)}" };
            ApplyColor(m, color);
            MakeTransparent(m);
            m.enableInstancing = true;
            Cache[key] = m;
            return m;
        }

        /// <summary>
        /// Turns an opaque URP/Standard material instance into an alpha-blended one.
        /// Both pipelines need the same render-state changes, only the property
        /// names for the surface-type toggle differ.
        /// </summary>
        public static void MakeTransparent(Material m)
        {
            if (m == null) return;

            if (m.HasProperty(SurfaceId)) m.SetFloat(SurfaceId, 1f);   // URP: 0 opaque, 1 transparent
            if (m.HasProperty(BlendId)) m.SetFloat(BlendId, 0f);       // URP: alpha blend

            m.SetFloat(SrcBlendId, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat(DstBlendId, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat(ZWriteId, 0f);

            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        public static void SetColor(Material m, Color color)
        {
            if (m == null) return;
            ApplyColor(m, color);
        }

        public static void SetEmission(Material m, Color emission)
        {
            if (m == null) return;
            if (!m.HasProperty(EmissionColorId)) return;
            m.EnableKeyword("_EMISSION");
            m.SetColor(EmissionColorId, emission);
        }

        /// <summary>Drops every cached material. Called when a play session ends.</summary>
        public static void Clear() => Cache.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
            _shadersResolved = false;
            _litShader = null;
            _unlitShader = null;
        }

        private static void ApplyColor(Material m, Color color)
        {
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, color);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, color);
        }

        private static void ApplySmoothness(Material m, float smoothness)
        {
            if (m.HasProperty(SmoothnessId)) m.SetFloat(SmoothnessId, smoothness);
            if (m.HasProperty(GlossinessId)) m.SetFloat(GlossinessId, smoothness);
        }

        private static void ResolveShaders()
        {
            if (_shadersResolved) return;
            _shadersResolved = true;

            _litShader = Shader.Find("Universal Render Pipeline/Lit");
            _isUrp = _litShader != null;

            if (_litShader == null) _litShader = Shader.Find("Standard");
            if (_litShader == null) _litShader = Shader.Find("Diffuse");

            _unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (_unlitShader == null) _unlitShader = Shader.Find("Unlit/Color");
            if (_unlitShader == null) _unlitShader = _litShader;

            if (_litShader == null)
                Debug.LogError("[MaterialLibrary] No usable shader found. Check the render pipeline setup.");
        }

        private static int HashKey(Color c, float smoothness, float metallic, Color emission, int variant)
        {
            unchecked
            {
                int h = variant;
                h = h * 397 ^ Quantise(c.r);
                h = h * 397 ^ Quantise(c.g);
                h = h * 397 ^ Quantise(c.b);
                h = h * 397 ^ Quantise(c.a);
                h = h * 397 ^ Quantise(smoothness);
                h = h * 397 ^ Quantise(metallic);
                h = h * 397 ^ Quantise(emission.r);
                h = h * 397 ^ Quantise(emission.g);
                h = h * 397 ^ Quantise(emission.b);
                return h;
            }
        }

        private static int Quantise(float v) => Mathf.RoundToInt(v * 255f);

        private static string ColorHex(Color c) => ColorUtility.ToHtmlStringRGB(c);
    }
}
