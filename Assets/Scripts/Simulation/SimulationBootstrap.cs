using DivineWorld.Simulation.Core;
using DivineWorld.Simulation.Presentation;
using DivineWorld.Simulation.UI;
using UnityEngine;

namespace DivineWorld.Simulation
{
    /// <summary>
    /// Spawns Phase 1 simulation runtime in an empty scene.
    /// Attach to any GameObject, or let Boot scene create it.
    /// </summary>
    public class SimulationBootstrap : MonoBehaviour
    {
        [SerializeField] bool createCameraIfMissing = true;
        [SerializeField] bool createRegionMarkers = true;
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

            if (createRegionMarkers)
            {
                var markersGo = new GameObject("RegionMarkers");
                markersGo.transform.position = new Vector3(0f, 0f, 8f);
                var markers = markersGo.AddComponent<SimpleRegionMarkers>();
                markers.Bind(world);
            }

            Debug.Log("[DivineWorld] Phase 1 simulation started. Use on-screen Observer HUD.");
        }

        void EnsureCamera()
        {
            if (!createCameraIfMissing)
            {
                return;
            }

            if (Camera.main != null)
            {
                Camera.main.transform.position = new Vector3(0f, 4f, -6f);
                Camera.main.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
                return;
            }

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.transform.position = new Vector3(0f, 4f, -6f);
            cam.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
        }
    }
}
