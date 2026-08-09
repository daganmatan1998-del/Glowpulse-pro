namespace Glowpulse.Core
{
    /// <summary>Who a character fights for. Used to filter hits and target search.</summary>
    public enum Faction
    {
        Neutral = 0,
        Player = 1,
        Hostile = 2,
        Civilian = 3
    }

    public static class FactionRules
    {
        public static bool IsHostileTo(this Faction self, Faction other)
        {
            if (self == Faction.Neutral || other == Faction.Neutral) return false;
            if (self == other) return false;
            if (self == Faction.Civilian || other == Faction.Civilian) return false;
            return true;
        }
    }
}
