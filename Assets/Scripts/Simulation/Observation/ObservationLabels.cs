using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// Display labels copied for the observation layer. Not event evaluation / season math.
    /// </summary>
    public static class ObservationLabels
    {
        public const string UiVersion = "P2-B · Observation v0.2";
        public const string NoEvent = "平静 / 无事件";

        public static string SeasonName(SeasonId season)
        {
            switch (season)
            {
                case SeasonId.Spring: return "Spring";
                case SeasonId.Summer: return "Summer";
                case SeasonId.Autumn: return "Autumn";
                case SeasonId.Winter: return "Winter";
                default: return season.ToString();
            }
        }

        public static string EventDisplayName(SimEventType type)
        {
            switch (type)
            {
                case SimEventType.FoodShortage: return "粮食短缺";
                case SimEventType.DiseaseOutbreak: return "疫病爆发";
                case SimEventType.LowStability: return "动荡";
                case SimEventType.HighStability: return "升平";
                case SimEventType.YearTurn: return "新年";
                case SimEventType.NaturalDisaster: return "天灾";
                case SimEventType.None: return NoEvent;
                default: return type.ToString();
            }
        }
    }
}
