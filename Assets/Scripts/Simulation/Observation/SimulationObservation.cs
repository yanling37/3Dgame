using System.Collections.Generic;
using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// Read-only projection from Simulation State into observation DTOs.
    /// Does not call population/resource/event formulas — only copies fields.
    /// </summary>
    public static class SimulationObservation
    {
        public static WorldObservationSnapshot Capture(WorldState state)
        {
            if (state == null)
            {
                return new WorldObservationSnapshot();
            }

            var regions = state.Regions ?? System.Array.Empty<RegionState>();
            var snaps = new RegionObservationSnapshot[regions.Length];
            float totalPop = 0f;
            float totalFood = 0f;
            float totalWater = 0f;
            float totalMana = 0f;

            for (int i = 0; i < regions.Length; i++)
            {
                var r = regions[i];
                var snap = CaptureRegion(r, state.TotalDays);
                snaps[i] = snap;
                totalPop += snap.Population;
                totalFood += snap.Food;
                totalWater += snap.Water;
                totalMana += snap.Mana;
            }

            return new WorldObservationSnapshot
            {
                WorldName = state.WorldName,
                Year = state.Year,
                DayOfYear = state.DayOfYear,
                TotalDays = state.TotalDays,
                CurrentSeason = state.CurrentSeason,
                SeasonIndex = state.SeasonIndex,
                DayInSeason = state.DayInSeason,
                SeasonProgress = state.SeasonProgress,
                DaysPerYear = SimulationConfig.DaysPerYear,
                DaysPerSeason = SimulationConfig.DaysPerSeason,
                HaltedOnNumericError = state.HaltedOnNumericError,
                LastNumericError = state.LastNumericError,
                Regions = snaps,
                TotalPopulation = totalPop,
                TotalFood = totalFood,
                TotalWater = totalWater,
                TotalMana = totalMana
            };
        }

        public static RegionObservationSnapshot CaptureRegion(RegionState region, int worldTotalDays)
        {
            if (region == null)
            {
                return new RegionObservationSnapshot();
            }

            return new RegionObservationSnapshot
            {
                RegionId = region.Id,
                DisplayName = region.DisplayName,
                Population = region.Population,
                PopulationDelta = region.PopulationDelta,
                CarryingCapacity = region.LastCarryingCapacity,
                Food = region.Get(ResourceId.Food),
                Water = region.Get(ResourceId.Water),
                Timber = region.Get(ResourceId.Timber),
                Ore = region.Get(ResourceId.Ore),
                Mana = region.Get(ResourceId.Magic),
                DiseasePressure = region.DiseasePressure,
                Stability = region.Stability,
                Education = region.Education,
                Faith = region.FaithLevel,
                WeatherFactor = region.WeatherFactor,
                LastEventSummary = region.LastEvent,
                ActiveEvents = CaptureEvents(region.ActiveEvents, worldTotalDays)
            };
        }

        static EventObservation[] CaptureEvents(List<RegionEvent> events, int worldTotalDays)
        {
            if (events == null || events.Count == 0)
            {
                return System.Array.Empty<EventObservation>();
            }

            var list = new EventObservation[events.Count];
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                list[i] = new EventObservation
                {
                    EventId = e.EventId,
                    DisplayName = ObservationLabels.EventDisplayName(e.EventType),
                    EventType = e.EventType,
                    RegionId = e.RegionId,
                    Scope = e.Scope,
                    StartDay = e.StartDay,
                    Duration = e.Duration,
                    EndDay = e.EndDay,
                    RemainingDays = System.Math.Max(0, e.EndDay - worldTotalDays),
                    Severity = e.Severity,
                    IsActive = e.IsActiveOn(worldTotalDays)
                };
            }

            return list;
        }
    }
}
