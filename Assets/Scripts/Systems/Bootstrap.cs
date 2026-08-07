using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Systems
{
    /// <summary>
    /// Ensures GameState exists, then loads the main menu.
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] string nextScene = "MainMenu";

        void Start()
        {
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
