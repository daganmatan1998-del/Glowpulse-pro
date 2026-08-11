using UnityEngine;

namespace Glowpulse.Core.Characters
{
    /// <summary>
    /// Look and proportions of a placeholder character. Keeping this as plain
    /// data means enemy variants, civilians and the player all come out of the
    /// same builder with a different palette rather than a different code path.
    /// </summary>
    [System.Serializable]
    public struct CharacterStyle
    {
        public float Height;
        public float Build;        // 1 = average, >1 heavier, <1 slimmer
        public Color Skin;
        public Color Torso;
        public Color Legs;
        public Color Shoes;
        public Color Hair;
        public Color Accent;       // trim, jacket detail, gloves
        public float AccentGlow;   // 0 = matte, >0 emissive rim (used for enemy tells)

        /// <summary>
        /// Bare chest. The rig swaps the torso to skin and adds the muscle masses
        /// that a shirt would otherwise hide - without them a shirtless character
        /// just reads as a flesh-coloured box.
        /// </summary>
        public bool Shirtless;

        /// <summary>Ink colour for the back piece. Alpha at zero means no tattoo.</summary>
        public Color Tattoo;

        public static CharacterStyle Player()
        {
            return new CharacterStyle
            {
                Height = 1.84f,

                // Heavier than anything on the street except a bruiser. The build
                // figure drives shoulder width, limb thickness and the muscle
                // masses together, so raising it thickens the whole silhouette
                // rather than just widening the chest box.
                Build = 1.26f,

                Skin = Hex("C08A5E"),
                Torso = Hex("C08A5E"),   // bare chest: the torso is skin
                Legs = Hex("1A2129"),
                Shoes = Hex("14161A"),
                Hair = Hex("24201E"),
                Accent = Hex("46C2C8"),
                AccentGlow = 1.1f,
                Shirtless = true,
                Tattoo = Hex("14212B")
            };
        }

        public static CharacterStyle Thug()
        {
            return new CharacterStyle
            {
                Height = 1.78f,
                Build = 1.05f,
                Skin = Hex("B98A63"),
                Torso = Hex("59323A"),
                Legs = Hex("2B2A31"),
                Shoes = Hex("1A1A1D"),
                Hair = Hex("1B1614"),
                Accent = Hex("D8563F"),
                AccentGlow = 0.8f
            };
        }

        public static CharacterStyle Bruiser()
        {
            return new CharacterStyle
            {
                Height = 2.02f,
                Build = 1.38f,
                Skin = Hex("A97B58"),
                Torso = Hex("3E3326"),
                Legs = Hex("262119"),
                Shoes = Hex("17140F"),
                Hair = Hex("15110E"),
                Accent = Hex("E0913A"),
                AccentGlow = 1.0f
            };
        }

        public static CharacterStyle Runner()
        {
            return new CharacterStyle
            {
                Height = 1.72f,
                Build = 0.85f,
                Skin = Hex("C08F6A"),
                Torso = Hex("2C3F35"),
                Legs = Hex("1D2622"),
                Shoes = Hex("121614"),
                Hair = Hex("2A211C"),
                Accent = Hex("7FD858"),
                AccentGlow = 1.2f
            };
        }

        /// <summary>Deterministic civilian palette so a crowd looks varied but stable across runs.</summary>
        public static CharacterStyle Civilian(int seed)
        {
            var rng = new System.Random(seed);
            float NextF(float a, float b) => Mathf.Lerp(a, b, (float)rng.NextDouble());

            Color[] skins = { Hex("F0C8A0"), Hex("D9A578"), Hex("B07C4F"), Hex("8A5A38"), Hex("5E3A22") };
            Color[] tops = { Hex("6B7B8C"), Hex("8C6B6B"), Hex("4E5D52"), Hex("7A7285"), Hex("A8A093"), Hex("3B4A5A"), Hex("94684F") };
            Color[] bottoms = { Hex("343A42"), Hex("4A4038"), Hex("2C3038"), Hex("5A5348") };
            Color[] hairs = { Hex("1B1512"), Hex("3A2A1E"), Hex("6B5238"), Hex("8F8F92"), Hex("2A2A2E") };

            return new CharacterStyle
            {
                Height = NextF(1.62f, 1.88f),
                Build = NextF(0.86f, 1.16f),
                Skin = skins[rng.Next(skins.Length)],
                Torso = tops[rng.Next(tops.Length)],
                Legs = bottoms[rng.Next(bottoms.Length)],
                Shoes = Hex("1C1A18"),
                Hair = hairs[rng.Next(hairs.Length)],
                Accent = tops[rng.Next(tops.Length)],
                AccentGlow = 0f
            };
        }

        private static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out Color c) ? c : Color.magenta;
        }
    }
}
