using UnityEngine;

namespace Glowpulse.VFX
{
    /// <summary>
    /// Small procedurally generated textures for particle effects, so the game
    /// has decent-looking sparks and smoke with no imported art. They are tiny,
    /// generated once, and shared by every effect.
    /// </summary>
    public static class EffectTextures
    {
        private static Texture2D _softDot;
        private static Texture2D _spark;
        private static Texture2D _ring;
        private static Texture2D _smoke;

        /// <summary>Round, soft-edged blob. The workhorse particle shape.</summary>
        public static Texture2D SoftDot => _softDot != null ? _softDot : _softDot = BuildSoftDot();

        /// <summary>Elongated streak, for fast-moving sparks.</summary>
        public static Texture2D Spark => _spark != null ? _spark : _spark = BuildSpark();

        /// <summary>Hollow ring, used for the shockwave on heavy impacts.</summary>
        public static Texture2D Ring => _ring != null ? _ring : _ring = BuildRing();

        /// <summary>Cloudy blob for dust and smoke.</summary>
        public static Texture2D Smoke => _smoke != null ? _smoke : _smoke = BuildSmoke();

        private static Texture2D BuildSoftDot()
        {
            const int size = 32;
            return Build("FX_SoftDot", size, (u, v) =>
            {
                float d = Mathf.Sqrt(u * u + v * v) * 2f;
                float a = Mathf.Clamp01(1f - d);
                return a * a;
            });
        }

        private static Texture2D BuildSpark()
        {
            const int size = 32;
            return Build("FX_Spark", size, (u, v) =>
            {
                // Squash vertically so the quad reads as a streak.
                float d = Mathf.Sqrt(u * u * 0.16f + v * v) * 2f;
                float a = Mathf.Clamp01(1f - d);
                return a * a * a;
            });
        }

        private static Texture2D BuildRing()
        {
            const int size = 64;
            return Build("FX_Ring", size, (u, v) =>
            {
                float d = Mathf.Sqrt(u * u + v * v) * 2f;
                float band = 1f - Mathf.Clamp01(Mathf.Abs(d - 0.78f) / 0.2f);
                return band * band;
            });
        }

        private static Texture2D BuildSmoke()
        {
            const int size = 48;
            return Build("FX_Smoke", size, (u, v) =>
            {
                float d = Mathf.Sqrt(u * u + v * v) * 2f;
                float falloff = Mathf.Clamp01(1f - d);

                // A couple of octaves of noise break up the perfect circle.
                float n = Mathf.PerlinNoise((u + 0.5f) * 6f, (v + 0.5f) * 6f) * 0.65f
                          + Mathf.PerlinNoise((u + 0.5f) * 13f, (v + 0.5f) * 13f) * 0.35f;

                return falloff * falloff * Mathf.Clamp01(n * 1.4f);
            });
        }

        private static Texture2D Build(string name, int size, System.Func<float, float, float> alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size - 0.5f;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size - 0.5f;
                    byte a = (byte)(Mathf.Clamp01(alpha(u, v)) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _softDot = _spark = _ring = _smoke = null;
        }
    }
}
