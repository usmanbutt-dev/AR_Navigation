using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Nibrask.Core;

namespace Nibrask.UI
{
    /// <summary>
    /// Screen-space notification system for navigation feedback.
    /// Displays toast-style messages for events like off-route warnings,
    /// route recalculations, and checkpoint progress.
    /// </summary>
    public class FeedbackPanel : MonoBehaviour
    {
        [Header("Toast Notification")]
        [SerializeField]
        [Tooltip("The toast notification container")]
        private RectTransform toastContainer;

        [SerializeField]
        private Image toastBackground;

        [SerializeField]
        private TextMeshProUGUI toastText;

        [SerializeField]
        private Image toastIcon;

        [Header("Progress Bar")]
        [SerializeField]
        [Tooltip("Navigation progress bar")]
        private Image progressBarFill;

        [SerializeField]
        private TextMeshProUGUI progressText;

        [Header("Off-Route Warning")]
        [SerializeField]
        [Tooltip("Full-width warning bar shown when off route")]
        private GameObject offRouteWarning;

        [SerializeField]
        private TextMeshProUGUI offRouteText;

        [Header("Colors")]
        [SerializeField]
        private Color successColor = new Color(0.0f, 0.75f, 0.35f, 0.9f);

        [SerializeField]
        private Color warningColor = new Color(1.0f, 0.6f, 0.0f, 0.9f);

        [SerializeField]
        private Color dangerColor = new Color(0.9f, 0.2f, 0.15f, 0.9f);

        [SerializeField]
        private Color infoColor = new Color(0.1f, 0.5f, 0.9f, 0.9f);

        [Header("Animation")]
        [SerializeField]
        private float toastShowDuration = 3f;

        [SerializeField]
        private float slideSpeed = 800f;

        [Header("References")]
        [SerializeField]
        [Tooltip("Assign the NavigationManager from the scene — avoids expensive FindAnyObjectByType calls (Fix #10)")]
        private Nibrask.Navigation.NavigationManager navigationManager;

        private Queue<ToastMessage> messageQueue = new Queue<ToastMessage>();
        private bool isShowingToast = false;
        private int totalCheckpoints = 0;
        private int passedCheckpoints = 0;

        private struct ToastMessage
        {
            public string text;
            public Color color;
            public float duration;
        }

        private void OnEnable()
        {
            AppEvents.OnNavigationStarted += HandleNavigationStarted;
            AppEvents.OnCheckpointReached += HandleCheckpointReached;
            AppEvents.OnOffRoute += HandleOffRoute;
            AppEvents.OnBackOnRoute += HandleBackOnRoute;
            AppEvents.OnRouteRecalculated += HandleRouteRecalculated;
            AppEvents.OnRecalculationFailed += HandleRecalculationFailed; // Fix #11
            AppEvents.OnArrived += HandleArrived;

            if (AppStateManager.Instance != null)
                AppStateManager.Instance.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            AppEvents.OnNavigationStarted -= HandleNavigationStarted;
            AppEvents.OnCheckpointReached -= HandleCheckpointReached;
            AppEvents.OnOffRoute -= HandleOffRoute;
            AppEvents.OnBackOnRoute -= HandleBackOnRoute;
            AppEvents.OnRouteRecalculated -= HandleRouteRecalculated;
            AppEvents.OnRecalculationFailed -= HandleRecalculationFailed; // Fix #11
            AppEvents.OnArrived -= HandleArrived;

            if (AppStateManager.Instance != null)
                AppStateManager.Instance.OnStateChanged -= HandleStateChanged;
        }

        private void Start()
        {
            if (AppStateManager.Instance != null)
            {
                AppStateManager.Instance.OnStateChanged -= HandleStateChanged;
                AppStateManager.Instance.OnStateChanged += HandleStateChanged;
            }

            if (navigationManager == null)
            {
                Debug.LogWarning("[FeedbackPanel] navigationManager is not assigned in the Inspector. " +
                    "Progress bar and checkpoint counts will not work correctly.");
            }
        }

        /// <summary>
        /// Enqueues a toast notification to be shown.
        /// </summary>
        public void ShowToast(string message, Color color, float duration = -1f)
        {
            if (duration < 0f) duration = toastShowDuration;

            messageQueue.Enqueue(new ToastMessage
            {
                text = message,
                color = color,
                duration = duration
            });

            if (!isShowingToast)
            {
                StartCoroutine(ProcessToastQueue());
            }
        }

        /// <summary>
        /// Processes the toast message queue one at a time.
        /// </summary>
        private IEnumerator ProcessToastQueue()
        {
            isShowingToast = true;

            while (messageQueue.Count > 0)
            {
                var msg = messageQueue.Dequeue();
                yield return StartCoroutine(ShowToastAnimation(msg));
            }

            isShowingToast = false;
        }

        /// <summary>
        /// Animates a single toast message (slide in → hold → slide out).
        /// </summary>
        private IEnumerator ShowToastAnimation(ToastMessage msg)
        {
            if (toastContainer == null) yield break;

            // Setup
            if (toastText != null) toastText.text = msg.text;
            if (toastBackground != null) toastBackground.color = msg.color;
            toastContainer.gameObject.SetActive(true);

            // Slide in from top
            Vector2 hiddenPos = new Vector2(0f, 120f);
            Vector2 shownPos = new Vector2(0f, -20f);
            toastContainer.anchoredPosition = hiddenPos;

            float elapsed = 0f;
            float slideDuration = 0.3f;

            while (elapsed < slideDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
                toastContainer.anchoredPosition = Vector2.Lerp(hiddenPos, shownPos, t);
                yield return null;
            }

            // Hold
            yield return new WaitForSeconds(msg.duration);

            // Slide out
            elapsed = 0f;
            while (elapsed < slideDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
                toastContainer.anchoredPosition = Vector2.Lerp(shownPos, hiddenPos, t);
                yield return null;
            }

            toastContainer.gameObject.SetActive(false);
        }

        /// <summary>
        /// Updates the progress bar.
        /// </summary>
        private void UpdateProgressBar()
        {
            if (totalCheckpoints <= 0) return;

            float progress = (float)passedCheckpoints / totalCheckpoints;

            if (progressBarFill != null)
                progressBarFill.fillAmount = progress;

            if (progressText != null)
                progressText.text = $"{passedCheckpoints}/{totalCheckpoints}";
        }

        // ── Event Handlers ─────────────────────────────────────────────

        private void HandleNavigationStarted()
        {
            if (toastContainer != null) toastContainer.gameObject.SetActive(true);
            if (progressBarFill != null) progressBarFill.transform.parent.gameObject.SetActive(true);
            passedCheckpoints = 0;

            // Use the serialized reference instead of the expensive FindAnyObjectByType (Fix #10)
            if (navigationManager != null && navigationManager.CurrentPath != null)
                totalCheckpoints = navigationManager.CurrentPath.Count;

            UpdateProgressBar();
            ShowToast("Navigation started", successColor, 2f);

            if (offRouteWarning != null)
                offRouteWarning.SetActive(false);
        }

        private void HandleCheckpointReached(int index)
        {
            passedCheckpoints = index + 1;
            UpdateProgressBar();
            ShowToast($"Checkpoint {passedCheckpoints}/{totalCheckpoints}", successColor, 1.5f);
        }

        private void HandleOffRoute()
        {
            ShowToast("You are off route!", warningColor);

            if (offRouteWarning != null)
            {
                offRouteWarning.SetActive(true);
                if (offRouteText != null)
                    offRouteText.text = "Off route — recalculating...";
            }
        }

        private void HandleBackOnRoute()
        {
            ShowToast("Back on route", successColor, 2f);

            if (offRouteWarning != null)
                offRouteWarning.SetActive(false);
        }

        private void HandleRouteRecalculated()
        {
            ShowToast("Route recalculated", infoColor, 2f);

            if (offRouteWarning != null)
                offRouteWarning.SetActive(false);
        }

        private void HandleRecalculationFailed()
        {
            // Fix #11: Clear the off-route warning when recalculation fails so the UI doesn't
            // get permanently stuck showing the warning banner with no follow-up feedback.
            ShowToast("Could not find a path. Try moving closer to the route.", warningColor, 4f);

            if (offRouteWarning != null)
            {
                if (offRouteText != null)
                    offRouteText.text = "No path found — please move to a different area";
            }
        }

        private void HandleArrived(Data.DestinationData destination)
        {
            ShowToast("You have arrived!", successColor, 4f);

            if (offRouteWarning != null)
                offRouteWarning.SetActive(false);
        }

         private void HandleStateChanged(AppState oldState, AppState newState)
        {
            switch (newState)
            {
                case AppState.Navigating:
                    // Children will be shown by HandleNavigationStarted
                    break;

                case AppState.Arrival:
                    // Keep showing for arrival notification
                    break;

                default:
                    StopAllCoroutines();
                    isShowingToast = false;
                    messageQueue.Clear();
                    // Hide visual children but keep GO active for subscriptions
                    if (toastContainer != null) toastContainer.gameObject.SetActive(false);
                    if (offRouteWarning != null) offRouteWarning.SetActive(false);
                    break;
            }
        }
    }
}
