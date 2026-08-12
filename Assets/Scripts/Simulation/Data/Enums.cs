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
        Magic = 6 // Mana (display name); keep index stable
    }

    public enum RaceId
    {
        Human = 0,
        Merfolk = 1
    }

    public enum RegionId
    {
        Theocracy = 0,
        Empire = 1,
        Sea = 2
    }

    public enum SeasonId
    {
        Spring = 0,
        Summer = 1,
        Autumn = 2,
        Winter = 3
    }

    public enum SimEventType
    {
        None = 0,
        FoodShortage = 1,
        DiseaseOutbreak = 2,
        LowStability = 3,
        HighStability = 4,
        YearTurn = 5,
        NaturalDisaster = 6
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

    public enum SimEventScope
    {
        Regional = 0,
        Global = 1
    }
}
