using DivineWorld.Simulation.Core;
using DivineWorld.Simulation.Presentation;
using DivineWorld.Simulation.UI;
using UnityEngine;

namespace DivineWorld.Simulation
{
    /// <summary>
    /// Spawns Phase 2 simulation runtime: world + HUD + map visualization.
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

            if (createHud)
            {
                var hudGo = new GameObject("WorldObserverHud");
                var hud = hudGo.AddComponent<WorldObserverHud>();
                hud.Bind(world);
            }

            if (createMapVisualization)
            {
                var mapGo = new GameObject("MapVisualization");
                mapGo.transform.position = new Vector3(0f, 0f, 8f);
                var map = mapGo.AddComponent<MapVisualizationController>();
                map.Bind(world);
            }

            Debug.Log("[DivineWorld] Phase 2 simulation started (season / resources / events / map / fast-forward).");
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
