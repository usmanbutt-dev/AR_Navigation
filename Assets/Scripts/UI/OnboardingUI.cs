using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Nibrask.Core;

namespace Nibrask.UI
{
    /// <summary>
    /// Manages the onboarding screen overlay — the first thing the user sees.
    /// Displays the app name, instructions, and a "Start Scanning" button.
    /// Also shows scanning progress once the user begins floor detection.
    /// </summary>
    public class OnboardingUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField]
        [Tooltip("The welcome panel shown initially")]
        private GameObject welcomePanel;

        [SerializeField]
        [Tooltip("The scanning instructions panel")]
        private GameObject scanningPanel;

        [Header("Welcome Panel Elements")]
        [SerializeField]
        private TextMeshProUGUI titleText;

        [SerializeField]
        private TextMeshProUGUI subtitleText;

        [SerializeField]
        private TextMeshProUGUI instructionText;

        [SerializeField]
        private Button startButton;

        [Header("Scanning Panel Elements")]
        [SerializeField]
        private TextMeshProUGUI scanningStatusText;

        [SerializeField]
        private Image scanningProgressFill;

        [SerializeField]
        private TextMeshProUGUI scanningInstructionText;

        [Header("Animation")]
        [SerializeField]
        private CanvasGroup canvasGroup;

        [SerializeField]
        private float fadeSpeed = 2f;

        private bool isScanning = false;
        private bool floorFound = false;
        private float scanProgress = 0f;
        private float targetAlpha = 1f;

        private void OnEnable()
        {
            // Subscribe if AppStateManager already exists (re-enables, mid-session)
            if (AppStateManager.Instance != null)
                AppStateManager.Instance.OnStateChanged += HandleStateChanged;

            AppEvents.OnFloorDetected += HandleFloorDetected;

            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnStartButtonClicked);
                startButton.onClick.AddListener(OnStartButtonClicked);
            }
        }

        private void OnDisable()
        {
            if (AppStateManager.Instance != null)
                AppStateManager.Instance.OnStateChanged -= HandleStateChanged;

            AppEvents.OnFloorDetected -= HandleFloorDetected;

            if (startButton != null)
                startButton.onClick.RemoveListener(OnStartButtonClicked);
        }

        private void Start()
        {
            SetupWelcomePanel();

            // CRITICAL: By Start(), AppStateManager is guaranteed to have run Awake().
            // If Instance was null during OnEnable() we missed the subscription — do it now.
            if (AppStateManager.Instance != null)
            {
                // Re-subscribe safely (removing first prevents duplicates if OnEnable also subscribed)
                AppStateManager.Instance.OnStateChanged -= HandleStateChanged;
                AppStateManager.Instance.OnStateChanged += HandleStateChanged;

                // Sync to current state (handles fast transitions or missed events)
                HandleStateChanged(AppState.Onboarding, AppStateManager.Instance.CurrentState);
            }
            else
            {
                Debug.LogError("[OnboardingUI] AppStateManager.Instance is null in Start(). " +
                    "Ensure AppStateManager is in the scene and its Awake() runs before OnboardingUI.Start().");
            }
        }

        private void Update()
        {
            // Animate canvas alpha
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
            }

            // Animate scanning progress
            if (isScanning && !floorFound)
            {
                scanProgress += Time.deltaTime * 0.15f; // Slow progress animation
                scanProgress = Mathf.Min(scanProgress, 0.8f); // Cap at 80% until floor is detected

                if (scanningProgressFill != null)
                    scanningProgressFill.fillAmount = scanProgress;

                if (scanningStatusText != null)
                    scanningStatusText.text = $"Scanning... {Mathf.RoundToInt(scanProgress * 100)}%";
            }
        }

        /// <summary>
        /// Sets up the initial welcome panel with app branding.
        /// </summary>
        private void SetupWelcomePanel()
        {
            if (titleText != null)
                titleText.text = "Nibrāsk";

            if (subtitleText != null)
                subtitleText.text = "AR Navigation Assistant";

            if (instructionText != null)
                instructionText.text = "Navigate airport terminals\nwith augmented reality guidance";

            if (startButton != null)
            {
                var buttonText = startButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                    buttonText.text = "Start Scanning";
            }
            else
            {
                Debug.LogError("[OnboardingUI] startButton is NOT assigned in the Inspector! The user cannot start scanning.");
            }

            ShowWelcome();
        }

        /// <summary>
        /// Shows the welcome panel and hides the scanning panel.
        /// </summary>
        private void ShowWelcome()
        {
            if (welcomePanel != null) welcomePanel.SetActive(true);
            if (scanningPanel != null) scanningPanel.SetActive(false);
            targetAlpha = 1f;
        }

        /// <summary>
        /// Shows the scanning panel and hides the welcome panel.
        /// </summary>
        private void ShowScanning()
        {
            Debug.Log($"[OnboardingUI] ShowScanning() called. welcomePanel={welcomePanel != null}, scanningPanel={scanningPanel != null}");

            if (welcomePanel != null) welcomePanel.SetActive(false);
            if (scanningPanel != null) scanningPanel.SetActive(true);

            if (scanningPanel == null)
            {
                Debug.LogError("[OnboardingUI] scanningPanel is NOT assigned in the Inspector! " +
                    "Assign the scanning panel GameObject in the OnboardingUI component.");
            }

            if (scanningInstructionText != null)
                scanningInstructionText.text = "Point your phone at the floor\nand move it slowly around";

            if (scanningStatusText != null)
                scanningStatusText.text = "Scanning... 0%";

            if (scanningProgressFill != null)
                scanningProgressFill.fillAmount = 0f;

            isScanning = true;
            scanProgress = 0f;
            floorFound = false;
        }

        /// <summary>
        /// Called when the Start button is clicked.
        /// </summary>
        private void OnStartButtonClicked()
        {
            Debug.Log("[OnboardingUI] OnStartButtonClicked! Emitting AppEvents.RaiseOnboardingComplete().");
            AppEvents.RaiseOnboardingComplete();
        }

        /// <summary>
        /// Called when the floor is detected during scanning.
        /// </summary>
        private void HandleFloorDetected()
        {
            floorFound = true;

            if (scanningStatusText != null)
                scanningStatusText.text = "Floor detected! ✓\nTap the floor to set your position.";

            if (scanningProgressFill != null)
                scanningProgressFill.fillAmount = 1f;

            if (scanningInstructionText != null)
                scanningInstructionText.text = "Tap the floor to place the terminal map origin";
        }

        /// <summary>
        /// Reacts to global state changes.
        /// </summary>
        private void HandleStateChanged(AppState oldState, AppState newState)
        {
            switch (newState)
            {
                case AppState.Onboarding:
                    gameObject.SetActive(true);
                    ShowWelcome();
                    break;

                case AppState.Scanning:
                    gameObject.SetActive(true);
                    Debug.Log("[OnboardingUI] HandleStateChanged → Scanning. Calling ShowScanning().");
                    ShowScanning();
                    break;

                case AppState.DestinationSelection:
                case AppState.Navigating:
                case AppState.Arrival:
                    // Fade out and hide
                    targetAlpha = 0f;
                    CancelInvoke(nameof(HideSelf)); // Prevent stacking timers (Fix #5)
                    Invoke(nameof(HideSelf), 0.5f);
                    break;
            }
        }

        private void HideSelf()
        {
            gameObject.SetActive(false);
            isScanning = false;
        }
    }
}
