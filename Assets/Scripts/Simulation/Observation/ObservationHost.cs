using DivineWorld.Simulation.Data;
using UnityEngine;

namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// Observation lifecycle: listens for world reset / day-advanced and maintains HistoryBuffer.
    /// Does not run simulation math. Callers subscribe <see cref="HandleWorldReset"/> and
    /// <see cref="HandleDayAdvanced"/> to SimulationWorld (or HeadlessWorld) events.
    /// </summary>
    public sealed class ObservationHost
    {
        public SimulationHistoryBuffer History { get; }

        public ObservationHost(int capacity = 4096)
        {
            History = new SimulationHistoryBuffer(capacity);
        }

        public WorldObservationSnapshot Latest => History.Latest;

        /// <summary>
        /// World was replaced. Drop previous-run samples, then record the new Day-0 baseline.
        /// </summary>
        public void HandleWorldReset(WorldState state)
        {
            History.Clear();
            RecordCurrent(state);
            Debug.Log("[P2-B Observation] Reset: history cleared, Day 0 recorded"
                + " samples=" + History.Count
                + " TotalDays=" + (state != null ? state.TotalDays : -1));
        }

        /// <summary>
        /// Daily tick or FastForward endpoint. Capture current State; do not clear history.
        /// Same TotalDays replaces the last sample (idempotent).
        /// </summary>
        public void HandleDayAdvanced(WorldState state)
        {
            RecordCurrent(state);
        }

        public WorldObservationSnapshot RecordCurrent(WorldState state)
        {
            var snap = SimulationObservation.Capture(state);
            History.Record(snap);
            return snap;
        }
    }
}
