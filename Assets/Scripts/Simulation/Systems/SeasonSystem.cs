using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Systems
{
    /// <summary>
    /// Keeps calendar season in sync with DayOfYear (formal world state, not UI-only).
    /// </summary>
    public static class SeasonSystem
    {
        public static void UpdateSeason(WorldState world)
        {
            if (world == null)
            {
                return;
            }

            world.SyncSeasonFromDay();
        }

        public static void AdvanceCalendar(WorldState world)
        {
            if (world == null)
            {
                return;
            }

            world.DayOfYear++;
            world.TotalDays++;

            if (world.DayOfYear > SimulationConfig.DaysPerYear)
            {
                world.DayOfYear = 1;
                world.Year++;
                if (world.Regions != null)
                {
                    foreach (var region in world.Regions)
                    {
                        region.LastEvent = $"新年纪事 · {world.Year}";
                    }
                }
            }

            world.SyncSeasonFromDay();
        }
    }
}
