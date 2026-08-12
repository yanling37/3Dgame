using System.Collections.Generic;
using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Systems
{
    public static class RegionLookup
    {
        public static RaceDefinition FindRace(IReadOnlyList<RaceDefinition> races, RaceId id)
        {
            if (races == null || races.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < races.Count; i++)
            {
                if (races[i].Id == id)
                {
                    return races[i];
                }
            }

            return races[0];
        }

        public static RegionState FindRegion(IReadOnlyList<RegionState> regions, RegionId id)
        {
            if (regions == null)
            {
                return null;
            }

            for (int i = 0; i < regions.Count; i++)
            {
                if (regions[i].Id == id)
                {
                    return regions[i];
                }
            }

            return null;
        }
    }
}
