using System.Collections.Generic;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Observation;
using UnityEngine;

namespace DivineWorld.Simulation.Presentation
{
    /// <summary>
    /// Placeholder population / settlement dots driven only by
    /// <see cref="RegionObservationSnapshot.Population"/>.
    /// Pooled per region up to MaxMarkersPerRegion; no per-person GameObjects.
    /// </summary>
    public class PopulationVisualizer : MonoBehaviour
    {
        [SerializeField] ObservationHost observation;
        [SerializeField] PopulationVisualizationConfig config = new PopulationVisualizationConfig();
        [SerializeField] float spacing = 7f;
        [SerializeField] float regionRadius = 2.2f;

        readonly Dictionary<RegionId, RegionPool> _pools = new Dictionary<RegionId, RegionPool>();
        readonly PopulationVisualState _logic = new PopulationVisualState();
        bool _subscribed;

        public PopulationVisualizationConfig Config => config;
        public PopulationVisualState Logic => _logic;

        public void Bind(ObservationHost host)
        {
            if (observation != null && _subscribed)
            {
                observation.OnSnapshotUpdated -= OnSnapshot;
                _subscribed = false;
            }

            observation = host;
            if (observation != null)
            {
                observation.OnSnapshotUpdated += OnSnapshot;
                _subscribed = true;
                Apply(observation.Current);
            }
        }

        public int VisibleCount(RegionId id) => _logic.VisibleCount(id);

        public int AllocatedCount(RegionId id) => _logic.AllocatedCount(id);

        void Start()
        {
            if (observation == null)
            {
                observation = FindObjectOfType<ObservationHost>();
            }

            if (observation != null && !_subscribed)
            {
                Bind(observation);
            }
        }

        void OnDestroy()
        {
            if (observation != null && _subscribed)
            {
                observation.OnSnapshotUpdated -= OnSnapshot;
                _subscribed = false;
            }
        }

        void OnSnapshot(WorldObservationSnapshot snapshot)
        {
            Apply(snapshot);
        }

        void Apply(WorldObservationSnapshot snapshot)
        {
            var cfg = (config ?? PopulationVisualizationConfig.CreateDefault()).Sanitized();
            config = cfg;
            EnsurePools(snapshot, cfg);
            _logic.Apply(snapshot, cfg);

            if (snapshot?.Regions == null)
            {
                foreach (var pair in _pools)
                {
                    SyncPool(pair.Value, 0, pair.Value.Scale);
                }

                return;
            }

            var seen = new HashSet<RegionId>();
            for (int i = 0; i < snapshot.Regions.Length; i++)
            {
                var region = snapshot.Regions[i];
                if (region == null || !_pools.TryGetValue(region.RegionId, out var pool))
                {
                    continue;
                }

                seen.Add(region.RegionId);
                var plan = PopulationMarkerRules.Evaluate(region.Population, cfg);
                SyncPool(pool, plan.MarkerCount, plan.MarkerScale);
            }

            foreach (var pair in _pools)
            {
                if (!seen.Contains(pair.Key))
                {
                    SyncPool(pair.Value, 0, pair.Value.Scale);
                }
            }
        }

        void EnsurePools(WorldObservationSnapshot snapshot, PopulationVisualizationConfig cfg)
        {
            if (snapshot?.Regions == null)
            {
                return;
            }

            int max = cfg.MaxMarkersPerRegion;
            for (int i = 0; i < snapshot.Regions.Length; i++)
            {
                var region = snapshot.Regions[i];
                if (region == null)
                {
                    continue;
                }

                if (!_pools.TryGetValue(region.RegionId, out var pool))
                {
                    var root = new GameObject(region.RegionId + "_Population").transform;
                    root.SetParent(transform, false);
                    int slot = (int)region.RegionId;
                    root.localPosition = new Vector3((slot - 1) * spacing, 0f, 0f);
                    pool = new RegionPool(region.RegionId, root);
                    _pools[region.RegionId] = pool;
                }

                GrowPool(pool, max, cfg);
            }
        }

        void GrowPool(RegionPool pool, int max, PopulationVisualizationConfig cfg)
        {
            while (pool.Markers.Count < max)
            {
                int d = pool.Markers.Count;
                var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dot.name = "PopMarker_" + d;
                dot.transform.SetParent(pool.Root, false);
                var collider = dot.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.Destroy(collider);
                }

                float ang = d * 2.399f;
                float rad = regionRadius * Mathf.Sqrt((d + 1f) / Mathf.Max(1, max));
                dot.transform.localPosition = new Vector3(Mathf.Cos(ang) * rad, 0.15f, Mathf.Sin(ang) * rad);
                dot.transform.localScale = Vector3.one * cfg.MinMarkerScale;
                SetColor(dot.GetComponent<Renderer>(), PlaceholderColor(pool.Id));
                dot.SetActive(false);
                pool.Markers.Add(dot);
                pool.Renderers.Add(dot.GetComponent<Renderer>());
            }
        }

        void SyncPool(RegionPool pool, int visible, float scale)
        {
            if (visible < 0)
            {
                visible = 0;
            }

            if (visible > pool.Markers.Count)
            {
                visible = pool.Markers.Count;
            }

            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale < 0f)
            {
                scale = config != null ? config.MinMarkerScale : PopulationVisualizationConfig.DefaultMinMarkerScale;
            }

            bool countChanged = pool.Visible != visible;
            bool scaleChanged = !Mathf.Approximately(pool.Scale, scale);
            if (!countChanged && !scaleChanged)
            {
                return;
            }

            if (countChanged)
            {
                for (int i = 0; i < pool.Markers.Count; i++)
                {
                    bool on = i < visible;
                    if (pool.Markers[i].activeSelf != on)
                    {
                        pool.Markers[i].SetActive(on);
                    }
                }

                pool.Visible = visible;
            }

            if (scaleChanged || countChanged)
            {
                var size = Vector3.one * scale;
                for (int i = 0; i < visible && i < pool.Markers.Count; i++)
                {
                    pool.Markers[i].transform.localScale = size;
                }

                pool.Scale = scale;
            }
        }

        static Color PlaceholderColor(RegionId id)
        {
            switch (id)
            {
                case RegionId.Theocracy: return new Color(0.82f, 0.74f, 0.42f);
                case RegionId.Empire: return new Color(0.58f, 0.58f, 0.82f);
                case RegionId.Sea: return new Color(0.32f, 0.66f, 0.82f);
                default: return new Color(0.7f, 0.7f, 0.7f);
            }
        }

        static void SetColor(Renderer renderer, Color color)
        {
            if (renderer != null)
            {
                renderer.material.color = color;
            }
        }

        sealed class RegionPool
        {
            public readonly RegionId Id;
            public readonly Transform Root;
            public readonly List<GameObject> Markers = new List<GameObject>();
            public readonly List<Renderer> Renderers = new List<Renderer>();
            public int Visible;
            public float Scale = -1f;

            public RegionPool(RegionId id, Transform root)
            {
                Id = id;
                Root = root;
            }
        }
    }
}
