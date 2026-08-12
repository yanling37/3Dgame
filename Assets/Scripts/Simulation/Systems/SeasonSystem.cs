using DivineWorld.Simulation.Data;
using UnityEngine;

namespace DivineWorld.Simulation.Systems
{
    /// <summary>
    /// Formal season clock: 360-day year, 4×90-day seasons driven by DayOfYear.
    /// </summary>
    public static class SeasonSystem
    {
        /// <summary>Alias used by DailySimulation — sync season from calendar day.</summary>
        public static void UpdateSeason(WorldState world) => SyncFromCalendar(world);

        public static void SyncFromCalendar(WorldState world)
        {
            if (world == null)
            {
                return;
            }

            world.SyncSeasonFromDay();
        }

        /// <summary>
        /// Advances one calendar day. Returns true when the year rolls over.
        /// Does not apply year-turn events — caller should invoke EventSystem.ApplyYearTurn.
        /// </summary>
        public static bool AdvanceCalendar(WorldState world)
        {
            if (world == null)
            {
                return false;
            }

            world.DayOfYear++;
            world.TotalDays++;

            bool yearTurned = false;
            if (world.DayOfYear > SimulationConfig.DaysPerYear)
            {
                world.DayOfYear = 1;
                world.Year++;
                yearTurned = true;
            }

            world.SyncSeasonFromDay();
            return yearTurned;
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
            var config = SimulationConfig.CreateDefault();
            config.GetWeatherRange(season, out _, out min, out max);
        }

        public static float WeatherBaseline(SeasonId season)
        {
            var config = SimulationConfig.CreateDefault();
            config.GetWeatherRange(season, out float baseline, out _, out _);
            return baseline;
        }

        public static float DeathModifier(SeasonId season)
        {
            return SimulationConfig.CreateDefault().DeathModifier(season);
        }

        public static float DiseaseModifier(SeasonId season)
        {
            return SimulationConfig.CreateDefault().DiseaseModifier(season);
        }
    }
}
