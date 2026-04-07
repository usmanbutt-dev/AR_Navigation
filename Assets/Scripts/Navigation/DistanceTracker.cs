using System.Collections.Generic;
using UnityEngine;
using Nibrask.Core;

namespace Nibrask.Navigation
{
    /// <summary>
    /// Monitors the user's position relative to the current navigation path.
    /// Calculates remaining distance, estimated walking time, detects off-route conditions,
    /// and fires checkpoint/arrival events.
    /// </summary>
    public class DistanceTracker : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField]
        [Tooltip("Maximum allowed distance from the path before triggering off-route (meters)")]
        private float offRouteThreshold = 3.0f;

        [SerializeField]
        [Tooltip("Distance to a waypoint to consider it 'reached' as a checkpoint (meters)")]
        private float checkpointReachDistance = 1.5f;

        [SerializeField]
        [Tooltip("Distance to the final destination to consider arrival (meters)")]
        private float arrivalDistance = 2.0f;

        [SerializeField]
        [Tooltip("Average walking speed in meters per second")]
        private float averageWalkingSpeed = 1.4f;

        [SerializeField]
        [Tooltip("How often to update distance calculations (seconds)")]
        private float updateInterval = 0.3f;

        [Header("References")]
        [SerializeField]
        [Tooltip("The camera/user transform to track")]
        private Transform userTransform;

        [Header("Debug")]
        [SerializeField]
        private float currentDistanceToPath;

        [SerializeField]
        private float remainingDistance;

        [SerializeField]
        private float estimatedTime;

        [SerializeField]
        private bool isOffRoute;

        [SerializeField]
        private int currentCheckpointIndex;

        /// <summary>
        /// Whether the user is currently off the route.
        /// </summary>
        public bool IsOffRoute => isOffRoute;

        /// <summary>
        /// Remaining distance to destination in meters.
        /// </summary>
        public float RemainingDistance => remainingDistance;

        /// <summary>
        /// Estimated time to destination in seconds.
        /// </summary>
        public float EstimatedTimeSeconds => estimatedTime;

        /// <summary>
        /// Index of the current/next checkpoint the user is approaching.
        /// </summary>
        public int CurrentCheckpointIndex => currentCheckpointIndex;

        private List<WaypointNode> currentPath;
        private bool isTracking = false;
        private float nextUpdateTime;
        private bool wasOffRoute = false;
        private bool hasArrived = false;
        private DestinationData trackedDestination;

        private void Start()
        {
            // Default to main camera if not assigned
            if (userTransform == null)
            {
                userTransform = Camera.main?.transform;
            }
        }

        private void Update()
        {
            if (!isTracking || currentPath == null || currentPath.Count == 0)
                return;

            if (Time.time < nextUpdateTime)
                return;

            nextUpdateTime = Time.time + updateInterval;
            UpdateTracking();
        }

        /// <summary>
        /// Starts tracking the user's position against a navigation path.
        /// destination is passed explicitly so arrival does not depend on the waypoint's
        /// associatedDestination back-reference, which may be null.
        /// </summary>
        public void StartTracking(List<WaypointNode> path, DestinationData destination)
        {
            if (path == null || path.Count == 0)
            {
                Debug.LogWarning("[DistanceTracker] Cannot track — path is null or empty.");
                return;
            }

            currentPath = new List<WaypointNode>(path);
            trackedDestination = destination;
            currentCheckpointIndex = 0;
            isOffRoute = false;
            wasOffRoute = false;
            hasArrived = false;
            isTracking = true;
            nextUpdateTime = 0f;

            Debug.Log($"[DistanceTracker] Tracking started with {path.Count} waypoints.");
        }

        /// <summary>
        /// Stops tracking.
        /// </summary>
        public void StopTracking()
        {
            isTracking = false;
            currentPath = null;
        }

        /// <summary>
        /// Updates the path when a recalculation occurs.
        /// destination is kept from the original StartTracking call; if a new
        /// destination is provided it overrides the tracked one.
        /// </summary>
        public void UpdatePath(List<WaypointNode> newPath, DestinationData destination = null)
        {
            if (newPath == null || newPath.Count == 0)
            {
                Debug.LogWarning("[DistanceTracker] UpdatePath received null/empty path — recalculation failed.");
                AppEvents.RaiseRecalculationFailed();
                return;
            }

            currentPath = new List<WaypointNode>(newPath);
            if (destination != null) trackedDestination = destination;
            currentCheckpointIndex = 0;
            isOffRoute = false;
            wasOffRoute = false;
            hasArrived = false;
        }

        /// <summary>
        /// Main tracking update: checks checkpoints, off-route status, and arrival.
        /// </summary>
        private void UpdateTracking()
        {
            if (userTransform == null) return;

            Vector3 userPos = userTransform.position;

            // 1. Check if current checkpoint has been reached
            CheckCheckpoints(userPos);

            // 2. Calculate distance to nearest path segment
            currentDistanceToPath = CalculateDistanceToPath(userPos);

            // 3. Calculate remaining distance from current checkpoint to destination
            remainingDistance = CalculateRemainingDistance(userPos);

            // 4. Estimate walking time
            estimatedTime = remainingDistance / averageWalkingSpeed;

            // 5. Broadcast distance update
            AppEvents.RaiseDistanceUpdated(remainingDistance, estimatedTime);

            // 6. Check off-route condition
            bool currentlyOffRoute = currentDistanceToPath > offRouteThreshold;

            if (currentlyOffRoute && !wasOffRoute)
            {
                isOffRoute = true;
                wasOffRoute = true;
                Debug.Log($"[DistanceTracker] User is OFF ROUTE (distance: {currentDistanceToPath:F1}m)");
                AppEvents.RaiseOffRoute();
            }
            else if (!currentlyOffRoute && wasOffRoute)
            {
                isOffRoute = false;
                wasOffRoute = false;
                Debug.Log("[DistanceTracker] User is BACK ON ROUTE.");
                AppEvents.RaiseBackOnRoute();
            }

            // 7. Check arrival condition
            if (!hasArrived && currentPath.Count > 0)
            {
                WaypointNode destinationNode = currentPath[currentPath.Count - 1];
                float distToDest = Vector3.Distance(userPos, destinationNode.WorldPosition);

                if (distToDest <= arrivalDistance)
                {
                    hasArrived = true;
                    isTracking = false;
                    Debug.Log("[DistanceTracker] User has ARRIVED at destination.");
                    // Use the explicitly tracked destination (safe), fall back to node's association
                    var dest = trackedDestination ?? destinationNode.associatedDestination;
                    AppEvents.RaiseArrived(dest);
                }
            }
        }

        /// <summary>
        /// Checks if the user has reached the next checkpoint waypoint.
        /// </summary>
        private void CheckCheckpoints(Vector3 userPos)
        {
            if (currentCheckpointIndex >= currentPath.Count)
                return;

            WaypointNode checkpoint = currentPath[currentCheckpointIndex];
            float distToCheckpoint = Vector3.Distance(userPos, checkpoint.WorldPosition);

            if (distToCheckpoint <= checkpointReachDistance)
            {
                Debug.Log($"[DistanceTracker] Checkpoint {currentCheckpointIndex} reached (node {checkpoint.nodeId})");
                AppEvents.RaiseCheckpointReached(currentCheckpointIndex);
                currentCheckpointIndex++;
            }
        }

        /// <summary>
        /// Calculates the minimum distance from the user to the nearest path segment.
        /// </summary>
        private float CalculateDistanceToPath(Vector3 userPos)
        {
            float minDist = float.MaxValue;

            for (int i = currentCheckpointIndex; i < currentPath.Count - 1; i++)
            {
                Vector3 a = currentPath[i].WorldPosition;
                Vector3 b = currentPath[i + 1].WorldPosition;
                float dist = DistanceToLineSegment(userPos, a, b);
                if (dist < minDist)
                    minDist = dist;
            }

            // Also check distance to the current checkpoint itself
            if (currentCheckpointIndex < currentPath.Count)
            {
                float distToCheckpoint = Vector3.Distance(userPos, currentPath[currentCheckpointIndex].WorldPosition);
                if (distToCheckpoint < minDist)
                    minDist = distToCheckpoint;
            }

            return minDist;
        }

        /// <summary>
        /// Calculates the remaining distance from the user's projected position on the path to the destination.
        /// </summary>
        private float CalculateRemainingDistance(Vector3 userPos)
        {
            if (currentCheckpointIndex >= currentPath.Count)
                return 0f;

            float distance = 0f;

            // Distance from user to the next checkpoint
            distance += Vector3.Distance(userPos, currentPath[currentCheckpointIndex].WorldPosition);

            // Distance along the remaining path segments
            for (int i = currentCheckpointIndex; i < currentPath.Count - 1; i++)
            {
                distance += currentPath[i].DistanceTo(currentPath[i + 1]);
            }

            return distance;
        }

        /// <summary>
        /// Calculates the shortest distance from point P to line segment AB.
        /// </summary>
        private static float DistanceToLineSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            Vector3 ap = p - a;

            float sqrLenAB = ab.sqrMagnitude;
            if (sqrLenAB < 0.0001f) return Vector3.Distance(p, a);

            float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / sqrLenAB);
            Vector3 closestPoint = a + t * ab;

            return Vector3.Distance(p, closestPoint);
        }
    }
}
