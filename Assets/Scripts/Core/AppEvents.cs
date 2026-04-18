using System;
using UnityEngine;
using Nibrask.Data;

namespace Nibrask.Core
{
    /// <summary>
    /// Static event bus for decoupled communication between all Nibrask systems.
    /// Components subscribe to events they care about without needing direct references.
    /// </summary>
    public static class AppEvents
    {
        // ── AR Environment Events ──────────────────────────────────────────

        /// <summary>
        /// Fired when a horizontal floor plane is detected for the first time.
        /// </summary>
        public static event Action OnFloorDetected;
        public static void RaiseFloorDetected() => OnFloorDetected?.Invoke();

        /// <summary>
        /// Fired when the terminal origin anchor has been placed in the world.
        /// Parameter: the world-space Transform of the anchor.
        /// </summary>
        public static event Action<Transform> OnTerminalAnchorPlaced;
        public static void RaiseTerminalAnchorPlaced(Transform anchor) => OnTerminalAnchorPlaced?.Invoke(anchor);

        // ── Navigation Events ──────────────────────────────────────────────

        /// <summary>
        /// Fired when the user selects a destination from the menu.
        /// </summary>
        public static event Action<DestinationData> OnDestinationSelected;
        public static void RaiseDestinationSelected(DestinationData destination) => OnDestinationSelected?.Invoke(destination);

        /// <summary>
        /// Fired when navigation has started and the path is visible.
        /// </summary>
        public static event Action OnNavigationStarted;
        public static void RaiseNavigationStarted() => OnNavigationStarted?.Invoke();

        /// <summary>
        /// Fired when the user passes a waypoint checkpoint along the route.
        /// Parameter: the index of the checkpoint in the current path.
        /// </summary>
        public static event Action<int> OnCheckpointReached;
        public static void RaiseCheckpointReached(int waypointIndex) => OnCheckpointReached?.Invoke(waypointIndex);

        /// <summary>
        /// Fired when the user moves too far from the planned route.
        /// </summary>
        public static event Action OnOffRoute;
        public static void RaiseOffRoute() => OnOffRoute?.Invoke();

        /// <summary>
        /// Fired when the user returns to the planned route after deviation.
        /// </summary>
        public static event Action OnBackOnRoute;
        public static void RaiseBackOnRoute() => OnBackOnRoute?.Invoke();

        /// <summary>
        /// Fired when the route has been recalculated (e.g., after deviation).
        /// </summary>
        public static event Action OnRouteRecalculated;
        public static void RaiseRouteRecalculated() => OnRouteRecalculated?.Invoke();

        /// <summary>
        /// Fired when a route recalculation fails to find any path (e.g., user is in an unreachable area).
        /// </summary>
        public static event Action OnRecalculationFailed;
        public static void RaiseRecalculationFailed() => OnRecalculationFailed?.Invoke();

        /// <summary>
        /// Fired when the user has arrived at the selected destination.
        /// </summary>
        public static event Action<DestinationData> OnArrived;
        public static void RaiseArrived(DestinationData destination) => OnArrived?.Invoke(destination);

        // ── Obstacle Detection Events ────────────────────────────────────

        /// <summary>
        /// Fired when an obstacle is detected blocking a path segment.
        /// Parameters: nodeId of segment start, nodeId of segment end.
        /// </summary>
        public static event Action<int, int> OnObstacleDetected;
        public static void RaiseObstacleDetected(int nodeA, int nodeB) => OnObstacleDetected?.Invoke(nodeA, nodeB);

        /// <summary>
        /// Fired when a previously blocked path segment is cleared.
        /// Parameters: nodeId of segment start, nodeId of segment end.
        /// </summary>
        public static event Action<int, int> OnObstacleCleared;
        public static void RaiseObstacleCleared(int nodeA, int nodeB) => OnObstacleCleared?.Invoke(nodeA, nodeB);

        // ── UI Events ──────────────────────────────────────────────────────

        /// <summary>
        /// Fired when the user taps the "Start" button on the onboarding screen.
        /// </summary>
        public static event Action OnOnboardingComplete;
        public static void RaiseOnboardingComplete() => OnOnboardingComplete?.Invoke();

        /// <summary>
        /// Fired when the user wants to navigate to a new destination after arrival.
        /// </summary>
        public static event Action OnNavigateAgain;
        public static void RaiseNavigateAgain() => OnNavigateAgain?.Invoke();

        // ── Distance/Time Updates ──────────────────────────────────────────

        /// <summary>
        /// Fired each frame with updated distance (meters) and estimated time (seconds).
        /// </summary>
        public static event Action<float, float> OnDistanceUpdated;
        public static void RaiseDistanceUpdated(float distanceMeters, float estimatedTimeSeconds) =>
            OnDistanceUpdated?.Invoke(distanceMeters, estimatedTimeSeconds);

        // ── Lifecycle ──────────────────────────────────────────────────────

        /// <summary>
        /// Clears all static event subscriptions. Call this when the scene/session resets
        /// to prevent stale subscribers on destroyed objects from previous sessions.
        /// </summary>
        public static void ClearAll()
        {
            OnFloorDetected = null;
            OnTerminalAnchorPlaced = null;
            OnDestinationSelected = null;
            OnNavigationStarted = null;
            OnCheckpointReached = null;
            OnOffRoute = null;
            OnBackOnRoute = null;
            OnRouteRecalculated = null;
            OnRecalculationFailed = null;
            OnArrived = null;
            OnOnboardingComplete = null;
            OnNavigateAgain = null;
            OnDistanceUpdated = null;
            OnObstacleDetected = null;
            OnObstacleCleared = null;
        }
    }
}
