using System;
using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// Headless-friendly observation cache. Unity wraps this in <see cref="ObservationHost"/>.
    /// History is recorded from each captured snapshot (simulation tick), never per frame.
    /// </summary>
    public sealed class ObservationSession
    {
        public WorldObservationSnapshot Current { get; private set; } = WorldObservationSnapshot.Empty;
        public ObservationHistory History { get; } = new ObservationHistory();

        public event Action<WorldObservationSnapshot> Updated;

        public WorldObservationSnapshot Capture(WorldState state)
        {
            Current = ObservationCapture.FromWorld(state);
            History.Record(Current);
            Updated?.Invoke(Current);
            return Current;
        }
    }
}
