using System.Collections.Generic;
using UnityEngine;
using Nibrask.Core;
using Nibrask.Data;

namespace Nibrask.Navigation
{
    /// <summary>
    /// Central navigation controller. Receives the selected destination, computes the route
    /// using PathFinder, spawns the visual path and arrows, monitors the user's progress,
    /// and handles route recalculation when the user deviates from the path.
    /// </summary>
    public class NavigationManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private WaypointGraph waypointGraph;

        [SerializeField]
        private PathRenderer pathRenderer;

        [SerializeField]
        private ArrowGenerator arrowGenerator;

        [SerializeField]
        private DistanceTracker distanceTracker;

        [Header("Settings")]
        [SerializeField]
        [Tooltip("Cooldown between route recalculations (seconds)")]
        private float recalculationCooldown = 1.5f;

        [Header("Debug")]
        [SerializeField]
        private bool isNavigating = false;

        /// <summary>
        /// The current computed navigation path.
        /// </summary>
        public List<WaypointNode> CurrentPath { get; private set; }

        /// <summary>
        /// The currently active destination.
        /// </summary>
        public DestinationData CurrentDestination { get; private set; }

        /// <summary>
        /// Whether navigation is currently active.
        /// </summary>
        public bool IsNavigating => isNavigating;

        private Transform userTransform;
        private float lastRecalculationTime;

        private void Start()
        {
            userTransform = Camera.main?.transform;

            // Fallback subscription: OnEnable might run before AppStateManager.Awake() sets Instance
            if (AppStateManager.Instance != null)
            {
                AppStateManager.Instance.OnStateChanged -= HandleStateChanged;
                AppStateManager.Instance.OnStateChanged += HandleStateChanged;
            }
        }

        private void OnEnable()
        {
            if (AppStateManager.Instance != null)
                AppStateManager.Instance.OnStateChanged += HandleStateChanged;

            AppEvents.OnDestinationSelected += HandleDestinationSelected;
            AppEvents.OnOffRoute += HandleOffRoute;
            AppEvents.OnArrived += HandleArrived;
        }

        private void OnDisable()
        {
            if (AppStateManager.Instance != null)
                AppStateManager.Instance.OnStateChanged -= HandleStateChanged;

            AppEvents.OnDestinationSelected -= HandleDestinationSelected;
            AppEvents.OnOffRoute -= HandleOffRoute;
            AppEvents.OnArrived -= HandleArrived;
        }

        private void Update()
        {
            if (!isNavigating || userTransform == null) return;

            // Update the start of the visible path to follow user
            pathRenderer?.UpdateStartPosition(userTransform.position);
        }

        /// <summary>
        /// Handles state changes from the AppStateManager.
        /// </summary>
        private void HandleStateChanged(AppState oldState, AppState newState)
        {
            switch (newState)
            {
                case AppState.Navigating:
                    // Start navigation using the destination stored by AppStateManager.
                    // This is safer than relying on OnDestinationSelected handler ordering.
                    var dest = AppStateManager.Instance?.SelectedDestination;
                    if (dest != null)
                        StartNavigation(dest);
                    else
                        Debug.LogError("[NavigationManager] Navigating state reached but SelectedDestination is null.");
                    break;

                case AppState.Arrival:
                case AppState.DestinationSelection:
                case AppState.Onboarding:
                case AppState.Scanning:
                    StopNavigation();
                    break;
            }
        }

        /// <summary>
        /// Handles destination selection — stores destination for reference but does NOT start
        /// navigation here. Navigation is started in HandleStateChanged(Navigating) to avoid
        /// fragile event ordering dependencies.
        /// </summary>
        private void HandleDestinationSelected(DestinationData destination)
        {
            CurrentDestination = destination;
            // Navigation will be started when AppStateManager transitions to Navigating state.
        }

        /// <summary>
        /// Starts navigation to the specified destination.
        /// </summary>
        public void StartNavigation(DestinationData destination)
        {
            if (waypointGraph == null || !waypointGraph.IsBuilt)
            {
                Debug.LogError("[NavigationManager] Waypoint graph is not built.");
                return;
            }

            if (userTransform == null)
            {
                userTransform = Camera.main?.transform;
                if (userTransform == null)
                {
                    Debug.LogError("[NavigationManager] No camera/user transform available.");
                    return;
                }
            }

            // Find the nearest waypoint to the user
            WaypointNode startNode = waypointGraph.FindNearestNode(userTransform.position);
            if (startNode == null)
            {
                Debug.LogError("[NavigationManager] Could not find start node near user.");
                return;
            }

            // Find the waypoint node associated with the destination
            WaypointNode endNode = waypointGraph.GetNodeForDestination(destination);
            if (endNode == null)
            {
                Debug.LogError($"[NavigationManager] Could not find node for destination '{destination.destinationName}'.");
                return;
            }

            // Compute path using A*
            CurrentPath = PathFinder.FindPath(startNode, endNode);

            if (CurrentPath == null || CurrentPath.Count == 0)
            {
                Debug.LogError($"[NavigationManager] No path found to '{destination.destinationName}'.");
                return;
            }

            float totalDistance = PathFinder.CalculatePathDistance(CurrentPath);
            Debug.Log($"[NavigationManager] Path computed: {CurrentPath.Count} waypoints, {totalDistance:F1}m total distance.");

            // Render the path and arrows
            pathRenderer?.RenderPath(CurrentPath);
            arrowGenerator?.GenerateArrows(CurrentPath);

            // Start tracking the user's progress, passing destination explicitly for safe arrival
            distanceTracker?.StartTracking(CurrentPath, destination);

            isNavigating = true;
            lastRecalculationTime = Time.time;
            AppEvents.RaiseNavigationStarted();
        }

        /// <summary>
        /// Handles off-route event — recalculates the route from the user's current position.
        /// </summary>
        private void HandleOffRoute()
        {
            if (!isNavigating || CurrentDestination == null)
                return;

            // Enforce cooldown to prevent rapid recalculations
            if (Time.time - lastRecalculationTime < recalculationCooldown)
                return;

            Debug.Log("[NavigationManager] Recalculating route...");
            RecalculateRoute();
        }

        /// <summary>
        /// Recalculates the navigation route from the user's current position.
        /// </summary>
        public void RecalculateRoute()
        {
            if (CurrentDestination == null || waypointGraph == null || userTransform == null)
                return;

            WaypointNode startNode = waypointGraph.FindNearestNode(userTransform.position);
            WaypointNode endNode = waypointGraph.GetNodeForDestination(CurrentDestination);

            if (startNode == null || endNode == null) return;

            CurrentPath = PathFinder.FindPath(startNode, endNode);

            if (CurrentPath == null || CurrentPath.Count == 0)
            {
                Debug.LogWarning("[NavigationManager] Route recalculation failed — no path found.");
                return;
            }

            // Update visuals
            pathRenderer?.RenderPath(CurrentPath);
            arrowGenerator?.GenerateArrows(CurrentPath);
            distanceTracker?.UpdatePath(CurrentPath);

            lastRecalculationTime = Time.time;
            AppEvents.RaiseRouteRecalculated();

            Debug.Log($"[NavigationManager] Route recalculated: {CurrentPath.Count} waypoints.");
        }

        /// <summary>
        /// Handles arrival at the destination.
        /// </summary>
        private void HandleArrived(DestinationData destination)
        {
            Debug.Log($"[NavigationManager] Arrived at '{destination?.destinationName}'.");
            StopNavigation();
        }

        /// <summary>
        /// Stops all navigation visuals and tracking.
        /// </summary>
        public void StopNavigation()
        {
            isNavigating = false;
            CurrentPath = null;
            CurrentDestination = null;

            pathRenderer?.ClearPath();
            arrowGenerator?.ClearArrows();
            distanceTracker?.StopTracking();
        }
    }
}
