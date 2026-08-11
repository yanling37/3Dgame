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
}
