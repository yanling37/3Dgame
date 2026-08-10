using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Data
{
    /// <summary>
    /// Phase 1 default world: 教廷区 / 帝国区 / 海.
    /// Data-driven entry point; later replace with JSON / ScriptableObject.
    /// </summary>
    public static class DefaultWorldFactory
    {
        public static RaceDefinition[] CreateRaces()
        {
            return new[]
            {
                new RaceDefinition
                {
                    Id = RaceId.Human,
                    DisplayName = "人类",
                    LifespanFactor = 1f,
                    FertilityFactor = 1f,
                    GrowthFactor = 1f,
                    StrengthFactor = 1f,
                    WisdomFactor = 1f,
                    MagicAffinity = 0.6f,
                    AbilityVariance = 1f,
                    FaithTendency = 1f,
                    KnowledgeTendency = 1f,
                    PrefersSea = false
                },
                new RaceDefinition
                {
                    Id = RaceId.Merfolk,
                    DisplayName = "人鱼",
                    LifespanFactor = 1.3f,
                    FertilityFactor = 0.75f,
                    GrowthFactor = 0.9f,
                    StrengthFactor = 0.9f,
                    WisdomFactor = 1.1f,
                    MagicAffinity = 1.4f,
                    AbilityVariance = 0.8f,
                    FaithTendency = 0.8f,
                    KnowledgeTendency = 1.1f,
                    PrefersSea = true
                }
            };
        }

        public static WorldState CreateWorld()
        {
            return new WorldState
            {
                WorldName = "初始大陆与近海",
                Year = 1,
                DayOfYear = 1,
                TotalDays = 0,
                Regions = new[]
                {
                    new RegionState
                    {
                        Id = RegionId.Theocracy,
                        DisplayName = "教廷区",
                        DominantRace = RaceId.Human,
                        Population = 42000f,
                        Resources = new float[] { 18000, 12000, 6000, 2500, 9000, 3500, 800 },
                        Stability = 0.85f,
                        Education = 0.45f,
                        FaithLevel = 0.8f,
                        DiseasePressure = 0.05f,
                        WeatherFactor = 1f
                    },
                    new RegionState
                    {
                        Id = RegionId.Empire,
                        DisplayName = "帝国区",
                        DominantRace = RaceId.Human,
                        Population = 68000f,
                        Resources = new float[] { 26000, 14000, 11000, 7000, 2500, 5000, 400 },
                        Stability = 0.7f,
                        Education = 0.4f,
                        FaithLevel = 0.35f,
                        DiseasePressure = 0.08f,
                        WeatherFactor = 1f
                    },
                    new RegionState
                    {
                        Id = RegionId.Sea,
                        DisplayName = "海",
                        DominantRace = RaceId.Merfolk,
                        Population = 18000f,
                        Resources = new float[] { 9000, 50000, 800, 1200, 1500, 2800, 3500 },
                        Stability = 0.75f,
                        Education = 0.35f,
                        FaithLevel = 0.4f,
                        DiseasePressure = 0.04f,
                        WeatherFactor = 1f
                    }
                }
            };
        }
    }
}
