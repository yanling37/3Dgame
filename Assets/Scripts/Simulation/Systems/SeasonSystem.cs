using DivineWorld.Simulation.Data;
using UnityEngine;

namespace DivineWorld.Simulation.Systems
{
    /// <summary>
    /// Formal season clock: 360-day year, 4×90-day seasons.
    /// Season drives baselines; WeatherFactor wanders inside seasonal range.
    /// </summary>
    public static class SeasonSystem
    {
        public static void SyncFromCalendar(WorldState world)
        {
            int dayIndex = Mathf.Clamp(world.DayOfYear - 1, 0, WorldState.DaysPerYear - 1);
            world.SeasonIndex = dayIndex / WorldState.DaysPerSeason;
            world.CurrentSeason = (SeasonId)world.SeasonIndex;
            int dayInSeason = dayIndex % WorldState.DaysPerSeason;
            world.SeasonProgress = dayInSeason / (float)WorldState.DaysPerSeason;
        }

        public static string DisplayName(SeasonId season)
        {
            switch (season)
            {
                case SeasonId.Spring: return "春";
                case SeasonId.Summer: return "夏";
                case SeasonId.Autumn: return "秋";
                case SeasonId.Winter: return "冬";
                default: return season.ToString();
            }
        }

        public static void GetWeatherRange(SeasonId season, out float min, out float max)
        {
            switch (season)
            {
                case SeasonId.Spring:
                    min = 0.85f; max = 1.2f; break;
                case SeasonId.Summer:
                    min = 0.95f; max = 1.35f; break;
                case SeasonId.Autumn:
                    min = 0.8f; max = 1.15f; break;
                case SeasonId.Winter:
                    min = 0.55f; max = 0.95f; break;
                default:
                    min = 0.6f; max = 1.3f; break;
            }
        }

        public static float WeatherBaseline(SeasonId season)
        {
            GetWeatherRange(season, out float min, out float max);
            return (min + max) * 0.5f;
        }

        public static float BirthModifier(SeasonId season)
        {
            switch (season)
            {
                case SeasonId.Spring: return 1.15f;
                case SeasonId.Summer: return 1.05f;
                case SeasonId.Autumn: return 0.95f;
                case SeasonId.Winter: return 0.8f;
                default: return 1f;
            }
        }

        public static float DeathModifier(SeasonId season)
        {
            switch (season)
            {
                case SeasonId.Spring: return 0.95f;
                case SeasonId.Summer: return 1.0f;
                case SeasonId.Autumn: return 1.05f;
                case SeasonId.Winter: return 1.25f;
                default: return 1f;
            }
        }

        public static float DiseaseModifier(SeasonId season)
        {
            switch (season)
            {
                case SeasonId.Spring: return 0.9f;
                case SeasonId.Summer: return 1.15f;
                case SeasonId.Autumn: return 1.0f;
                case SeasonId.Winter: return 1.2f;
                default: return 1f;
            }
        }

        public static void TickWeather(RegionState region, SeasonId season, System.Random rng)
        {
            GetWeatherRange(season, out float min, out float max);
            region.WeatherFactor = Mathf.Clamp(
                region.WeatherFactor + ((float)rng.NextDouble() - 0.5f) * 0.02f,
                min,
                max);
        }
    }
}
