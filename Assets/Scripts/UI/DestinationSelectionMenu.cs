using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Nibrask.Core;
using Nibrask.Data;

namespace Nibrask.UI
{
    /// <summary>
    /// World-space floating menu for selecting a destination in the airport terminal.
    /// Displays categorized destinations (Gates, Services, Exits) from TerminalMapData
    /// with icons and flight info. Positioned in front of the camera with billboard behavior.
    /// </summary>
    public class DestinationSelectionMenu : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("The parent container for destination buttons")]
        private Transform buttonContainer;

        [SerializeField]
        [Tooltip("Prefab for each destination button")]
        private GameObject destinationButtonPrefab;

        [SerializeField]
        [Tooltip("Canvas group for fade animations")]
        private CanvasGroup canvasGroup;

        [Header("UI Elements")]
        [SerializeField]
        private TextMeshProUGUI headerText;

        [SerializeField]
        private TextMeshProUGUI categoryLabelGates;

        [SerializeField]
        private TextMeshProUGUI categoryLabelServices;

        [Header("Positioning")]
        [SerializeField]
        [Tooltip("Distance from camera to spawn the menu (meters)")]
        private float distanceFromCamera = 1.5f;

        [SerializeField]
        [Tooltip("Height offset from camera (meters)")]
        private float heightOffset = -0.2f;

        [Header("Animation")]
        [SerializeField]
        private float fadeSpeed = 3f;

        [SerializeField]
        private float scaleAnimationSpeed = 5f;

        private List<GameObject> spawnedButtons = new List<GameObject>();
        private bool isVisible = false;
        private float targetAlpha = 0f;
        private Vector3 targetScale = Vector3.zero;

        private void Awake()
        {
            // Start hidden visually but keep GameObject active so OnEnable/Start subscriptions work
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            transform.localScale = Vector3.zero;
        }

        private void OnEnable()
        {
            if (AppStateManager.Instance != null)
                AppStateManager.Instance.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
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
        }

        private void Update()
        {
            // Animate alpha
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
                canvasGroup.interactable = canvasGroup.alpha > 0.5f;
                canvasGroup.blocksRaycasts = canvasGroup.alpha > 0.5f;
            }

            // Animate scale
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleAnimationSpeed);
        }

        /// <summary>
        /// Shows the destination selection menu with all available destinations.
        /// Positions the menu in front of the camera.
        /// </summary>
        public void Show()
        {
            if (isVisible) return;

            var mapData = AppStateManager.Instance?.TerminalMap;
            if (mapData == null)
            {
                Debug.LogWarning("[DestinationSelectionMenu] No terminal map data available.");
                return;
            }

            // Position menu in front of the camera
            PositionInFrontOfCamera();

            // Clear existing buttons
            ClearButtons();

            // Set header
            if (headerText != null)
                headerText.text = "Select Destination";

            // Create buttons for each destination, grouped by type
            var gates = new List<DestinationData>();
            var services = new List<DestinationData>();

            foreach (var dest in mapData.destinations)
            {
                if (dest.destinationType == DestinationType.Gate)
                    gates.Add(dest);
                else
                    services.Add(dest);
            }

            // Add gate category label
            if (categoryLabelGates != null)
            {
                // Always set explicitly so stale labels from a previous Show() are cleared (Fix #12)
                categoryLabelGates.gameObject.SetActive(gates.Count > 0);
                if (gates.Count > 0)
                    categoryLabelGates.text = $"🛫 Gates ({gates.Count})";
            }

            foreach (var gate in gates)
            {
                CreateDestinationButton(gate);
            }

            // Add services category label
            if (categoryLabelServices != null)
            {
                // Always set explicitly so stale labels from a previous Show() are cleared (Fix #12)
                categoryLabelServices.gameObject.SetActive(services.Count > 0);
                if (services.Count > 0)
                    categoryLabelServices.text = $"📍 Services ({services.Count})";
            }

            foreach (var service in services)
            {
                CreateDestinationButton(service);
            }

            // Animate in
            isVisible = true;
            targetAlpha = 1f;
            targetScale = Vector3.one;
            transform.localScale = Vector3.one * 0.5f;

            Debug.Log($"[DestinationSelectionMenu] Showing {mapData.destinations.Count} destinations.");
        }

        /// <summary>
        /// Hides the destination selection menu with animation.
        /// </summary>
        public void Hide()
        {
            if (!isVisible) return;

            isVisible = false;
            targetAlpha = 0f;
            targetScale = Vector3.one * 0.8f;

            CancelInvoke(nameof(DeactivateSelf)); // Prevent stale timers hiding re-shown menu (Fix #5)
            Invoke(nameof(DeactivateSelf), 0.5f);
        }

        /// <summary>
        /// Positions the menu in front of the AR camera.
        /// Billboard component handles continuous rotation in LateUpdate.
        /// </summary>
        private void PositionInFrontOfCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            forward.Normalize();

            transform.position = cam.transform.position + forward * distanceFromCamera + Vector3.up * heightOffset;

            // +forward makes +Z point away from camera → UI (-Z local) faces camera
            transform.rotation = Quaternion.LookRotation(forward);
        }

        /// <summary>
        /// Creates a button for a single destination.
        /// </summary>
        private void CreateDestinationButton(DestinationData destination)
        {
            if (buttonContainer == null) return;

            GameObject buttonGo;

            if (destinationButtonPrefab != null)
            {
                buttonGo = Instantiate(destinationButtonPrefab, buttonContainer);
            }
            else
            {
                // Fallback: create a simple button
                buttonGo = CreateFallbackButton(destination);
            }

            // Configure the button text
            var buttonText = buttonGo.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                string displayText = destination.GetDisplayName();
                if (destination.destinationType != DestinationType.Gate)
                {
                    displayText = $"{destination.GetTypeLabel()}\n{destination.destinationName}";
                }
                else
                {
                    displayText = $"🛫 {destination.destinationName}";
                    if (!string.IsNullOrEmpty(destination.flightNumber))
                        displayText += $"\n{destination.flightNumber}";
                    if (!string.IsNullOrEmpty(destination.boardingTime))
                        displayText += $" • {destination.boardingTime}";
                }
                buttonText.text = displayText;
            }

            // Configure the button icon
            if (destination.icon != null)
            {
                var iconImage = buttonGo.transform.Find("Icon")?.GetComponent<Image>();
                if (iconImage != null)
                    iconImage.sprite = destination.icon;
            }

            // Add click handler
            var button = buttonGo.GetComponent<Button>();
            if (button != null)
            {
                var dest = destination; // Capture for closure
                button.onClick.AddListener(() => OnDestinationClicked(dest));
            }

            spawnedButtons.Add(buttonGo);
        }

        /// <summary>
        /// Creates a simple fallback button when no prefab is assigned.
        /// </summary>
        private GameObject CreateFallbackButton(DestinationData destination)
        {
            var buttonGo = new GameObject($"Btn_{destination.destinationName}");
            buttonGo.transform.SetParent(buttonContainer, false);

            // Add RectTransform
            var rect = buttonGo.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300f, 60f);

            // Add Image background
            var bg = buttonGo.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.2f, 0.85f);

            // Add Button
            var button = buttonGo.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.0f, 0.6f, 0.3f, 0.9f);
            colors.pressedColor = new Color(0.0f, 0.8f, 0.4f, 1.0f);
            button.colors = colors;

            // Add Text
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(buttonGo.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 5f);
            textRect.offsetMax = new Vector2(-10f, -5f);

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 14f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;

            return buttonGo;
        }

        /// <summary>
        /// Handles a destination button click.
        /// </summary>
        private void OnDestinationClicked(DestinationData destination)
        {
            Debug.Log($"[DestinationSelectionMenu] Selected: {destination.destinationName}");
            Hide();
            AppEvents.RaiseDestinationSelected(destination);
        }

        /// <summary>
        /// Removes all spawned buttons.
        /// </summary>
        private void ClearButtons()
        {
            foreach (var btn in spawnedButtons)
            {
                if (btn != null) Destroy(btn);
            }
            spawnedButtons.Clear();
        }

        /// <summary>
        /// Reacts to global state changes.
        /// </summary>
        private void HandleStateChanged(AppState oldState, AppState newState)
        {
            switch (newState)
            {
                case AppState.DestinationSelection:
                    Show();
                    break;

                default:
                    Hide();
                    break;
            }
        }

        private void DeactivateSelf()
        {
            if (!isVisible)
            {
                // Visually hide but keep GO active so subscriptions stay alive
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 0f;
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                }
                transform.localScale = Vector3.zero;
            }
        }
    }
}
