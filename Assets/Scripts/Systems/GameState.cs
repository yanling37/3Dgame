using UnityEngine;

namespace Game.Systems
{
    public enum GamePlayState
    {
        Boot,
        MainMenu,
        Playing,
        Paused,
        Victory,
        Defeat
    }

    /// <summary>
    /// Lightweight global game state for MVP flow.
    /// </summary>
    public class GameState : MonoBehaviour
    {
        public static GameState Instance { get; private set; }

        [SerializeField] GamePlayState current = GamePlayState.Boot;

        public GamePlayState Current => current;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetState(GamePlayState state)
        {
            current = state;
            Time.timeScale = state == GamePlayState.Paused ? 0f : 1f;
            Debug.Log($"[GameState] -> {state}");
        }
    }
}
