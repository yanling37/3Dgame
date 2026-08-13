namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// Pluggable region population visualization.
    /// Visual density / size / count rules are NOT decided in v0.2 — implement later.
    /// Must read population from <see cref="RegionObservationSnapshot.Population"/>.
    /// Must not spawn one GameObject per person.
    /// </summary>
    public interface IRegionPopulationVisualizer
    {
        void Apply(RegionObservationSnapshot region);

        float LastObservedPopulation { get; }
    }

    /// <summary>
    /// Placeholder visualizer: consumes snapshot population and stores it.
    /// Does not invent mapping rules or spawn population dots.
    /// </summary>
    public sealed class PendingRegionPopulationVisualizer : IRegionPopulationVisualizer
    {
        public float LastObservedPopulation { get; private set; }

        public void Apply(RegionObservationSnapshot region)
        {
            LastObservedPopulation = region != null ? region.Population : 0f;
        }
    }
}
