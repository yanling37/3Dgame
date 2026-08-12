namespace DivineWorld.Simulation.Data
{
    public enum ResourceId
    {
        Food = 0,
        Water = 1,
        Timber = 2,
        Ore = 3,
        Faith = 4,
        Knowledge = 5,
        Magic = 6
    }

    public enum RaceId
    {
        Human = 0,
        Merfolk = 1
    }

    public enum RegionId
    {
        Theocracy = 0, // 教廷区
        Empire = 1,    // 帝国区
        Sea = 2        // 海
    }

    /// <summary>
    /// Calendar season. Year is fixed at 360 days, 90 days each.
    /// </summary>
    public enum SeasonId
    {
        Spring = 0,
        Summer = 1,
        Autumn = 2,
        Winter = 3
    }

    /// <summary>
    /// Resource lifecycle rules used by the data-driven resource engine.
    /// </summary>
    public enum ResourceLifecycle
    {
        /// <summary>Stock + production - consumption - spoilage (e.g. Food).</summary>
        Perishable = 0,
        /// <summary>Stock + production - consumption, no time spoilage (e.g. Magic).</summary>
        Persistent = 1,
        /// <summary>Stock capped by region capacity (e.g. Water).</summary>
        CapacityLimited = 2
    }
}
