using UnityEngine;
using TMPro;
using Nibrask.Core;
using Nibrask.Data;

namespace Nibrask.UI
{
    /// <summary>
    /// World-space floating information panel displayed during navigation.
    /// Shows destination name, gate number, boarding time, remaining distance,
    /// and estimated walking time. Updates dynamically as the user moves.
    /// Positioned as a floating HUD that follows the camera with a slight offset.
    /// </summary>
    public class GateInfoPanel : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField]
        private TextMeshProUGUI destinationNameText;

        [SerializeField]
        private TextMeshProUGUI flightInfoText;

        [SerializeField]
        private TextMeshProUGUI distanceText;

        [SerializeField]
        private TextMeshProUGUI walkingTimeText;

        [SerializeField]
        private TextMeshProUGUI statusText;

        [Header("Panel")]
        [SerializeField]
        private CanvasGroup canvasGroup;

        [SerializeField]
        private GameObject panelRoot;

        [Header("Positioning")]
        [SerializeField]
        [Tooltip("Offset from camera position")]
        private Vector3 cameraOffset = new Vector3(0f, -0.1f, 0.8f);

        [SerializeField]
        [Tooltip("Smooth follow speed")]
        private float followSpeed = 3f;

        [SerializeField]
        [Tooltip("Whether the panel follows the camera or stays world-anchored")]
        private bool followCamera = true;

        [Header("Animation")]
        [SerializeField]
        private float fadeSpeed = 3f;

        private float targetAlpha = 0f;
        private DestinationData currentDestination;
        private bool isMinimized = false;
        private Vector3 targetPosition;

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

            // Note: OnDestinationSelected subscription removed — Show() is triggered exclusively
            // from HandleStateChanged(Navigating) to prevent being called twice (Fix #6)
            AppEvents.OnDistanceUpdated += HandleDistanceUpdated;
            AppEvents.OnOffRoute += HandleOffRoute;
            AppEvents.OnBackOnRoute += HandleBackOnRoute;
            AppEvents.OnRouteRecalculated += HandleRouteRecalculated;
            AppEvents.OnRecalculationFailed += HandleRecalculationFailed; // Fix #11
        }

        private void OnDisable()
        {
            if (AppStateManager.Instance != null)
                AppStateManager.Instance.OnStateChanged -= HandleStateChanged;

            AppEvents.OnDistanceUpdated -= HandleDistanceUpdated;
            AppEvents.OnOffRoute -= HandleOffRoute;
            AppEvents.OnBackOnRoute -= HandleBackOnRoute;
            AppEvents.OnRouteRecalculated -= HandleRouteRecalculated;
            AppEvents.OnRecalculationFailed -= HandleRecalculationFailed; // Fix #11
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

            // Follow camera
            if (followCamera && targetAlpha > 0f)
            {
                UpdatePosition();
            }
        }

        /// <summary>
        /// Smoothly follows the camera with an offset.
        /// Uses transform.position which Unity handles correctly even on
        /// children of scaled parents (Canvas_WorldSpace at 0.001 scale).
        /// </summary>
        private void UpdatePosition()
        {
            var cam = Camera.main;
            if (cam == null) return;

            // Calculate desired world position relative to camera
            Vector3 desiredWorldPos = cam.transform.TransformPoint(cameraOffset);

            // Smooth follow — transform.position handles parent scale internally
            transform.position = Vector3.Lerp(transform.position, desiredWorldPos, Time.deltaTime * followSpeed);

            // Face the camera
            Vector3 lookDir = cam.transform.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                // -lookDir makes +Z point AWAY from camera,
                // so the UI visible side (-Z local) faces the camera
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(-lookDir),
                    Time.deltaTime * followSpeed
                );
            }
        }

        /// <summary>
        /// Shows the panel with the specified destination info.
        /// </summary>
        public void Show(DestinationData destination)
        {
            currentDestination = destination;

            if (destinationNameText != null)
                destinationNameText.text = destination.destinationName;

            if (flightInfoText != null)
            {
                if (destination.destinationType == DestinationType.Gate)
                {
                    string flightInfo = "";
                    if (!string.IsNullOrEmpty(destination.flightNumber))
                        flightInfo += destination.flightNumber;
                    if (!string.IsNullOrEmpty(destination.boardingTime))
                        flightInfo += $"\nBoarding: {destination.boardingTime}";
                    if (!string.IsNullOrEmpty(destination.airlineName))
                        flightInfo += $"\n{destination.airlineName}";

                    flightInfoText.text = flightInfo;
                    flightInfoText.gameObject.SetActive(true);
                }
                else
                {
                    flightInfoText.text = destination.GetTypeLabel();
                    flightInfoText.gameObject.SetActive(true);
                }
            }

            if (distanceText != null)
                distanceText.text = "Calculating...";

            if (walkingTimeText != null)
                walkingTimeText.text = "Time: --:--";

            if (statusText != null)
            {
                statusText.text = "Following route";
                statusText.color = new Color(0.0f, 0.9f, 0.4f);
            }

            targetAlpha = 1f;

            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            // Snap position and rotation immediately
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 desiredWorldPos = cam.transform.TransformPoint(cameraOffset);
                transform.position = desiredWorldPos;

                // Snap rotation — face camera immediately
                Vector3 lookDir = cam.transform.position - transform.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(-lookDir);

                Debug.Log($"[GateInfoPanel] Show() — cam={cam.transform.position}, " +
                          $"offset={cameraOffset}, worldPos={desiredWorldPos}, " +
                          $"localPos={transform.localPosition}, rot={transform.eulerAngles}");
            }
        }

        /// <summary>
        /// Hides the panel with fade animation.
        /// </summary>
        public void Hide()
        {
            targetAlpha = 0f;
            currentDestination = null;
            CancelInvoke(nameof(DeactivateSelf));
            Invoke(nameof(DeactivateSelf), 0.5f);
        }

        /// <summary>
        /// Toggles the panel between full and minimized views.
        /// </summary>
        public void ToggleMinimize()
        {
            isMinimized = !isMinimized;

            if (flightInfoText != null)
                flightInfoText.gameObject.SetActive(!isMinimized);

            if (statusText != null)
                statusText.gameObject.SetActive(!isMinimized);
        }

        // ── Event Handlers ─────────────────────────────────────────────

        private void HandleStateChanged(AppState oldState, AppState newState)
        {
            switch (newState)
            {
                case AppState.Navigating:
                    if (AppStateManager.Instance?.SelectedDestination != null)
                        Show(AppStateManager.Instance.SelectedDestination);
                    break;

                default:
                    Hide();
                    break;
            }
        }

        // HandleDestinationSelected removed — Show() is now driven solely by HandleStateChanged
        // to prevent the panel from being positioned twice in a single frame (Fix #6)

        private void HandleDistanceUpdated(float distanceMeters, float estimatedTimeSeconds)
        {
            if (distanceText != null)
            {
                if (distanceMeters >= 1000f)
                    distanceText.text = $"Distance: {distanceMeters / 1000f:F1} km";
                else
                    distanceText.text = $"Distance: {distanceMeters:F0} m";
            }

            if (walkingTimeText != null)
            {
                int minutes = Mathf.FloorToInt(estimatedTimeSeconds / 60f);
                int seconds = Mathf.FloorToInt(estimatedTimeSeconds % 60f);

                if (minutes > 0)
                    walkingTimeText.text = $"Time: {minutes}m {seconds}s";
                else
                    walkingTimeText.text = $"Time: {seconds}s";
            }
        }

        private void HandleOffRoute()
        {
            if (statusText != null)
            {
                statusText.text = "Error: Off route — recalculating...";
                statusText.color = new Color(1.0f, 0.4f, 0.1f);
            }
        }

        private void HandleBackOnRoute()
        {
            if (statusText != null)
            {
                statusText.text = "Success: Back on route";
                statusText.color = new Color(0.0f, 0.9f, 0.4f);
            }
        }

        private void HandleRouteRecalculated()
        {
            if (statusText != null)
            {
                statusText.text = "Updating: Route updated";
                statusText.color = new Color(0.0f, 0.7f, 0.9f);

                // Reset to normal after a delay (cancel any pending reset first)
                CancelInvoke(nameof(ResetStatusText));
                Invoke(nameof(ResetStatusText), 2f);
            }
        }

        private void HandleRecalculationFailed()
        {
            // Fix #11: Reset status text when no path found so it isn't permanently stuck
            // on "Off route — recalculating..."
            if (statusText != null)
            {
                statusText.text = "Error: No path found";
                statusText.color = new Color(1.0f, 0.4f, 0.1f);
            }
        }

        private void ResetStatusText()
        {
            if (statusText != null)
            {
                statusText.text = "Following route";
                statusText.color = new Color(0.0f, 0.9f, 0.4f);
            }
        }

        private void DeactivateSelf()
        {
            if (targetAlpha <= 0f)
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
