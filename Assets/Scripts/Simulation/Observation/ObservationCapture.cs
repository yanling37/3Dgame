using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// Copies Simulation State into an observation snapshot.
    /// Does not run population/resource formulas.
    /// </summary>
    public static class ObservationCapture
    {
        public static WorldObservationSnapshot FromWorld(WorldState state)
        {
            if (state == null)
            {
                return WorldObservationSnapshot.Empty;
            }

            var source = state.Regions;
            int count = source != null ? source.Length : 0;
            var regions = new RegionObservationSnapshot[count];
            for (int i = 0; i < count; i++)
            {
                regions[i] = FromRegion(source[i]);
            }

            return new WorldObservationSnapshot(
                state.Year,
                state.DayOfYear,
                state.TotalDays,
                state.CurrentSeason,
                state.HaltedOnNumericError,
                regions);
        }

        public static RegionObservationSnapshot FromRegion(RegionState region)
        {
            if (region == null)
            {
                return new RegionObservationSnapshot(
                    RegionId.Theocracy,
                    string.Empty,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    string.Empty);
            }

            return new RegionObservationSnapshot(
                region.Id,
                region.DisplayName,
                region.Population,
                region.PopulationDelta,
                region.Get(ResourceId.Food),
                region.Get(ResourceId.Water),
                region.Stability,
                region.LastCarryingCapacity,
                region.LastEvent);
        }
    }
}
