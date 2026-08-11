using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Data
{
    /// <summary>
    /// Phase 1 默认世界工厂：教廷区 / 帝国区 / 海。
    /// 以后可换成 JSON / ScriptableObject；调试初始平衡时先改这里。
    ///
    /// Resources 数组下标必须与 ResourceId 枚举一致：
    /// [0]Food [1]Water [2]Timber [3]Ore [4]Faith [5]Knowledge [6]Magic
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
                    LifespanFactor = 1f,      // 越大越长寿 → 日自然死亡率越低
                    FertilityFactor = 1f,     // 乘在人口出生率上
                    GrowthFactor = 1f,        // 乘在粮食产量上
                    StrengthFactor = 1f,      // Phase1 未参与日结算，预留
                    WisdomFactor = 1f,        // Phase1 未参与日结算，预留
                    MagicAffinity = 0.6f,     // 乘在魔力产量上
                    AbilityVariance = 1f,     // Phase1 未用，预留给人物层
                    FaithTendency = 1f,       // 乘在信仰资源产量上
                    KnowledgeTendency = 1f,   // 乘在知识产量上
                    PrefersSea = false        // false → 正常产木/矿；true → 陆地原料几乎不产
                },
                new RaceDefinition
                {
                    Id = RaceId.Merfolk,
                    DisplayName = "人鱼",
                    LifespanFactor = 1.3f,
                    FertilityFactor = 0.75f,  // 生育更慢
                    GrowthFactor = 0.9f,
                    StrengthFactor = 0.9f,
                    WisdomFactor = 1.1f,
                    MagicAffinity = 1.4f,     // 魔力更强
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
                CurrentSeason = SeasonId.Spring,
                SeasonIndex = 0,
                SeasonProgress = 0f,
                Regions = new[]
                {
                    // 教廷：人口中等，信仰高、教育略高，粮水够开局观察「高信仰」走势
                    new RegionState
                    {
                        Id = RegionId.Theocracy,
                        DisplayName = "教廷区",
                        DominantRace = RaceId.Human,
                        Population = 42000f,
                        // Food, Water, Timber, Ore, Faith, Knowledge, Magic
                        Resources = new float[] { 18000, 12000, 6000, 2500, 9000, 3500, 800 },
                        Stability = 0.85f,
                        Education = 0.45f,
                        FaithLevel = 0.8f,
                        DiseasePressure = 0.05f,
                        WeatherFactor = 1f
                    },
                    // 帝国：人口最多，矿产/木材多，信仰低，疫病略高 → 适合测短缺与军事向资源
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
                    // 海：人鱼，水极多、木矿极低、魔力高、人口少
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
