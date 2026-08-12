using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Systems;

namespace DivineWorld.Simulation.Core
{
    /// <summary>
    /// Pure daily simulation pipeline shared by realtime ticks and FastForward rate estimators.
    /// </summary>
    public static class DailySimulation
    {
        public static void SimulateDay(
            WorldState world,
            RaceDefinition[] races,
            SimulationConfig config,
            System.Random rng)
        {
            if (world == null || races == null || config == null || world.HaltedOnNumericError)
            {
                return;
            }

            SeasonSystem.UpdateSeason(world);
            var season = world.CurrentSeason;

            if (world.Regions == null)
            {
                return;
            }

            foreach (var region in world.Regions)
            {
                var race = RegionLookup.FindRace(races, region.DominantRace);
                if (race == null)
                {
                    continue;
                }

                WeatherSystem.TickDay(region, season, config, rng);
                ResourceSystem.TickDay(world, region, race, season, config, rng);
                if (world.HaltedOnNumericError)
                {
                    return;
                }

                PopulationSystem.TickDay(world, region, race, season, config);
                if (world.HaltedOnNumericError)
                {
                    return;
                }

                SocietySystem.TickDay(region, config, rng);
            }

            SeasonSystem.AdvanceCalendar(world);
            if (world.DayOfYear == 1 && world.TotalDays > 0)
            {
                // Just rolled into a new year (AdvanceCalendar already incremented Year).
                EventSystem.ApplyYearTurn(world);
            }

            EventSystem.EvaluateAndApply(world, config);
        }
    }
}
