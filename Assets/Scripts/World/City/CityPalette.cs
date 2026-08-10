using System.Collections.Generic;
using Glowpulse.Core.Rendering;
using UnityEngine;

namespace Glowpulse.World.City
{
    /// <summary>
    /// The city's materials, resolved once and shared.
    ///
    /// Sharing matters twice over: it is what lets the geometry batcher weld
    /// pieces together, and it keeps the whole city on a deliberately small
    /// palette so it reads as one place rather than a colour swatch.
    /// </summary>
    public sealed class CityPalette
    {
        public Material Road;
        public Material RoadMarking;
        public Material Sidewalk;
        public Material Kerb;
        public Material Metal;
        public Material DarkMetal;
        public Material Glass;
        public Material WindowLit;
        public Material Neon;
        public Material Foliage;
        public Material Trunk;
        public Material Concrete;
        public Material Rubber;

        private readonly List<Material> _facades = new List<Material>(8);
        private readonly List<Material> _trims = new List<Material>(6);
        private readonly List<Material> _shopfronts = new List<Material>(6);
        private readonly List<Material> _vehicles = new List<Material>(6);

        public static CityPalette Build()
        {
            var p = new CityPalette();

            p.Road = Lit("#26282C", 0.08f);
            p.RoadMarking = Lit("#C6B47C", 0.05f);
            p.Sidewalk = Lit("#4B4945", 0.06f);
            p.Kerb = Lit("#5E5B54", 0.08f);
            p.Concrete = Lit("#6A665E", 0.07f);
            p.Metal = Lit("#6E7378", 0.55f, 0.75f);
            p.DarkMetal = Lit("#33373C", 0.4f, 0.6f);
            p.Rubber = Lit("#15161A", 0.1f);
            p.Glass = Lit("#2A3843", 0.85f, 0.15f);
            p.Trunk = Lit("#4A3A2C", 0.1f);
            p.Foliage = Lit("#3E5233", 0.12f);

            // The two lights in the city: warm windows and cold shop neon.
            p.WindowLit = MaterialLibrary.Emissive(Hex("#FFD9A0"), 2.1f, 0.4f);
            p.Neon = MaterialLibrary.Emissive(Hex("#46C2C8"), 2.6f, 0.5f);

            // Facades stay within a narrow warm-grey band so height and window
            // pattern do the work of telling buildings apart, not hue.
            AddAll(p._facades, "#585049", "#4C4A48", "#63564C", "#4A4E52", "#6B5F55", "#43484D");
            AddAll(p._trims, "#8A8175", "#3C4045", "#7A6A5C", "#575C61");
            AddAll(p._shopfronts, "#7A4038", "#3F5A55", "#6B5730", "#43506B", "#5C3D5A");
            AddAll(p._vehicles, "#5A6470", "#6B4B44", "#41505A", "#6E6353", "#495159", "#7A7268");

            return p;
        }

        public Material Facade(int seed) => Pick(_facades, seed);
        public Material Trim(int seed) => Pick(_trims, seed);
        public Material Shopfront(int seed) => Pick(_shopfronts, seed);
        public Material Vehicle(int seed) => Pick(_vehicles, seed);

        private static Material Pick(List<Material> list, int seed)
        {
            if (list.Count == 0) return null;
            int index = Mathf.Abs(seed) % list.Count;
            return list[index];
        }

        private static void AddAll(List<Material> list, params string[] hexes)
        {
            foreach (string hex in hexes) list.Add(Lit(hex, 0.08f));
        }

        private static Material Lit(string hex, float smoothness, float metallic = 0f)
        {
            return MaterialLibrary.Lit(Hex(hex), smoothness, metallic);
        }

        private static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
        }
    }
}
