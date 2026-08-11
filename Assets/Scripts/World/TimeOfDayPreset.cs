namespace Glowpulse.World
{
    /// <summary>
    /// The atmospheres a scene can be lit with.
    ///
    /// Its own file, away from the lighting component, because stage data names a
    /// time of day and stage data has to stay free of anything that needs a
    /// renderer - that is what lets the whole progression be checked in a test.
    /// </summary>
    public enum TimeOfDayPreset
    {
        Noon = 0,
        GoldenHour = 1,
        Dusk = 2,
        Night = 3
    }
}
