using UnityEngine;

namespace Glowpulse.Audio
{
    /// <summary>
    /// Synthesises the game's placeholder sound effects at runtime.
    ///
    /// A beat 'em up is unplayable without audio feedback, and waiting for
    /// recorded assets would mean tuning combat feel deaf. These are simple
    /// noise-and-sine constructions, but they land on the right frame with the
    /// right weight, which is what the tuning actually needs. Any of them can be
    /// replaced by a real clip through <see cref="AudioManager.OverrideClip"/>
    /// without touching gameplay code.
    /// </summary>
    public static class ProceduralAudio
    {
        private const int SampleRate = 44100;

        private static System.Random _rng = new System.Random(9182736);

        /// <summary>Flat, meaty impact: a noise crack over a low body thump.</summary>
        public static AudioClip Punch()
        {
            return Build("sfx_punch", 0.16f, (t, d) =>
            {
                float env = Decay(t, d, 22f);
                float crack = Noise() * env * 0.55f;
                float body = Mathf.Sin(2f * Mathf.PI * 92f * t) * Decay(t, d, 14f) * 0.7f;
                return crack + body;
            });
        }

        /// <summary>Heavier and slower than a punch, with more low end.</summary>
        public static AudioClip Kick()
        {
            return Build("sfx_kick", 0.22f, (t, d) =>
            {
                float env = Decay(t, d, 16f);
                float crack = LowPassNoise(t, 0.35f) * env * 0.5f;
                // Falling pitch is what makes a low thump sound like weight.
                float body = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(74f, 48f, t / d) * t)
                             * Decay(t, d, 10f) * 0.85f;
                return crack + body;
            });
        }

        /// <summary>The big one: deep, long, with a snap on the front.</summary>
        public static AudioClip HeavyHit()
        {
            return Build("sfx_heavy", 0.42f, (t, d) =>
            {
                float snap = Noise() * Decay(t, d, 40f) * 0.5f;
                float body = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(62f, 34f, t / d) * t)
                             * Decay(t, d, 7f) * 1.0f;
                float sub = Mathf.Sin(2f * Mathf.PI * 41f * t) * Decay(t, d, 5f) * 0.5f;
                return snap + body + sub;
            });
        }

        /// <summary>Dull, damped thud - the blow was absorbed, not landed.</summary>
        public static AudioClip Block()
        {
            return Build("sfx_block", 0.14f, (t, d) =>
            {
                float thud = LowPassNoise(t, 0.15f) * Decay(t, d, 30f) * 0.65f;
                float tone = Mathf.Sin(2f * Mathf.PI * 165f * t) * Decay(t, d, 26f) * 0.35f;
                return thud + tone;
            });
        }

        /// <summary>Bright inharmonic ring - deliberately the most distinctive sound in the game.</summary>
        public static AudioClip Parry()
        {
            return Build("sfx_parry", 0.5f, (t, d) =>
            {
                // Inharmonic partials are what make something sound metallic
                // rather than musical.
                float a = Mathf.Sin(2f * Mathf.PI * 1180f * t) * Decay(t, d, 7f);
                float b = Mathf.Sin(2f * Mathf.PI * 1847f * t) * Decay(t, d, 9f) * 0.7f;
                float c = Mathf.Sin(2f * Mathf.PI * 2593f * t) * Decay(t, d, 12f) * 0.45f;
                float transient = Noise() * Decay(t, d, 90f) * 0.4f;
                return (a + b + c) * 0.36f + transient;
            });
        }

        /// <summary>Air moving past a limb. Rises then falls.</summary>
        public static AudioClip Whoosh(float pitch = 1f)
        {
            return Build($"sfx_whoosh_{pitch:F2}", 0.26f, (t, d) =>
            {
                float u = t / d;
                // Bell-shaped envelope: the swing passes the listener.
                float env = Mathf.Sin(u * Mathf.PI);
                return LowPassNoise(t, Mathf.Lerp(0.06f, 0.5f, env) * pitch) * env * 0.5f;
            });
        }

        /// <summary>Short vocal grunt when a character is hit.</summary>
        public static AudioClip Grunt(float pitch = 1f)
        {
            return Build($"sfx_grunt_{pitch:F2}", 0.3f, (t, d) =>
            {
                float f = 152f * pitch * Mathf.Lerp(1.1f, 0.75f, t / d);
                // Two harmonics plus breath noise reads as a voice.
                float voice = Mathf.Sin(2f * Mathf.PI * f * t) * 0.6f
                              + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.25f;
                float breath = LowPassNoise(t, 0.2f) * 0.3f;
                return (voice + breath) * Decay(t, d, 9f) * 0.7f;
            });
        }

        /// <summary>Longer, falling grunt for a death.</summary>
        public static AudioClip Death(float pitch = 1f)
        {
            return Build($"sfx_death_{pitch:F2}", 0.75f, (t, d) =>
            {
                float f = 140f * pitch * Mathf.Lerp(1f, 0.5f, Mathf.Sqrt(t / d));
                float voice = Mathf.Sin(2f * Mathf.PI * f * t) * 0.55f
                              + Mathf.Sin(2f * Mathf.PI * f * 1.5f * t) * 0.2f;
                float breath = LowPassNoise(t, 0.14f) * 0.35f;
                return (voice + breath) * Decay(t, d, 3.6f) * 0.6f;
            });
        }

        /// <summary>Body hitting the ground.</summary>
        public static AudioClip BodyFall()
        {
            return Build("sfx_bodyfall", 0.34f, (t, d) =>
            {
                float thud = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(70f, 38f, t / d) * t)
                             * Decay(t, d, 11f) * 0.9f;
                float scuff = LowPassNoise(t, 0.25f) * Decay(t, d, 18f) * 0.4f;
                return thud + scuff;
            });
        }

        public static AudioClip Footstep(float pitch = 1f)
        {
            return Build($"sfx_step_{pitch:F2}", 0.11f, (t, d) =>
            {
                return LowPassNoise(t, 0.3f * pitch) * Decay(t, d, 42f) * 0.4f;
            });
        }

        public static AudioClip Landing()
        {
            return Build("sfx_land", 0.2f, (t, d) =>
            {
                float thud = Mathf.Sin(2f * Mathf.PI * 88f * t) * Decay(t, d, 18f) * 0.7f;
                float scuff = LowPassNoise(t, 0.4f) * Decay(t, d, 30f) * 0.45f;
                return thud + scuff;
            });
        }

        /// <summary>Guard shattering: a metallic crash with a long tail.</summary>
        public static AudioClip GuardBreak()
        {
            return Build("sfx_guardbreak", 0.6f, (t, d) =>
            {
                float crash = Noise() * Decay(t, d, 6f) * 0.4f;
                float ring = (Mathf.Sin(2f * Mathf.PI * 620f * t) + Mathf.Sin(2f * Mathf.PI * 941f * t))
                             * Decay(t, d, 5f) * 0.22f;
                return crash + ring;
            });
        }

        /// <summary>Soft UI blip.</summary>
        public static AudioClip UiClick(float frequency = 880f)
        {
            return Build($"sfx_ui_{frequency:F0}", 0.09f, (t, d) =>
            {
                return Mathf.Sin(2f * Mathf.PI * frequency * t) * Decay(t, d, 36f) * 0.35f;
            });
        }

        /// <summary>Rising confirmation chime, e.g. an objective completing.</summary>
        public static AudioClip Chime()
        {
            return Build("sfx_chime", 0.55f, (t, d) =>
            {
                float a = Mathf.Sin(2f * Mathf.PI * 784f * t) * Decay(t, d, 6f);
                float b = Mathf.Sin(2f * Mathf.PI * 1175f * t) * Decay(Mathf.Max(0f, t - 0.09f), d, 6f);
                return (a + b) * 0.28f;
            });
        }

        /// <summary>
        /// Looping city ambience: filtered noise with a slow swell, plus a low
        /// rumble. Stands in for a recorded city bed.
        /// </summary>
        public static AudioClip CityAmbience(float seconds = 8f)
        {
            int count = Mathf.RoundToInt(SampleRate * seconds);
            var data = new float[count];
            float lp = 0f, lp2 = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;

                // Two cascaded one-pole filters turn white noise into a distant hiss.
                float n = (float)(_rng.NextDouble() * 2.0 - 1.0);
                lp += (n - lp) * 0.02f;
                lp2 += (lp - lp2) * 0.05f;

                float swell = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.07f * t)
                                   * Mathf.Sin(2f * Mathf.PI * 0.031f * t);
                float rumble = Mathf.Sin(2f * Mathf.PI * 47f * t) * 0.03f
                               + Mathf.Sin(2f * Mathf.PI * 61f * t) * 0.02f;

                data[i] = lp2 * 2.4f * swell + rumble;
            }

            CrossfadeLoop(data, Mathf.RoundToInt(SampleRate * 0.4f));
            return FromSamples("amb_city", data);
        }

        // ---- synthesis helpers -------------------------------------------------------

        private static AudioClip Build(string name, float duration, System.Func<float, float, float> sample)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                data[i] = Mathf.Clamp(sample(t, duration), -1f, 1f);
            }

            // A short fade in and out stops the click that a discontinuity at the
            // clip boundary would otherwise produce.
            FadeEdges(data, Mathf.RoundToInt(SampleRate * 0.002f));
            return FromSamples(name, data);
        }

        private static AudioClip FromSamples(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Decay(float t, float duration, float rate)
        {
            if (t < 0f) return 0f;
            return Mathf.Exp(-rate * t);
        }

        private static float Noise() => (float)(_rng.NextDouble() * 2.0 - 1.0);

        private static float _lowPassState;
        private static float _lastLowPassTime = -1f;

        /// <summary>One-pole low-passed noise. <paramref name="cutoff"/> is 0..1.</summary>
        private static float LowPassNoise(float t, float cutoff)
        {
            // Reset the filter whenever a new clip starts, so clips do not bleed
            // filter state into one another.
            if (t < _lastLowPassTime) _lowPassState = 0f;
            _lastLowPassTime = t;

            _lowPassState += (Noise() - _lowPassState) * Mathf.Clamp(cutoff, 0.005f, 1f);
            return _lowPassState;
        }

        private static void FadeEdges(float[] data, int fadeSamples)
        {
            int n = Mathf.Min(fadeSamples, data.Length / 2);
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                data[i] *= k;
                data[data.Length - 1 - i] *= k;
            }
        }

        /// <summary>Blends the tail into the head so a looping clip has no seam.</summary>
        private static void CrossfadeLoop(float[] data, int fadeSamples)
        {
            int n = Mathf.Min(fadeSamples, data.Length / 4);
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                int tail = data.Length - n + i;
                data[i] = Mathf.Lerp(data[tail], data[i], k);
            }

            // The tail is now duplicated at the head, so silence it.
            for (int i = 0; i < n; i++)
                data[data.Length - n + i] *= 1f - i / (float)n;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _rng = new System.Random(9182736);
            _lowPassState = 0f;
            _lastLowPassTime = -1f;
        }
    }
}
