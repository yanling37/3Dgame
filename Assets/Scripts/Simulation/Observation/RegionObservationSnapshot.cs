using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// Immutable read-only copy of one region's observable fields.
    /// Population is copied from Simulation State; never recomputed here.
    /// </summary>
    public sealed class RegionObservationSnapshot
    {
        public RegionObservationSnapshot(
            RegionId regionId,
            string displayName,
            float population,
            float populationDelta,
            float food,
            float water,
            float stability,
            float lastCarryingCapacity,
            string lastEvent)
        {
            RegionId = regionId;
            DisplayName = displayName ?? string.Empty;
            Population = population;
            PopulationDelta = populationDelta;
            Food = food;
            Water = water;
            Stability = stability;
            LastCarryingCapacity = lastCarryingCapacity;
            LastEvent = lastEvent ?? string.Empty;
        }

        public RegionId RegionId { get; }
        public string DisplayName { get; }

        /// <summary>Copied from RegionState.Population. Unique population source for visualization.</summary>
        public float Population { get; }

        public float PopulationDelta { get; }
        public float Food { get; }
        public float Water { get; }
        public float Stability { get; }
        public float LastCarryingCapacity { get; }
        public string LastEvent { get; }
    }
}
