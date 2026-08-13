using System;
using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// Headless-friendly observation cache. Unity wraps this in <see cref="ObservationHost"/>.
    /// </summary>
    public sealed class ObservationSession
    {
        public WorldObservationSnapshot Current { get; private set; } = WorldObservationSnapshot.Empty;

        public event Action<WorldObservationSnapshot> Updated;

        public WorldObservationSnapshot Capture(WorldState state)
        {
            Current = ObservationCapture.FromWorld(state);
            Updated?.Invoke(Current);
            return Current;
        }
    }
}
