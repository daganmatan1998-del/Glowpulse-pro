using UnityEngine;

namespace Glowpulse.World.City
{
    /// <summary>
    /// Dimensions of the city. Deliberately small: the brief calls for a dense,
    /// believable few blocks rather than an empty expanse, and every number here
    /// is a metre so the scale stays readable against a 1.8m character.
    /// </summary>
    [CreateAssetMenu(menuName = "Glowpulse/City Settings", fileName = "CitySettings")]
    public sealed class CitySettings : ScriptableObject
    {
        [Header("Grid")]
        [Tooltip("City blocks along X. Three by three is a walkable slice with a centre square.")]
        [Range(2, 6)] public int BlocksX = 3;

        [Range(2, 6)] public int BlocksZ = 3;

        [Tooltip("Side length of one block, in metres.")]
        public float BlockSize = 42f;

        [Tooltip("Carriageway plus both pavements.")]
        public float RoadWidth = 13f;

        [Tooltip("Width of the pavement on each side of the carriageway.")]
        public float SidewalkWidth = 2.6f;

        [Header("Plots")]
        [Tooltip("How deep a building is from its street frontage.")]
        public float PlotDepth = 11f;

        public float MinPlotWidth = 7f;
        public float MaxPlotWidth = 15f;

        [Tooltip("Chance a frontage slot is left empty, opening a gap into the alley.")]
        [Range(0f, 0.4f)] public float GapChance = 0.12f;

        [Header("Heights")]
        public float MinHeight = 7f;
        public float MaxHeight = 22f;

        [Header("Dressing")]
        [Tooltip("Metres between street lights along a kerb.")]
        public float LampSpacing = 17f;

        [Tooltip("Chance a given pavement slot gets a bench, bin, tree or planter.")]
        [Range(0f, 1f)] public float StreetFurnitureDensity = 0.55f;

        [Tooltip("Parked vehicles per road segment.")]
        [Range(0f, 1f)] public float VehicleDensity = 0.45f;

        [Header("Build")]
        [Tooltip("Combine static geometry into one mesh per material. Costs a moment at load, saves draw calls all game.")]
        public bool BatchStaticGeometry = true;

        private static CitySettings _default;

        public static CitySettings Default
        {
            get
            {
                if (_default == null)
                {
                    _default = CreateInstance<CitySettings>();
                    _default.name = "CitySettings (Default)";
                }

                return _default;
            }
        }

        /// <summary>Distance between block origins, including the road between them.</summary>
        public float Stride => BlockSize + RoadWidth;

        /// <summary>Width of the driveable surface, excluding pavements.</summary>
        public float CarriagewayWidth => Mathf.Max(3f, RoadWidth - SidewalkWidth * 2f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _default = null;
    }
}
