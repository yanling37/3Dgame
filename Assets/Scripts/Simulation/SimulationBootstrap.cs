using DivineWorld.Simulation.Core;
using DivineWorld.Simulation.Observation;
using DivineWorld.Simulation.Politics;
using DivineWorld.Simulation.Presentation;
using DivineWorld.Simulation.UI;
using UnityEngine;

namespace DivineWorld.Simulation
{
    /// <summary>
    /// Spawns Phase 2 simulation runtime: world + observation + HUD + map + population markers.
    /// Attach to any GameObject, or let Boot scene create it.
    /// </summary>
    public class SimulationBootstrap : MonoBehaviour
    {
        [SerializeField] bool createCameraIfMissing = true;
        [SerializeField] bool createMapVisualization = true;
        [SerializeField] bool createHud = true;

        void Awake()
        {
            EnsureCamera();

            var worldGo = new GameObject("SimulationWorld");
            var world = worldGo.AddComponent<SimulationWorld>();

            var observationGo = new GameObject("ObservationHost");
            var observation = observationGo.AddComponent<ObservationHost>();
            observation.Bind(world);

            if (createHud)
            {
                var hudGo = new GameObject("WorldObserverHud");
                var hud = hudGo.AddComponent<WorldObserverHud>();
                hud.Bind(world, observation);

                var historyGo = new GameObject("HistoryTrendHud");
                var historyHud = historyGo.AddComponent<HistoryTrendHud>();
                historyHud.Bind(observation);

                var politicsGo = new GameObject("PoliticsHud");
                var politicsHud = politicsGo.AddComponent<PoliticsHud>();
                politicsHud.Bind(world);
            }

            if (createMapVisualization)
            {
                var mapRoot = new GameObject("WorldMap");
                mapRoot.transform.position = new Vector3(0f, 0f, 8f);

                var mapGo = new GameObject("MapVisualization");
                mapGo.transform.SetParent(mapRoot.transform, false);
                var map = mapGo.AddComponent<MapVisualizationController>();
                map.Bind(world);

                var popGo = new GameObject("PopulationVisualizer");
                popGo.transform.SetParent(mapRoot.transform, false);
                var pop = popGo.AddComponent<PopulationVisualizer>();
                pop.Bind(observation);

                var resGo = new GameObject("ResourceNodeVisualizer");
                resGo.transform.SetParent(mapRoot.transform, false);
                var resources = resGo.AddComponent<ResourceNodeVisualizer>();
                resources.Bind(observation);
            }

            Debug.Log("[DivineWorld] " + ObservationVersion.HudTitle + " + " + PoliticsVersion.HudTitle + " started.");
        }

        void EnsureCamera()
        {
            if (!createCameraIfMissing)
            {
                return;
            }

            if (Camera.main != null)
            {
                Camera.main.transform.position = new Vector3(0f, 5f, -8f);
                Camera.main.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
                return;
            }

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.transform.position = new Vector3(0f, 5f, -8f);
            cam.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
        }
    }
}
