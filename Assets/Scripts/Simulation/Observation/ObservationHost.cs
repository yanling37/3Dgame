using System;
using DivineWorld.Simulation.Data;
using UnityEngine;

namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// Observation lifecycle + region selection for HUD / Map.
    /// Listens for world reset / day-advanced; does not run simulation math.
    /// </summary>
    public sealed class ObservationHost
    {
        public SimulationHistoryBuffer History { get; }
        public RegionId SelectedRegionId { get; private set; }

        public ObservationHost(int capacity = 4096)
        {
            History = new SimulationHistoryBuffer(capacity);
            SelectedRegionId = RegionId.Theocracy;
        }

        public WorldObservationSnapshot Latest => History.Latest;
        public WorldObservationSnapshot Current => Latest;
        public RegionObservationSnapshot SelectedRegion => FindRegion(SelectedRegionId);

        public event Action Changed;

        /// <summary>
        /// World was replaced. Drop previous-run samples, then record the new Day-0 baseline.
        /// </summary>
        public void HandleWorldReset(WorldState state)
        {
            History.Clear();
            SelectedRegionId = RegionId.Theocracy;
            RecordCurrent(state);
            Debug.Log("[P2-B Observation] Reset: history cleared, Day 0 recorded"
                + " samples=" + History.Count
                + " TotalDays=" + (state != null ? state.TotalDays : -1));
        }

        /// <summary>
        /// Daily tick or FastForward endpoint. Capture current State; do not clear history.
        /// </summary>
        public void HandleDayAdvanced(WorldState state)
        {
            RecordCurrent(state);
        }

        public WorldObservationSnapshot RecordCurrent(WorldState state)
        {
            var snap = SimulationObservation.Capture(state);
            History.Record(snap);
            EnsureSelection();
            Changed?.Invoke();
            return snap;
        }

        public void SelectRegion(RegionId regionId)
        {
            SelectedRegionId = regionId;
            EnsureSelection();
            Changed?.Invoke();
        }

        public RegionObservationSnapshot FindRegion(RegionId regionId)
        {
            var current = Current;
            if (current?.Regions == null)
            {
                return null;
            }

            for (int i = 0; i < current.Regions.Length; i++)
            {
                var region = current.Regions[i];
                if (region != null && region.RegionId == regionId)
                {
                    return region;
                }
            }

            return null;
        }

        void EnsureSelection()
        {
            if (FindRegion(SelectedRegionId) != null || Current?.Regions == null || Current.Regions.Length == 0)
            {
                return;
            }

            for (int i = 0; i < Current.Regions.Length; i++)
            {
                if (Current.Regions[i] != null)
                {
                    SelectedRegionId = Current.Regions[i].RegionId;
                    return;
                }
            }
        }
    }
}
