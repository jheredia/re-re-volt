using UnityEngine;
using TMPro;

namespace UI {
    /// <summary>
    /// Controls all in-game UI: HUD (position, lap, timer, weapon),
    /// countdown display, pause menu, and results screen.
    /// Phase 6 will flesh this out fully.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("HUD")]
        [SerializeField] private TextMeshProUGUI positionText;
        [SerializeField] private TextMeshProUGUI lapText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI weaponText;

        [Header("Countdown")]
        [SerializeField] private TextMeshProUGUI countdownText;

        [Header("Screens")]
        [SerializeField] private GameObject pauseScreen;
        [SerializeField] private GameObject resultsScreen;

        // ── HUD Update ─────────────────────────────────────────────────────────
        public void UpdatePosition(int position, int total)
        {
            if (positionText != null)
                positionText.text = $"{position}/{total}";
        }

        public void UpdateLap(int current, int total)
        {
            if (lapText != null)
                lapText.text = $"Lap {current}/{total}";
        }

        public void UpdateTimer(float seconds)
        {
            if (timerText == null) return;
            int   m = Mathf.FloorToInt(seconds / 60f);
            float s = seconds % 60f;
            timerText.text = $"{m:00}:{s:00.00}";
        }

        public void UpdateWeapon(string weaponName)
        {
            if (weaponText != null)
                weaponText.text = string.IsNullOrEmpty(weaponName) ? "—" : weaponName;
        }

        // ── Countdown ─────────────────────────────────────────────────────────
        public void ShowCountdown(int number)
        {
            if (countdownText == null) return;
            countdownText.gameObject.SetActive(true);
            countdownText.text = number > 0 ? number.ToString() : "GO!";
        }

        public void HideCountdown()
        {
            if (countdownText != null)
                countdownText.gameObject.SetActive(false);
        }

        // ── Screens ────────────────────────────────────────────────────────────
        public void ShowPause(bool show)
        {
            if (pauseScreen != null) pauseScreen.SetActive(show);
        }

        public void ShowResults(bool show)
        {
            if (resultsScreen != null) resultsScreen.SetActive(show);
        }
    }
}