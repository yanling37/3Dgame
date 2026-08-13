using UnityEngine;

namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// Parameterized mapping: snapshot population → marker count / scale.
    /// Count changes only when population crosses PopulationPerMarker thresholds.
    /// </summary>
    public readonly struct PopulationMarkerPlan
    {
        public PopulationMarkerPlan(int markerCount, float markerScale)
        {
            MarkerCount = markerCount;
            MarkerScale = markerScale;
        }

        public int MarkerCount { get; }
        public float MarkerScale { get; }
    }

    public static class PopulationMarkerRules
    {
        public static float SanitizePopulation(float population)
        {
            if (float.IsNaN(population) || float.IsInfinity(population) || population < 0f)
            {
                return 0f;
            }

            return population;
        }

        public static PopulationMarkerPlan Evaluate(float population, PopulationVisualizationConfig config)
        {
            var cfg = (config ?? PopulationVisualizationConfig.CreateDefault()).Sanitized();
            float pop = SanitizePopulation(population);

            int count = 0;
            if (cfg.PopulationPerMarker > 0f && cfg.MaxMarkersPerRegion > 0)
            {
                count = Mathf.FloorToInt(pop / cfg.PopulationPerMarker);
                if (count < 0)
                {
                    count = 0;
                }

                if (count > cfg.MaxMarkersPerRegion)
                {
                    count = cfg.MaxMarkersPerRegion;
                }
            }

            float maxRepresented = cfg.PopulationPerMarker * Mathf.Max(1, cfg.MaxMarkersPerRegion);
            float t = maxRepresented > 0f ? Mathf.Clamp01(pop / maxRepresented) : 0f;
            float scale = Mathf.Lerp(cfg.MinMarkerScale, cfg.MaxMarkerScale, t);
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale < 0f)
            {
                scale = cfg.MinMarkerScale;
            }

            return new PopulationMarkerPlan(count, scale);
        }

        public static int MarkerCount(float population, PopulationVisualizationConfig config)
        {
            return Evaluate(population, config).MarkerCount;
        }

        public static float MarkerScale(float population, PopulationVisualizationConfig config)
        {
            return Evaluate(population, config).MarkerScale;
        }
    }
}
