using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Observation;
using UnityEngine;

namespace DivineWorld.Simulation.Presentation
{
    /// <summary>
    /// Map observation presenter (P2-B v0.2).
    /// Reads snapshots from ObservationHost only. Population visual rules are deferred
    /// to <see cref="IRegionPopulationVisualizer"/> (pending implementation).
    /// Event markers stay on the owning region.
    /// </summary>
    public class MapVisualizationController : MonoBehaviour
    {
        [SerializeField] float spacing = 7f;

        ObservationHost _observation;

        struct RegionView
        {
            public RegionId RegionId;
            public Transform Root;
            public Transform EventMarker;
            public Renderer EventRenderer;
            public IRegionPopulationVisualizer PopulationVisualizer;
        }

        RegionView[] _views;
        bool _bound;

        public void Bind(ObservationHost host)
        {
            if (_observation != null && _bound)
            {
                _observation.Changed -= OnObservationChanged;
                _bound = false;
            }

            _observation = host;
            Build();
            if (_observation != null && !_bound)
            {
                _observation.Changed += OnObservationChanged;
                _bound = true;
            }

            Refresh();
        }

        void OnDestroy()
        {
            if (_observation != null && _bound)
            {
                _observation.Changed -= OnObservationChanged;
            }
        }

        void OnObservationChanged()
        {
            Refresh();
        }

        void Build()
        {
            ClearChildren();
            var snap = _observation != null ? _observation.Current : null;
            if (snap?.Regions == null)
            {
                return;
            }

            _views = new RegionView[snap.Regions.Length];
            for (int i = 0; i < snap.Regions.Length; i++)
            {
                var region = snap.Regions[i];
                var root = new GameObject(region.DisplayName).transform;
                root.SetParent(transform, false);
                root.localPosition = new Vector3((i - 1) * spacing, 0f, 0f);

                var view = new RegionView
                {
                    RegionId = region.RegionId,
                    Root = root,
                    PopulationVisualizer = new PendingRegionPopulationVisualizer()
                };

                var totem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                totem.name = "Totem";
                totem.transform.SetParent(root, false);
                totem.transform.localScale = new Vector3(1.2f, 0.2f, 1.2f);
                totem.transform.localPosition = new Vector3(0f, 0.2f, 0f);

                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = "Pillar";
                pillar.transform.SetParent(root, false);
                pillar.transform.localScale = new Vector3(0.35f, 1.0f, 0.35f);
                pillar.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                SetColor(pillar.GetComponent<Renderer>(), ColorFor(region.RegionId));

                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Head";
                head.transform.SetParent(root, false);
                head.transform.localScale = Vector3.one * 0.7f;
                head.transform.localPosition = new Vector3(0f, 2.3f, 0f);
                SetColor(head.GetComponent<Renderer>(), ColorFor(region.RegionId) * 1.15f);

                var evt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                evt.name = "EventMarker";
                evt.transform.SetParent(root, false);
                evt.transform.localScale = Vector3.one * 0.45f;
                evt.transform.localPosition = new Vector3(1.4f, 2.6f, 0f);
                view.EventMarker = evt.transform;
                view.EventRenderer = evt.GetComponent<Renderer>();
                view.EventMarker.gameObject.SetActive(false);

                var pickCollider = root.gameObject.AddComponent<BoxCollider>();
                pickCollider.size = new Vector3(2.2f, 3.2f, 2.2f);
                pickCollider.center = new Vector3(0f, 1.2f, 0f);
                var pick = root.gameObject.AddComponent<RegionPickTarget>();
                pick.Bind(_observation, region.RegionId);

                _views[i] = view;
            }
        }

        public void Refresh()
        {
            var snap = _observation != null ? _observation.Current : null;
            if (snap?.Regions == null)
            {
                return;
            }

            if (_views == null || _views.Length != snap.Regions.Length)
            {
                Build();
            }

            if (_views == null)
            {
                return;
            }

            for (int i = 0; i < _views.Length && i < snap.Regions.Length; i++)
            {
                var region = _observation.FindRegion(_views[i].RegionId) ?? snap.Regions[i];
                var view = _views[i];
                view.PopulationVisualizer?.Apply(region);

                var dominant = ObservationPanelText.DominantActiveEvent(region);
                if (dominant != null && dominant.RegionId == view.RegionId)
                {
                    view.EventMarker.gameObject.SetActive(true);
                    SetColor(view.EventRenderer, EventColor(dominant.EventType));
                }
                else if (view.EventMarker != null)
                {
                    view.EventMarker.gameObject.SetActive(false);
                }

                bool selected = _observation != null && _observation.SelectedRegionId == view.RegionId;
                view.Root.localScale = selected ? new Vector3(1.08f, 1.08f, 1.08f) : Vector3.one;
            }
        }

        static Color EventColor(SimEventType type)
        {
            switch (type)
            {
                case SimEventType.FoodShortage: return new Color(0.95f, 0.7f, 0.2f);
                case SimEventType.DiseaseOutbreak: return new Color(0.7f, 0.2f, 0.85f);
                case SimEventType.LowStability: return new Color(0.9f, 0.25f, 0.25f);
                case SimEventType.HighStability: return new Color(0.3f, 0.75f, 1f);
                case SimEventType.NaturalDisaster: return new Color(1f, 0.4f, 0.1f);
                case SimEventType.YearTurn: return new Color(0.9f, 0.9f, 0.5f);
                default: return Color.white;
            }
        }

        static Color ColorFor(RegionId id)
        {
            switch (id)
            {
                case RegionId.Theocracy: return new Color(0.85f, 0.75f, 0.35f);
                case RegionId.Empire: return new Color(0.55f, 0.55f, 0.85f);
                case RegionId.Sea: return new Color(0.25f, 0.65f, 0.85f);
                default: return Color.gray;
            }
        }

        static void SetColor(Renderer renderer, Color color)
        {
            if (renderer != null)
            {
                renderer.material.color = color;
            }
        }

        void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }
    }
}
