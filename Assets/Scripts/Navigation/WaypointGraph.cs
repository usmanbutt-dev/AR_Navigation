using System.Collections.Generic;
using UnityEngine;
using Nibrask.Core;
using Nibrask.Data;

namespace Nibrask.Navigation
{
    /// <summary>
    /// Builds and manages the runtime waypoint navigation graph from TerminalMapData.
    /// Spawns WaypointNode GameObjects relative to the terminal origin anchor and
    /// wires up their neighbor connections for pathfinding.
    /// </summary>
    public class WaypointGraph : MonoBehaviour
    {
        [Header("Debug Visualization")]
        [SerializeField]
        [Tooltip("Whether to show debug spheres for waypoints at runtime")]
        private bool showDebugVisuals = false;

        [SerializeField]
        [Tooltip("Prefab for debug waypoint visualization (optional)")]
        private GameObject debugWaypointPrefab;

        /// <summary>
        /// All spawned waypoint nodes indexed by their nodeId.
        /// </summary>
        private readonly Dictionary<int, WaypointNode> nodes = new Dictionary<int, WaypointNode>();

        /// <summary>
        /// Whether the graph has been built.
        /// </summary>
        public bool IsBuilt => nodes.Count > 0;

        private void OnEnable()
        {
            AppEvents.OnTerminalAnchorPlaced += HandleAnchorPlaced;
        }

        private void OnDisable()
        {
            AppEvents.OnTerminalAnchorPlaced -= HandleAnchorPlaced;
        }

        /// <summary>
        /// When the terminal anchor is placed, build the waypoint graph.
        /// </summary>
        private void HandleAnchorPlaced(Transform terminalOrigin)
        {
            var mapData = AppStateManager.Instance?.TerminalMap;
            if (mapData == null)
            {
                Debug.LogError("[WaypointGraph] No TerminalMapData available on AppStateManager.");
                return;
            }

            BuildGraph(mapData, terminalOrigin);
        }

        /// <summary>
        /// Builds the waypoint graph from TerminalMapData, positioning nodes relative to the terminal origin.
        /// </summary>
        public void BuildGraph(TerminalMapData mapData, Transform terminalOrigin)
        {
            // Clear any existing graph
            ClearGraph();

            if (mapData.waypoints == null || mapData.waypoints.Count == 0)
            {
                Debug.LogWarning("[WaypointGraph] TerminalMapData has no waypoints.");
                return;
            }

            Debug.Log($"[WaypointGraph] Building graph with {mapData.waypoints.Count} waypoints...");

            // Phase 1: Create all waypoint nodes
            foreach (var waypointData in mapData.waypoints)
            {
                Vector3 worldPos = terminalOrigin.TransformPoint(waypointData.relativePosition * mapData.scaleFactor);

                var nodeGo = new GameObject($"Waypoint_{waypointData.nodeId}_{waypointData.debugLabel}");
                nodeGo.transform.SetParent(transform);
                nodeGo.transform.position = worldPos;

                var node = nodeGo.AddComponent<WaypointNode>();
                node.nodeId = waypointData.nodeId;
                node.isDestinationNode = waypointData.isDestinationNode;
                node.debugLabel = waypointData.debugLabel;

                nodes[waypointData.nodeId] = node;

                // Spawn debug visual if enabled
                if (showDebugVisuals && debugWaypointPrefab != null)
                {
                    Instantiate(debugWaypointPrefab, worldPos, Quaternion.identity, nodeGo.transform);
                }
            }

            // Phase 2: Wire up connections
            foreach (var waypointData in mapData.waypoints)
            {
                if (!nodes.TryGetValue(waypointData.nodeId, out var node))
                    continue;

                foreach (int connectedId in waypointData.connectedNodeIds)
                {
                    if (nodes.TryGetValue(connectedId, out var connectedNode))
                    {
                        if (!node.connectedNodes.Contains(connectedNode))
                            node.connectedNodes.Add(connectedNode);
                    }
                    else
                    {
                        Debug.LogWarning($"[WaypointGraph] Waypoint {waypointData.nodeId} references unknown node {connectedId}");
                    }
                }
            }

            // Phase 3: Associate destinations with their nearest waypoint nodes
            foreach (var destination in mapData.destinations)
            {
                if (nodes.TryGetValue(destination.nearestWaypointIndex, out var destNode))
                {
                    destNode.associatedDestination = destination;
                    destNode.isDestinationNode = true;
                }
            }

            Debug.Log($"[WaypointGraph] Graph built successfully: {nodes.Count} nodes, " +
                      $"{CountConnections()} connections.");
        }

        /// <summary>
        /// Finds the closest waypoint node to a given world position.
        /// Uses XZ-only (horizontal) distance to avoid Y-axis bias from
        /// camera height vs floor-level waypoints.
        /// </summary>
        public WaypointNode FindNearestNode(Vector3 worldPosition)
        {
            WaypointNode nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var kvp in nodes)
            {
                // XZ-only distance so height differences don't bias the result
                Vector3 nodePos = kvp.Value.WorldPosition;
                float dx = nodePos.x - worldPosition.x;
                float dz = nodePos.z - worldPosition.z;
                float dist = dx * dx + dz * dz; // squared is fine for comparison
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = kvp.Value;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Gets the waypoint node associated with a specific destination.
        /// </summary>
        public WaypointNode GetNodeForDestination(DestinationData destination)
        {
            if (nodes.TryGetValue(destination.nearestWaypointIndex, out var node))
                return node;

            // Fallback: search by destination's world position relative to this graph's transform.
            // The WaypointGraph parent IS the terminal anchor transform set in BuildGraph,
            // so we use transform.TransformPoint (not transform.parent which could be anything).
            // Bug Fix #3: was incorrectly using transform.parent.TransformPoint.
            Debug.LogWarning($"[WaypointGraph] No node found for destination '{destination.destinationName}', using nearest.");
            Vector3 approxWorldPos = transform.TransformPoint(destination.relativePosition);
            return FindNearestNode(approxWorldPos);
        }

        /// <summary>
        /// Gets a waypoint node by its ID.
        /// </summary>
        public WaypointNode GetNodeById(int nodeId)
        {
            nodes.TryGetValue(nodeId, out var node);
            return node;
        }

        /// <summary>
        /// Destroys all spawned waypoint nodes and clears the graph.
        /// </summary>
        public void ClearGraph()
        {
            foreach (var kvp in nodes)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);
            }
            nodes.Clear();
        }

        /// <summary>
        /// Counts total connections in the graph (each edge counted once).
        /// </summary>
        private int CountConnections()
        {
            int total = 0;
            foreach (var kvp in nodes)
            {
                total += kvp.Value.connectedNodes.Count;
            }
            return total / 2; // Each edge is counted twice (bidirectional)
        }
    }
}
