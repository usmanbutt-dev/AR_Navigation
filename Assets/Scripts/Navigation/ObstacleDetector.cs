using System.Collections.Generic;
using UnityEngine;
using Nibrask.Core;

namespace Nibrask.Navigation
{
    /// <summary>
    /// Detects physical obstacles blocking navigation path segments using raycasts.
    /// Maintains a set of blocked edges that PathFinder uses to route around obstacles.
    /// Probes only the active path segments at configurable intervals. Gracefully
    /// degrades on devices without AR meshing (simply does nothing).
    /// </summary>
    public class ObstacleDetector : MonoBehaviour
    {
        [Header("Probing")]
        [SerializeField]
        [Tooltip("How often to probe path segments for obstacles (seconds)")]
        private float probeInterval = 1.0f;

        [SerializeField]
        [Tooltip("Heights above the floor at which to cast rays (meters)")]
        private float[] probeHeights = { 0.3f, 0.6f, 1.0f };

        [SerializeField]
        [Tooltip("Radius of SphereCast probes (meters)")]
        private float probeRadius = 0.15f;

        [SerializeField]
        [Tooltip("Number of ray sample points distributed along each segment")]
        private int raysPerSegment = 5;

        [SerializeField]
        [Tooltip("Consecutive blocked probes required before marking an edge (prevents false positives)")]
        private int obstacleDebounceCount = 3;

        [SerializeField]
        [Tooltip("Time to keep an edge marked after obstacle disappears (prevents path flickering)")]
        private float clearCooldown = 2.0f;

        [SerializeField]
        [Tooltip("Minimum segment length to probe (shorter segments are skipped)")]
        private float minSegmentLength = 0.5f;

        [Header("Layers")]
        [SerializeField]
        [Tooltip("Layers that count as obstacles (e.g. AR Mesh, Default)")]
        private LayerMask obstacleLayers = ~0; // Everything by default

        [SerializeField]
        [Tooltip("Layers to ignore during raycasts (e.g. floor planes, UI)")]
        private LayerMask ignoreLayers;

        [Header("References")]
        [SerializeField]
        private NavigationManager navigationManager;

        [SerializeField]
        private WaypointGraph waypointGraph;

        /// <summary>
        /// The set of currently blocked edges. Used by NavigationManager when rerouting.
        /// Each entry is (nodeIdA, nodeIdB) with the smaller ID first for consistency.
        /// </summary>
        public HashSet<(int, int)> BlockedEdges { get; private set; } = new HashSet<(int, int)>();

        // Tracks how many consecutive probes found an obstruction per edge
        private Dictionary<(int, int), int> hitCounts = new Dictionary<(int, int), int>();

        // Tracks when a blocked edge was last confirmed clear (for cooldown)
        private Dictionary<(int, int), float> clearTimestamps = new Dictionary<(int, int), float>();

        // The edges we're currently probing (only the active path)
        private List<(int, int)> activeEdges = new List<(int, int)>();

        private bool isProbing = false;
        private float lastProbeTime;

        // Combined layer mask (obstacleLayers minus ignoreLayers)
        private int effectiveLayerMask;

        private void OnEnable()
        {
            AppEvents.OnNavigationStarted += HandleNavigationStarted;
            AppEvents.OnRouteRecalculated += HandleRouteRecalculated;
            AppEvents.OnArrived += HandleArrived;
        }

        private void OnDisable()
        {
            AppEvents.OnNavigationStarted -= HandleNavigationStarted;
            AppEvents.OnRouteRecalculated -= HandleRouteRecalculated;
            AppEvents.OnArrived -= HandleArrived;
        }

        private void Start()
        {
            // Combine obstacle and ignore masks
            effectiveLayerMask = obstacleLayers & ~ignoreLayers;
        }

        private void Update()
        {
            if (!isProbing) return;
            if (Time.time - lastProbeTime < probeInterval) return;

            lastProbeTime = Time.time;
            ProbeActiveEdges();
        }

        // ── Event Handlers ──────────────────────────────────────────────

        private void HandleNavigationStarted()
        {
            // Start probing the current path
            RefreshActiveEdges();
            isProbing = true;
            lastProbeTime = Time.time;

            Debug.Log($"[ObstacleDetector] Started probing {activeEdges.Count} path segments.");
        }

        private void HandleRouteRecalculated()
        {
            // Path changed — update which edges we probe
            RefreshActiveEdges();
        }

        private void HandleArrived(Data.DestinationData destination)
        {
            StopProbing();
        }

        // ── Core Logic ──────────────────────────────────────────────────

        /// <summary>
        /// Stops all probing and clears state for next navigation session.
        /// </summary>
        public void StopProbing()
        {
            isProbing = false;
            activeEdges.Clear();
            hitCounts.Clear();
            clearTimestamps.Clear();
            BlockedEdges.Clear();
        }

        /// <summary>
        /// Refreshes the list of edges to probe based on the current navigation path.
        /// </summary>
        private void RefreshActiveEdges()
        {
            activeEdges.Clear();

            if (navigationManager == null || waypointGraph == null) return;

            var path = navigationManager.CurrentPath;
            if (path == null || path.Count < 2) return;

            activeEdges = waypointGraph.GetPathEdges(path);
        }

        /// <summary>
        /// Probes each active edge for obstacles using SphereCasts at multiple heights.
        /// </summary>
        private void ProbeActiveEdges()
        {
            if (waypointGraph == null) return;

            // Take a snapshot — activeEdges can change if reroute happens mid-probe
            var edgesToProbe = new List<(int, int)>(activeEdges);

            foreach (var edge in edgesToProbe)
            {
                var normalizedEdge = NormalizeEdge(edge.Item1, edge.Item2);

                if (!waypointGraph.GetEdgeWorldPositions(edge.Item1, edge.Item2, out Vector3 posA, out Vector3 posB))
                    continue;

                float segmentLength = Vector3.Distance(
                    new Vector3(posA.x, 0f, posA.z),
                    new Vector3(posB.x, 0f, posB.z));

                if (segmentLength < minSegmentLength)
                    continue;

                bool obstacleFound = ProbeSegment(posA, posB, segmentLength);

                if (obstacleFound)
                {
                    HandleObstacleHit(normalizedEdge);
                }
                else
                {
                    HandleObstacleMiss(normalizedEdge);
                }
            }
        }

        /// <summary>
        /// Casts rays along a segment at multiple heights. Returns true if any ray hits an obstacle.
        /// </summary>
        private bool ProbeSegment(Vector3 posA, Vector3 posB, float segmentLength)
        {
            Vector3 flatA = new Vector3(posA.x, 0f, posA.z);
            Vector3 flatB = new Vector3(posB.x, 0f, posB.z);
            Vector3 direction = (flatB - flatA).normalized;
            float floorY = Mathf.Min(posA.y, posB.y);

            for (int r = 0; r < raysPerSegment; r++)
            {
                // Distribute sample points along the segment (excluding endpoints)
                float t = (r + 1f) / (raysPerSegment + 1f);
                Vector3 sampleXZ = Vector3.Lerp(flatA, flatB, t);

                foreach (float height in probeHeights)
                {
                    Vector3 origin = new Vector3(sampleXZ.x, floorY + height, sampleXZ.z);

                    // Cast a short sphere in the segment direction to detect solid geometry
                    if (Physics.SphereCast(origin, probeRadius, direction, out RaycastHit hit,
                        segmentLength * 0.1f, effectiveLayerMask, QueryTriggerInteraction.Ignore))
                    {
                        return true;
                    }

                    // Also cast straight down from above to detect low obstacles
                    if (Physics.SphereCast(origin + Vector3.up * 0.5f, probeRadius, Vector3.down, out RaycastHit hitDown,
                        0.5f + height * 0.5f, effectiveLayerMask, QueryTriggerInteraction.Ignore))
                    {
                        // Only count if the hit is above the floor (not the floor itself)
                        if (hitDown.point.y > floorY + 0.1f)
                            return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Handles a positive obstacle detection on an edge. Increments debounce counter
        /// and marks the edge as blocked when the threshold is reached.
        /// </summary>
        private void HandleObstacleHit((int, int) edge)
        {
            // Remove from clear cooldown tracking
            clearTimestamps.Remove(edge);

            // Increment hit counter
            if (!hitCounts.ContainsKey(edge))
                hitCounts[edge] = 0;

            hitCounts[edge]++;

            // Check if debounce threshold reached
            if (hitCounts[edge] >= obstacleDebounceCount && !BlockedEdges.Contains(edge))
            {
                BlockedEdges.Add(edge);
                Debug.Log($"[ObstacleDetector] Edge ({edge.Item1} → {edge.Item2}) BLOCKED after {obstacleDebounceCount} consecutive detections.");
                AppEvents.RaiseObstacleDetected(edge.Item1, edge.Item2);
            }
        }

        /// <summary>
        /// Handles a clear probe on an edge. Manages cooldown before actually unblocking.
        /// </summary>
        private void HandleObstacleMiss((int, int) edge)
        {
            // Reset the hit counter
            hitCounts.Remove(edge);

            // If the edge was blocked, start or check cooldown before clearing
            if (BlockedEdges.Contains(edge))
            {
                if (!clearTimestamps.ContainsKey(edge))
                {
                    // First clear probe — start cooldown
                    clearTimestamps[edge] = Time.time;
                }
                else if (Time.time - clearTimestamps[edge] >= clearCooldown)
                {
                    // Cooldown elapsed — actually unblock
                    BlockedEdges.Remove(edge);
                    clearTimestamps.Remove(edge);
                    Debug.Log($"[ObstacleDetector] Edge ({edge.Item1} → {edge.Item2}) CLEARED after {clearCooldown}s cooldown.");
                    AppEvents.RaiseObstacleCleared(edge.Item1, edge.Item2);
                }
                // else: still in cooldown, keep blocked
            }
        }

        /// <summary>
        /// Normalizes an edge pair so the smaller nodeId is always first.
        /// This ensures (3,5) and (5,3) are treated as the same edge.
        /// </summary>
        private (int, int) NormalizeEdge(int a, int b)
        {
            return a < b ? (a, b) : (b, a);
        }
    }
}
