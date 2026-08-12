namespace DivineWorld.Simulation.Data
{
    /// <summary>
    /// Phase 1/2-A default world: 教廷区 / 帝国区 / 海.
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
            var world = new WorldState
            {
                WorldName = "初始大陆与近海",
                Year = 1,
                DayOfYear = 1,
                TotalDays = 0,
                CurrentSeason = SeasonId.Spring,
                RandomSeed = 20260810,
                Regions = new[]
                {
                    new RegionState
                    {
                        Id = RegionId.Theocracy,
                        DisplayName = "教廷区",
                        DominantRace = RaceId.Human,
                        Population = 42000f,
                        // Food, Water, Timber, Ore, Faith, Knowledge, Magic
                        Resources = new float[] { 18000, 9000, 6000, 2500, 9000, 3500, 800 },
                        ProductionCapacity = new float[] { 850, 550, 420, 220, 0, 0, 0 },
                        BaseWaterStorageCapacity = 12000f,
                        LandCarryingCapacity = 55000f,
                        IsSeaRegion = false,
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
                        Resources = new float[] { 26000, 11000, 11000, 7000, 2500, 5000, 400 },
                        ProductionCapacity = new float[] { 1300, 700, 750, 520, 0, 0, 0 },
                        BaseWaterStorageCapacity = 15000f,
                        LandCarryingCapacity = 90000f,
                        IsSeaRegion = false,
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
                        // Water stock high but within sea capacity.
                        Resources = new float[] { 9000, 50000, 800, 1200, 1500, 2800, 3500 },
                        ProductionCapacity = new float[] { 480, 2200, 40, 60, 0, 0, 0 },
                        BaseWaterStorageCapacity = 80000f,
                        LandCarryingCapacity = 28000f,
                        IsSeaRegion = true,
                        Stability = 0.75f,
                        Education = 0.35f,
                        FaithLevel = 0.4f,
                        DiseasePressure = 0.04f,
                        WeatherFactor = 0.95f
                    }
                }
            };

            world.SyncSeasonFromDay();
            return world;
        }
    }
}
