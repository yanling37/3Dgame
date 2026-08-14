using System.Collections.Generic;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Observation;
using UnityEngine;

namespace DivineWorld.Simulation.Presentation
{
    /// <summary>
    /// Placeholder resource markers bound to ObservationSnapshot via ResourceNodeState.
    /// Pooled (3 regions × 5 types); never created per frame or per history tick.
    /// </summary>
    public class ResourceNodeVisualizer : MonoBehaviour
    {
        [SerializeField] ObservationHost observation;
        [SerializeField] float spacing = ResourceNodeLayout.RegionSpacing;

        readonly ResourceNodeState _state = new ResourceNodeState();
        readonly Dictionary<string, Marker> _pool = new Dictionary<string, Marker>(16);
        readonly Dictionary<RegionId, Transform> _roots = new Dictionary<RegionId, Transform>(4);
        bool _subscribed;

        public ResourceNodeState State => _state;
        public int MarkerCount => _pool.Count;

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
            _state.Apply(snapshot);
            EnsureMarkers();
            SyncMarkers();
        }

        void EnsureMarkers()
        {
            var nodes = _state.Nodes;
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node == null || _pool.ContainsKey(node.NodeId))
                {
                    continue;
                }

                if (!_roots.TryGetValue(node.RegionId, out var root))
                {
                    var go = new GameObject(node.RegionId + "_Resources");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(((int)node.RegionId - 1) * spacing, 0f, 0f);

                    root = go.transform;
                    _roots[node.RegionId] = root;
                }

                var primitive = PrimitiveFor(node.Type);
                var markerGo = GameObject.CreatePrimitive(primitive);
                markerGo.name = node.NodeId;
                markerGo.transform.SetParent(root, false);
                var collider = markerGo.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.Destroy(collider);
                }

                var renderer = markerGo.GetComponent<Renderer>();
                _pool[node.NodeId] = new Marker
                {
                    Transform = markerGo.transform,
                    Renderer = renderer,
                    RegionId = node.RegionId,
                    Type = node.Type
                };
            }
        }

        void SyncMarkers()
        {
            var seen = new HashSet<string>();
            var nodes = _state.Nodes;
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node == null || !_pool.TryGetValue(node.NodeId, out var marker))
                {
                    continue;
                }

                seen.Add(node.NodeId);
                marker.Transform.localPosition = new Vector3(node.LocalX, node.LocalY, node.LocalZ);
                float fill = node.Fill;
                if (!node.Active || float.IsNaN(node.Amount) || float.IsInfinity(node.Amount))
                {
                    marker.Transform.gameObject.SetActive(false);
                    continue;
                }

                marker.Transform.gameObject.SetActive(true);
                float s = 0.22f + 0.35f * Mathf.Clamp01(fill > 0f ? fill : Mathf.Clamp01(node.Amount / 50000f));
                marker.Transform.localScale = ScaleFor(node.Type, s);
                SetColor(marker.Renderer, ColorFor(node.Type, node.RegionId));
            }

            foreach (var pair in _pool)
            {
                if (!seen.Contains(pair.Key) && pair.Value.Transform != null)
                {
                    pair.Value.Transform.gameObject.SetActive(false);
                }
            }
        }

        static PrimitiveType PrimitiveFor(ResourceNodeType type)
        {
            switch (type)
            {
                case ResourceNodeType.Food: return PrimitiveType.Cube;
                case ResourceNodeType.Water: return PrimitiveType.Sphere;
                case ResourceNodeType.Wood: return PrimitiveType.Capsule;
                case ResourceNodeType.Mineral: return PrimitiveType.Cube;
                default: return PrimitiveType.Sphere;
            }
        }

        static Vector3 ScaleFor(ResourceNodeType type, float s)
        {
            switch (type)
            {
                case ResourceNodeType.Wood: return new Vector3(s * 0.6f, s * 1.4f, s * 0.6f);
                case ResourceNodeType.Mineral: return new Vector3(s, s * 0.7f, s);
                default: return Vector3.one * s;
            }
        }

        static Color ColorFor(ResourceNodeType type, RegionId region)
        {
            Color baseColor;
            switch (type)
            {
                case ResourceNodeType.Food: baseColor = new Color(0.55f, 0.78f, 0.32f); break;
                case ResourceNodeType.Water: baseColor = new Color(0.30f, 0.62f, 0.92f); break;
                case ResourceNodeType.Wood: baseColor = new Color(0.28f, 0.52f, 0.28f); break;
                case ResourceNodeType.Mineral: baseColor = new Color(0.62f, 0.55f, 0.42f); break;
                default: baseColor = new Color(0.62f, 0.42f, 0.88f); break;
            }

            Color tint;
            switch (region)
            {
                case RegionId.Theocracy: tint = new Color(1f, 0.95f, 0.75f); break;
                case RegionId.Empire: tint = new Color(0.85f, 0.88f, 1f); break;
                default: tint = new Color(0.8f, 0.95f, 1f); break;
            }

            return Color.Lerp(baseColor, tint, 0.18f);
        }

        static void SetColor(Renderer renderer, Color color)
        {
            if (renderer != null)
            {
                renderer.material.color = color;
            }
        }

        sealed class Marker
        {
            public Transform Transform;
            public Renderer Renderer;
            public RegionId RegionId;
            public ResourceNodeType Type;
        }
    }
}
