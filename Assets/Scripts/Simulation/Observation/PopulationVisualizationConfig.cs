using System;
using UnityEngine;

namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// Centralized, tunable population marker rules. Do not hardcode these values in visualizers.
    /// </summary>
    [Serializable]
    public sealed class PopulationVisualizationConfig
    {
        public const float DefaultPopulationPerMarker = 2500f;
        public const int DefaultMaxMarkersPerRegion = 24;
        public const float DefaultMinMarkerScale = 0.12f;
        public const float DefaultMaxMarkerScale = 0.36f;

        public float PopulationPerMarker = DefaultPopulationPerMarker;
        public int MaxMarkersPerRegion = DefaultMaxMarkersPerRegion;
        public float MinMarkerScale = DefaultMinMarkerScale;
        public float MaxMarkerScale = DefaultMaxMarkerScale;

        public static PopulationVisualizationConfig CreateDefault()
        {
            return new PopulationVisualizationConfig();
        }

        public PopulationVisualizationConfig Clone()
        {
            return new PopulationVisualizationConfig
            {
                PopulationPerMarker = PopulationPerMarker,
                MaxMarkersPerRegion = MaxMarkersPerRegion,
                MinMarkerScale = MinMarkerScale,
                MaxMarkerScale = MaxMarkerScale
            };
        }

        public PopulationVisualizationConfig Sanitized()
        {
            var copy = Clone();
            if (!IsFinitePositive(copy.PopulationPerMarker))
            {
                copy.PopulationPerMarker = DefaultPopulationPerMarker;
            }

            if (copy.MaxMarkersPerRegion < 0)
            {
                copy.MaxMarkersPerRegion = 0;
            }

            if (!IsFiniteNonNegative(copy.MinMarkerScale))
            {
                copy.MinMarkerScale = DefaultMinMarkerScale;
            }

            if (!IsFiniteNonNegative(copy.MaxMarkerScale))
            {
                copy.MaxMarkerScale = DefaultMaxMarkerScale;
            }

            if (copy.MinMarkerScale > copy.MaxMarkerScale)
            {
                float tmp = copy.MinMarkerScale;
                copy.MinMarkerScale = copy.MaxMarkerScale;
                copy.MaxMarkerScale = tmp;
            }

            return copy;
        }

        static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }
}
