using DivineWorld.Simulation.Core;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.UI;
using UnityEngine;

namespace DivineWorld.Simulation.Presentation
{
    /// <summary>
    /// Extremely simple 3D placeholders: one pillar+sphere "totem" per region.
    /// Optional; simulation does not depend on this.
    /// </summary>
    public class SimpleRegionMarkers : MonoBehaviour
    {
        [SerializeField] SimulationWorld world;
        [SerializeField] float spacing = 6f;

        Transform[] _markers;

        public void Bind(SimulationWorld simulationWorld)
        {
            world = simulationWorld;
            Build();
            if (world != null)
            {
                world.OnDayAdvanced += _ => RefreshScales();
            }
        }

        void Start()
        {
            if (world == null)
            {
                world = FindObjectOfType<SimulationWorld>();
            }

            if (_markers == null)
            {
                Build();
            }
        }

        void Build()
        {
            if (world == null || world.State == null)
            {
                return;
            }

            ClearChildren();
            var regions = world.State.Regions;
            _markers = new Transform[regions.Length];

            for (int i = 0; i < regions.Length; i++)
            {
                var root = new GameObject(regions[i].DisplayName).transform;
                root.SetParent(transform, false);
                root.localPosition = new Vector3((i - 1) * spacing, 0f, 0f);

                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = "Pillar";
                pillar.transform.SetParent(root, false);
                pillar.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
                pillar.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                SetColor(pillar, ColorFor(regions[i].Id));

                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Head";
                head.transform.SetParent(root, false);
                head.transform.localScale = Vector3.one * 1.1f;
                head.transform.localPosition = new Vector3(0f, 2.8f, 0f);
                SetColor(head, ColorFor(regions[i].Id) * 1.1f);

                _markers[i] = root;
            }

            RefreshScales();
        }

        void RefreshScales()
        {
            if (world?.State?.Regions == null || _markers == null)
            {
                return;
            }

            for (int i = 0; i < _markers.Length && i < world.State.Regions.Length; i++)
            {
                float popScale = Mathf.Clamp(world.State.Regions[i].Population / 50000f, 0.5f, 2.2f);
                _markers[i].localScale = new Vector3(popScale, popScale, popScale);
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

        static void SetColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
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
