using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// Immutable read-only copy of one region's observable fields.
    /// Values are copied from Simulation State; never recomputed here.
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
            string lastEvent,
            float wood,
            float mineral,
            float magic,
            float disease,
            float education,
            float faith,
            float lastWaterCapacity,
            ObservationEventRecord[] events)
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
            Wood = wood;
            Mineral = mineral;
            Magic = magic;
            Disease = disease;
            Education = education;
            Faith = faith;
            LastWaterCapacity = lastWaterCapacity;
            Events = events ?? ObservationEventRecord.None;
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
        public float CarryingCapacity => LastCarryingCapacity;
        public string LastEvent { get; }
        public float Wood { get; }
        public float Mineral { get; }
        public float Magic { get; }
        public float Disease { get; }
        public float Education { get; }
        public float Faith { get; }
        public float LastWaterCapacity { get; }
        public float WaterCapacity => LastWaterCapacity;
        public ObservationEventRecord[] Events { get; }
    }
}
