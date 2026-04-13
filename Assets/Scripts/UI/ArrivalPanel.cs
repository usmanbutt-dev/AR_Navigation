using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Nibrask.Core;
using Nibrask.Data;

namespace Nibrask.UI
{
    /// <summary>
    /// World-space panel displayed when the user arrives at the destination.
    /// Shows a confirmation message with a checkmark animation and a button
    /// to navigate to a new destination.
    /// </summary>
    public class ArrivalPanel : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField]
        private TextMeshProUGUI arrivalTitleText;

        [SerializeField]
        private TextMeshProUGUI arrivalMessageText;

        [SerializeField]
        private TextMeshProUGUI destinationInfoText;

        [SerializeField]
        private Image checkmarkIcon;

        [SerializeField]
        private Button navigateAgainButton;

        [Header("Panel")]
        [SerializeField]
        private CanvasGroup canvasGroup;

        [Header("Positioning")]
        [SerializeField]
        private float distanceFromCamera = 1.2f;

        [Header("Animation")]
        [SerializeField]
        private float fadeSpeed = 2f;

        [SerializeField]
        private float checkmarkScaleSpeed = 3f;

        [SerializeField]
        private float autoDismissTime = 30f;

        private float targetAlpha = 0f;
        private float checkmarkTargetScale = 0f;
        private bool isShowing = false;

        private void Awake()
        {
            // Start visually hidden but keep GO active for subscriptions
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private void OnEnable()
        {
            if (AppStateManager.Instance != null)
                AppStateManager.Instance.OnStateChanged += HandleStateChanged;

            AppEvents.OnArrived += HandleArrived;

            if (navigateAgainButton != null)
            {
                navigateAgainButton.onClick.RemoveListener(OnNavigateAgainClicked);
                navigateAgainButton.onClick.AddListener(OnNavigateAgainClicked);
            }
        }

        private void OnDisable()
        {
            if (AppStateManager.Instance != null)
                AppStateManager.Instance.OnStateChanged -= HandleStateChanged;

            AppEvents.OnArrived -= HandleArrived;

            if (navigateAgainButton != null)
                navigateAgainButton.onClick.RemoveListener(OnNavigateAgainClicked);
        }

        private void Start()
        {
            if (AppStateManager.Instance != null)
            {
                AppStateManager.Instance.OnStateChanged -= HandleStateChanged;
                AppStateManager.Instance.OnStateChanged += HandleStateChanged;
            }
        }

        private void Update()
        {
            // Animate alpha
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
            }

            // Animate checkmark scale
            if (checkmarkIcon != null)
            {
                float currentScale = checkmarkIcon.transform.localScale.x;
                float newScale = Mathf.Lerp(currentScale, checkmarkTargetScale, Time.deltaTime * checkmarkScaleSpeed);
                checkmarkIcon.transform.localScale = Vector3.one * newScale;
            }
        }

        /// <summary>
        /// Shows the arrival confirmation panel.
        /// </summary>
        public void Show(DestinationData destination)
        {
            if (isShowing) return;

            isShowing = true;

            // Position in front of camera
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 forward = cam.transform.forward;
                forward.y = 0f;
                forward.Normalize();

                transform.position = cam.transform.position + forward * distanceFromCamera;

                // +forward makes +Z point away from camera → UI (-Z local) faces camera
                transform.rotation = Quaternion.LookRotation(forward);
            }

            // Set text content
            if (arrivalTitleText != null)
                arrivalTitleText.text = "You have arrived!";

            if (arrivalMessageText != null)
                arrivalMessageText.text = $"Welcome to";

            if (destinationInfoText != null)
            {
                string info = destination.destinationName;
                if (destination.destinationType == DestinationType.Gate
                    && !string.IsNullOrEmpty(destination.flightNumber))
                {
                    info += $"\nFlight: {destination.flightNumber}";
                    if (!string.IsNullOrEmpty(destination.boardingTime))
                        info += $" • Boarding: {destination.boardingTime}";
                }
                destinationInfoText.text = info;
            }

            // Configure navigate again button
            if (navigateAgainButton != null)
            {
                var buttonText = navigateAgainButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                    buttonText.text = "Navigate to another destination";
            }

            // Start animations
            targetAlpha = 1f;
            checkmarkTargetScale = 1f;

            if (checkmarkIcon != null)
                checkmarkIcon.transform.localScale = Vector3.zero;

            // Auto-dismiss after timeout
            CancelInvoke(nameof(AutoDismiss));
            Invoke(nameof(AutoDismiss), autoDismissTime);

            Debug.Log($"[ArrivalPanel] Showing arrival at '{destination.destinationName}'.");
        }

        /// <summary>
        /// Hides the panel with fade animation.
        /// </summary>
        public void Hide()
        {
            isShowing = false;
            targetAlpha = 0f;
            checkmarkTargetScale = 0f;
            CancelInvoke(nameof(AutoDismiss));
            Invoke(nameof(DeactivateSelf), 0.5f);
        }

        /// <summary>
        /// Called when the "Navigate Again" button is clicked.
        /// </summary>
        private void OnNavigateAgainClicked()
        {
            Hide();
            AppEvents.RaiseNavigateAgain();
        }

        /// <summary>
        /// Auto-dismiss callback — hides the panel after the timeout but does NOT
        /// silently re-enter the navigation flow. The user must press the button to continue.
        /// (Fix #8: was calling OnNavigateAgainClicked which fired RaiseNavigateAgain unexpectedly)
        /// </summary>
        private void AutoDismiss()
        {
            Hide();
        }

        /// <summary>
        /// Reacts to global state changes.
        /// </summary>
        private void HandleStateChanged(AppState oldState, AppState newState)
        {
            switch (newState)
            {
                case AppState.Arrival:
                    // Show is triggered by HandleArrived event
                    break;

                default:
                    if (isShowing) Hide();
                    break;
            }
        }

        /// <summary>
        /// Handles the arrived event.
        /// </summary>
        private void HandleArrived(DestinationData destination)
        {
            if (destination != null)
                Show(destination);
        }

        private void DeactivateSelf()
        {
            if (!isShowing)
            {
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 0f;
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                }
            }
        }
    }
}
