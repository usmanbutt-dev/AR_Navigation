using System;
using UnityEngine;
using Nibrask.Data;

namespace Nibrask.Core
{
    /// <summary>
    /// Defines the application states corresponding to the user journey steps.
    /// </summary>
    public enum AppState
    {
        /// <summary>Initial welcome screen with instructions.</summary>
        Onboarding,

        /// <summary>User is scanning the floor to detect AR planes.</summary>
        Scanning,

        /// <summary>User is choosing a destination from the floating menu.</summary>
        DestinationSelection,

        /// <summary>Active navigation with path, arrows, and info panel visible.</summary>
        Navigating,

        /// <summary>User has arrived at the destination.</summary>
        Arrival
    }

    /// <summary>
    /// Singleton manager controlling the global application state machine.
    /// Orchestrates transitions between Onboarding → Scanning → DestinationSelection → Navigating → Arrival.
    /// All other systems subscribe to state changes or AppEvents to react accordingly.
    /// </summary>
    public class AppStateManager : MonoBehaviour
    {
        public static AppStateManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Terminal map data defining the airport layout")]
        private TerminalMapData terminalMapData;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Current application state (read-only in inspector)")]
        private AppState currentState = AppState.Onboarding;

        /// <summary>
        /// Current application state.
        /// </summary>
        public AppState CurrentState => currentState;

        /// <summary>
        /// The loaded terminal map data.
        /// </summary>
        public TerminalMapData TerminalMap => terminalMapData;

        /// <summary>
        /// The currently selected destination (set during DestinationSelection).
        /// </summary>
        public DestinationData SelectedDestination { get; private set; }

        /// <summary>
        /// Event fired when the application state changes.
        /// Parameters: (oldState, newState)
        /// </summary>
        public event Action<AppState, AppState> OnStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[AppStateManager] Duplicate instance detected, destroying this one.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            // DontDestroyOnLoad requires a root-level GameObject.
            // Using transform.root ensures this works even if AppStateManager
            // is nested under a parent in the scene hierarchy.
            DontDestroyOnLoad(transform.root.gameObject);
        }

        private bool isQuitting = false;

        private void OnApplicationQuit()
        {
            isQuitting = true;
        }

        private void OnDestroy()
        {
            // Only clear static events when the app is truly shutting down.
            // During scene reloads, new objects may have already subscribed in
            // their OnEnable — calling ClearAll here would wipe those new
            // subscriptions. DontDestroyOnLoad keeps AppStateManager alive
            // across scenes anyway, so this only triggers on actual quit.
            if (Instance == this)
            {
                if (isQuitting)
                    AppEvents.ClearAll();
                Instance = null;
            }
        }

        private void OnEnable()
        {
            // Subscribe to events that trigger state transitions
            AppEvents.OnOnboardingComplete += HandleOnboardingComplete;
            AppEvents.OnFloorDetected += HandleFloorDetected;
            AppEvents.OnTerminalAnchorPlaced += HandleTerminalAnchorPlaced;
            AppEvents.OnDestinationSelected += HandleDestinationSelected;
            AppEvents.OnArrived += HandleArrived;
            AppEvents.OnNavigateAgain += HandleNavigateAgain;
        }

        private void OnDisable()
        {
            AppEvents.OnOnboardingComplete -= HandleOnboardingComplete;
            AppEvents.OnFloorDetected -= HandleFloorDetected;
            AppEvents.OnTerminalAnchorPlaced -= HandleTerminalAnchorPlaced;
            AppEvents.OnDestinationSelected -= HandleDestinationSelected;
            AppEvents.OnArrived -= HandleArrived;
            AppEvents.OnNavigateAgain -= HandleNavigateAgain;
        }

        private void Start()
        {
            // Begin in Onboarding state
            TransitionTo(AppState.Onboarding);
        }

        /// <summary>
        /// Performs a state transition with validation and event broadcasting.
        /// </summary>
        public void TransitionTo(AppState newState)
        {
            if (currentState == newState && newState != AppState.Onboarding) return;

            var oldState = currentState;
            currentState = newState;

            Debug.Log($"[AppStateManager] State transition: {oldState} → {newState}");
            OnStateChanged?.Invoke(oldState, newState);
        }

        // ── Event Handlers ─────────────────────────────────────────────

        private void HandleOnboardingComplete()
        {
            Debug.Log($"[AppStateManager] HandleOnboardingComplete received! Current state: {currentState}");
            if (currentState == AppState.Onboarding)
            {
                TransitionTo(AppState.Scanning);
            }
        }

        private void HandleFloorDetected()
        {
            // Floor detected during scanning — we'll wait for the anchor placement
            Debug.Log("[AppStateManager] Floor detected, waiting for anchor placement...");
        }

        private void HandleTerminalAnchorPlaced(Transform anchor)
        {
            if (currentState == AppState.Scanning)
            {
                TransitionTo(AppState.DestinationSelection);
            }
        }

        private void HandleDestinationSelected(DestinationData destination)
        {
            if (currentState == AppState.DestinationSelection)
            {
                SelectedDestination = destination;
                TransitionTo(AppState.Navigating);
            }
        }

        private void HandleArrived(DestinationData destination)
        {
            if (currentState == AppState.Navigating)
            {
                TransitionTo(AppState.Arrival);
            }
        }

        private void HandleNavigateAgain()
        {
            if (currentState == AppState.Arrival)
            {
                SelectedDestination = null;
                TransitionTo(AppState.DestinationSelection);
            }
        }
    }
}
