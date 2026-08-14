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
                    string.Empty,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    ObservationEventRecord.None);
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
                region.LastEvent,
                region.Get(ResourceId.Timber),
                region.Get(ResourceId.Ore),
                region.Get(ResourceId.Magic),
                region.DiseasePressure,
                region.Education,
                region.FaithLevel,
                CopyEvents(region));
        }

        static ObservationEventRecord[] CopyEvents(RegionState region)
        {
            if (region.ActiveEvents == null || region.ActiveEvents.Count == 0)
            {
                return ObservationEventRecord.None;
            }

            int count = 0;
            for (int i = 0; i < region.ActiveEvents.Count; i++)
            {
                if (region.ActiveEvents[i] != null)
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return ObservationEventRecord.None;
            }

            var copy = new ObservationEventRecord[count];
            int w = 0;
            for (int i = 0; i < region.ActiveEvents.Count; i++)
            {
                var source = region.ActiveEvents[i];
                if (source == null)
                {
                    continue;
                }

                var record = ObservationEventRecord.From(source, region.Id);
                if (record.RegionId != region.Id)
                {
                    record = new ObservationEventRecord(
                        record.EventId,
                        record.EventType,
                        region.Id,
                        record.Scope,
                        record.StartDay,
                        record.Duration,
                        record.Severity);
                }

                copy[w++] = record;
            }

            return copy;
        }
    }
}
