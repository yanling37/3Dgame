using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Systems
{
    /// <summary>
    /// Legacy boot helper. Phase 1 prefers <see cref="DivineWorld.Simulation.SimulationBootstrap"/>.
    /// Kept so old scenes do not break if still referenced.
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] string nextScene = "MainMenu";
        [SerializeField] bool startSimulationInPlace = true;

        void Start()
        {
            if (startSimulationInPlace)
            {
                if (FindObjectOfType<DivineWorld.Simulation.SimulationBootstrap>() == null
                    && FindObjectOfType<DivineWorld.Simulation.Core.SimulationWorld>() == null)
                {
                    var go = new GameObject("SimulationBootstrap");
                    go.AddComponent<DivineWorld.Simulation.SimulationBootstrap>();
                }

                return;
            }

            if (GameState.Instance == null)
            {
                var go = new GameObject("GameState");
                go.AddComponent<GameState>();
            }

            GameState.Instance.SetState(GamePlayState.Boot);
            SceneManager.LoadScene(nextScene);
        }
    }
}
