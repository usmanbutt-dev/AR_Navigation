using System.Collections.Generic;
using UnityEngine;
using Nibrask.Data;

namespace Nibrask.Navigation
{
    /// <summary>
    /// Runtime representation of a waypoint node in the navigation graph.
    /// Spawned by WaypointGraph based on TerminalMapData.
    /// </summary>
    public class WaypointNode : MonoBehaviour
    {
        [Header("Node Data")]
        [Tooltip("Unique identifier matching the WaypointData.nodeId")]
        public int nodeId;

        [Tooltip("Whether this node is directly associated with a destination")]
        public bool isDestinationNode;

        [Tooltip("Debug label for editor visualization")]
        public string debugLabel;

        /// <summary>
        /// List of directly connected neighbor nodes (populated at runtime by WaypointGraph).
        /// </summary>
        [HideInInspector]
        public List<WaypointNode> connectedNodes = new List<WaypointNode>();

        /// <summary>
        /// The associated DestinationData if this is a destination node.
        /// </summary>
        [HideInInspector]
        public DestinationData associatedDestination;

        /// <summary>
        /// World position of this waypoint.
        /// </summary>
        public Vector3 WorldPosition => transform.position;

        /// <summary>
        /// Calculates the distance to another waypoint node.
        /// </summary>
        public float DistanceTo(WaypointNode other)
        {
            return Vector3.Distance(WorldPosition, other.WorldPosition);
        }

        /// <summary>
        /// Calculates the distance to a world position.
        /// </summary>
        public float DistanceTo(Vector3 position)
        {
            return Vector3.Distance(WorldPosition, position);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Draw the waypoint as a sphere
            Gizmos.color = isDestinationNode ? Color.yellow : Color.cyan;
            Gizmos.DrawSphere(transform.position, 0.15f);

            // Draw connections to neighbors
            Gizmos.color = Color.green;
            foreach (var neighbor in connectedNodes)
            {
                if (neighbor != null)
                {
                    Gizmos.DrawLine(transform.position + Vector3.up * 0.05f,
                                    neighbor.transform.position + Vector3.up * 0.05f);
                }
            }

            // Draw label
            if (!string.IsNullOrEmpty(debugLabel))
            {
                UnityEditor.Handles.Label(transform.position + Vector3.up * 0.3f, debugLabel);
            }
        }
#endif
    }
}
