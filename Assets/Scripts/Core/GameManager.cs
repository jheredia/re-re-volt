using UnityEngine;
using UI;
using AI;
using Vehicle;
using Weapons;

namespace Core
{
    /// <summary>
    /// Central game state authority. Owns the top-level state machine
    /// and provides a signleton access point for all managers
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // Singleton instance
        public static GameManager Instance { get; private set; }

        // State machine
        public enum GameState
        {
            MainMenu,
            Loading, 
            Playing,
            Paused,
            GameOver
        }

        public GameState CurrentState { get; private set; } = GameState.MainMenu;

        // Manager references (assigned in inspector)
        [Header("Managers")]
        public RaceManager RaceManager;
        public UIManager UIManager;
        public VehicleManager VehicleManager;
        public AIManager AIManager;
        public WeaponManager WeaponManager;
        public SaveManager SaveManager;

            void Awake()
            {
                // Enforce singleton 
                if (Instance != null && Instance != this)
                {
                    Destroy(gameObject);
                    return;
                }
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }


            // Start is called once before the first execution of Update after the MonoBehaviour is created
            void Start()
        {
            RaceManager = RaceManager != null ? RaceManager : FindAnyObjectByType<RaceManager>();
            UIManager = UIManager != null ? UIManager : FindAnyObjectByType<UIManager>();
            VehicleManager = VehicleManager != null ? VehicleManager : FindAnyObjectByType<VehicleManager>();
            AIManager = AIManager != null ? AIManager : FindAnyObjectByType<AIManager>();
            WeaponManager = WeaponManager != null ? WeaponManager : FindAnyObjectByType<WeaponManager>();
            SaveManager = SaveManager != null ? SaveManager : FindAnyObjectByType<SaveManager>();
        }

        // State transitions

        public void SetState(GameState newState)
        {
            if (CurrentState == newState) return;

            Debug.Log($"[GameManager] {CurrentState} -> {newState}");
            CurrentState = newState;

            // Enter logic for new state
            switch (newState)
                {
                    case GameState.MainMenu:
                        Time.timeScale = 1f;
                        break;
                    case GameState.Loading:
                        Time.timeScale = 1f;
                        break;
                    case GameState.Playing:
                        Time.timeScale = 1f;
                        break;
                    case GameState.Paused:
                        Time.timeScale = 0f;
                        break;
                    case GameState.GameOver:
                        Time.timeScale = 0f;
                        break;
                }
        }

        #region Helpers
        // Helpers 
        public bool IsRacing => CurrentState == GameState.Playing;
        public bool IsPaused => CurrentState == GameState.Paused;

        public void StartGame() => SetState(GameState.Playing);
        public void PauseGame() => SetState(GameState.Paused);
        public void ResumeGame() => SetState(GameState.Playing);
        public void EndGame() => SetState(GameState.GameOver);
        public void ReturnToMainMenu() => SetState(GameState.MainMenu);
        #endregion
    }
}