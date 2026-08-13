using System.Collections.Generic;
using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// Headless stand-in for per-region marker pools.
    /// Allocates a bounded slot count per region; never grows with raw population.
    /// Count updates only when the display threshold is crossed.
    /// </summary>
    public sealed class PopulationVisualState
    {
        readonly Dictionary<RegionId, RegionMarkers> _regions = new Dictionary<RegionId, RegionMarkers>();

        public int AllocatedMarkerSlots { get; private set; }
        public int PoolGrowEvents { get; private set; }
        public int DestroyEvents { get; private set; }
        public int CountChangeEvents { get; private set; }

        public void Apply(WorldObservationSnapshot snapshot, PopulationVisualizationConfig config)
        {
            var cfg = (config ?? PopulationVisualizationConfig.CreateDefault()).Sanitized();
            if (snapshot == null || snapshot.Regions == null)
            {
                HideAll();
                return;
            }

            var seen = new HashSet<RegionId>();
            for (int i = 0; i < snapshot.Regions.Length; i++)
            {
                var region = snapshot.Regions[i];
                if (region == null)
                {
                    continue;
                }

                seen.Add(region.RegionId);
                EnsureCapacity(region.RegionId, cfg.MaxMarkersPerRegion);
                var plan = PopulationMarkerRules.Evaluate(region.Population, cfg);
                ApplyPlan(region.RegionId, plan);
            }

            foreach (var pair in _regions)
            {
                if (!seen.Contains(pair.Key))
                {
                    ApplyPlan(pair.Key, new PopulationMarkerPlan(0, pair.Value.Scale));
                }
            }
        }

        /// <summary>Reset path: hide current markers then rebuild from a new snapshot. Does not destroy pooled slots.</summary>
        public void RebuildFrom(WorldObservationSnapshot snapshot, PopulationVisualizationConfig config)
        {
            HideAll();
            Apply(snapshot, config);
        }

        public void HideAll()
        {
            foreach (var pair in _regions)
            {
                ApplyPlan(pair.Key, new PopulationMarkerPlan(0, pair.Value.Scale));
            }
        }

        public int VisibleCount(RegionId id)
        {
            return _regions.TryGetValue(id, out var markers) ? markers.Visible : 0;
        }

        public float Scale(RegionId id)
        {
            return _regions.TryGetValue(id, out var markers) ? markers.Scale : 0f;
        }

        public int AllocatedCount(RegionId id)
        {
            return _regions.TryGetValue(id, out var markers) ? markers.Allocated : 0;
        }

        public bool HasNegativeVisible()
        {
            foreach (var pair in _regions)
            {
                if (pair.Value.Visible < 0)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasNonFiniteScale()
        {
            foreach (var pair in _regions)
            {
                float s = pair.Value.Scale;
                if (float.IsNaN(s) || float.IsInfinity(s))
                {
                    return true;
                }
            }

            return false;
        }

        void EnsureCapacity(RegionId id, int maxMarkers)
        {
            if (!_regions.TryGetValue(id, out var markers))
            {
                markers = new RegionMarkers();
                _regions[id] = markers;
            }

            if (markers.Allocated < maxMarkers)
            {
                int grow = maxMarkers - markers.Allocated;
                markers.Allocated = maxMarkers;
                AllocatedMarkerSlots += grow;
                PoolGrowEvents++;
            }
        }

        void ApplyPlan(RegionId id, PopulationMarkerPlan plan)
        {
            if (!_regions.TryGetValue(id, out var markers))
            {
                return;
            }

            int visible = plan.MarkerCount;
            if (visible < 0)
            {
                visible = 0;
            }

            if (visible > markers.Allocated)
            {
                visible = markers.Allocated;
            }

            if (markers.Visible != visible)
            {
                markers.Visible = visible;
                CountChangeEvents++;
            }

            markers.Scale = plan.MarkerScale;
        }

        sealed class RegionMarkers
        {
            public int Allocated;
            public int Visible;
            public float Scale;
        }
    }
}
