using DivineWorld.Simulation.Core;
using DivineWorld.Simulation.Data;
using UnityEngine;

namespace DivineWorld.Simulation.Presentation
{
    /// <summary>
    /// Reads simulation data and renders map region totems / event cues.
    /// Population markers live in <see cref="PopulationVisualizer"/> (observation snapshots).
    /// </summary>
    public class MapVisualizationController : MonoBehaviour
    {
        [SerializeField] SimulationWorld world;
        [SerializeField] float spacing = 7f;

        struct RegionView
        {
            public Transform Root;
            public Transform Totem;
            public Renderer TotemRenderer;
            public Transform EventMarker;
            public Renderer EventRenderer;

            public RegionView(Transform root)
            {
                Root = root;
                Totem = null;
                TotemRenderer = null;
                EventMarker = null;
                EventRenderer = null;
            }
        }

        RegionView[] _views;
        bool _bound;

        public void Bind(SimulationWorld simulationWorld)
        {
            world = simulationWorld;
            Build();
            if (world != null && !_bound)
            {
                world.OnDayAdvanced += OnDay;
                _bound = true;
            }

            Refresh();
        }

        void Start()
        {
            if (world == null)
            {
                world = FindObjectOfType<SimulationWorld>();
            }

            if (world != null && _views == null)
            {
                Bind(world);
            }
        }

        void OnDestroy()
        {
            if (world != null && _bound)
            {
                world.OnDayAdvanced -= OnDay;
            }
        }

        void OnDay(WorldState _)
        {
            Refresh();
        }

        void Build()
        {
            ClearChildren();
            if (world?.State?.Regions == null)
            {
                return;
            }

            var regions = world.State.Regions;
            _views = new RegionView[regions.Length];

            for (int i = 0; i < regions.Length; i++)
            {
                var root = new GameObject(regions[i].DisplayName).transform;
                root.SetParent(transform, false);
                root.localPosition = new Vector3((i - 1) * spacing, 0f, 0f);

                var view = new RegionView(root);

                var totem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                totem.name = "Totem";
                totem.transform.SetParent(root, false);
                totem.transform.localScale = new Vector3(1.2f, 0.2f, 1.2f);
                totem.transform.localPosition = new Vector3(0f, 0.2f, 0f);
                Object.Destroy(totem.GetComponent<Collider>());
                view.Totem = totem.transform;
                view.TotemRenderer = totem.GetComponent<Renderer>();

                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = "Pillar";
                pillar.transform.SetParent(root, false);
                pillar.transform.localScale = new Vector3(0.35f, 1.0f, 0.35f);
                pillar.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                Object.Destroy(pillar.GetComponent<Collider>());
                SetColor(pillar.GetComponent<Renderer>(), ColorFor(regions[i].Id));

                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Head";
                head.transform.SetParent(root, false);
                head.transform.localScale = Vector3.one * 0.7f;
                head.transform.localPosition = new Vector3(0f, 2.3f, 0f);
                Object.Destroy(head.GetComponent<Collider>());
                SetColor(head.GetComponent<Renderer>(), ColorFor(regions[i].Id) * 1.15f);

                var evt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                evt.name = "EventMarker";
                evt.transform.SetParent(root, false);
                evt.transform.localScale = Vector3.one * 0.45f;
                evt.transform.localPosition = new Vector3(1.4f, 2.6f, 0f);
                Object.Destroy(evt.GetComponent<Collider>());
                view.EventMarker = evt.transform;
                view.EventRenderer = evt.GetComponent<Renderer>();
                view.EventMarker.gameObject.SetActive(false);

                _views[i] = view;
            }
        }

        public void Refresh()
        {
            if (world?.State?.Regions == null || _views == null)
            {
                return;
            }

            for (int i = 0; i < _views.Length && i < world.State.Regions.Length; i++)
            {
                var region = world.State.Regions[i];
                var view = _views[i];

                // Resource cue: totem height/color from food + mana.
                float foodNorm = Mathf.Clamp01(region.Get(ResourceId.Food) / Mathf.Max(1f, region.Population));
                float manaNorm = Mathf.Clamp01(region.Get(ResourceId.Magic) / 8000f);
                if (view.Totem != null)
                {
                    float h = 0.15f + foodNorm * 0.55f;
                    view.Totem.localScale = new Vector3(1.2f + manaNorm * 0.4f, h, 1.2f + manaNorm * 0.4f);
                    view.Totem.localPosition = new Vector3(0f, h, 0f);
                    SetColor(view.TotemRenderer, Color.Lerp(new Color(0.35f, 0.3f, 0.2f), new Color(0.4f, 0.75f, 0.35f), foodNorm));
                }

                // Event marker
                var dominant = DominantEvent(region);
                if (dominant == SimEventType.None)
                {
                    view.EventMarker.gameObject.SetActive(false);
                }
                else
                {
                    view.EventMarker.gameObject.SetActive(true);
                    SetColor(view.EventRenderer, EventColor(dominant));
                }
            }
        }

        static SimEventType DominantEvent(RegionState region)
        {
            if (region.ActiveEvents == null || region.ActiveEvents.Count == 0)
            {
                return SimEventType.None;
            }

            SimEventType best = region.ActiveEvents[0].EventType;
            float sev = region.ActiveEvents[0].Severity;
            for (int i = 1; i < region.ActiveEvents.Count; i++)
            {
                if (region.ActiveEvents[i].Severity >= sev)
                {
                    sev = region.ActiveEvents[i].Severity;
                    best = region.ActiveEvents[i].EventType;
                }
            }

            return best;
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
