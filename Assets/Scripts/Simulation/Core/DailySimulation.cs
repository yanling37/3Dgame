using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Systems;

namespace DivineWorld.Simulation.Core
{
    /// <summary>
    /// Pure daily simulation pipeline shared by realtime ticks and future macro/fast-forward callers.
    /// Does not advance the calendar; caller decides day advancement.
    /// </summary>
    public static class DailySimulation
    {
        public static void SimulateDay(
            WorldState world,
            RaceDefinition[] races,
            SimulationConfig config,
            System.Random rng)
        {
            if (world == null || races == null || config == null)
            {
                return;
            }

            // 1) Season from current DayOfYear
            SeasonSystem.UpdateSeason(world);
            var season = world.CurrentSeason;

            // 2..7) Per-region pipeline
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

                // 2) Weather (seasonal baseline/range + continuity)
                WeatherSystem.TickDay(region, season, config, rng);

                // 3-5) Resource production, consumption, lifecycle rules
                ResourceSystem.TickDay(region, race, season, config, rng);

                // 6) Population
                PopulationSystem.TickDay(region, race, season, config);

                // 7) Stability / Education / Faith
                SocietySystem.TickDay(region, config, rng);
            }
        }
    }
}
