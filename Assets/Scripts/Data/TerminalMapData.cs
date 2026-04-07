using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nibrask.Data
{
    /// <summary>
    /// Represents a single waypoint node in the terminal's navigation graph.
    /// </summary>
    [Serializable]
    public class WaypointData
    {
        [Tooltip("Unique identifier for this waypoint")]
        public int nodeId;

        [Tooltip("Position relative to the terminal origin anchor")]
        public Vector3 relativePosition;

        [Tooltip("Indices of connected waypoint nodes (bidirectional edges)")]
        public List<int> connectedNodeIds = new List<int>();

        [Tooltip("If true, this waypoint is directly associated with a destination")]
        public bool isDestinationNode;

        [Tooltip("Label for debugging/editor visualization")]
        public string debugLabel;
    }

    /// <summary>
    /// ScriptableObject defining the complete airport terminal layout including
    /// all destinations and the waypoint navigation graph used for pathfinding.
    /// </summary>
    [CreateAssetMenu(fileName = "NewTerminalMap", menuName = "Nibrask/Terminal Map Data")]
    public class TerminalMapData : ScriptableObject
    {
        [Header("Terminal Info")]
        [Tooltip("Name of the airport terminal")]
        public string terminalName = "Terminal 1";

        [Header("Destinations")]
        [Tooltip("All navigable destinations in this terminal")]
        public List<DestinationData> destinations = new List<DestinationData>();

        [Header("Waypoint Graph")]
        [Tooltip("All waypoint nodes forming the navigation graph")]
        public List<WaypointData> waypoints = new List<WaypointData>();

        [Header("Settings")]
        [Tooltip("Scale factor applied to all relative positions (meters per unit)")]
        public float scaleFactor = 1f;

        /// <summary>
        /// Finds a waypoint by its node ID.
        /// </summary>
        public WaypointData GetWaypointById(int nodeId)
        {
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i].nodeId == nodeId)
                    return waypoints[i];
            }
            return null;
        }

        /// <summary>
        /// Gets the world position of a waypoint relative to a terminal origin.
        /// </summary>
        public Vector3 GetWaypointWorldPosition(int nodeId, Transform terminalOrigin)
        {
            var waypoint = GetWaypointById(nodeId);
            if (waypoint == null) return terminalOrigin.position;
            return terminalOrigin.TransformPoint(waypoint.relativePosition * scaleFactor);
        }

        /// <summary>
        /// Gets the world position of a destination relative to a terminal origin.
        /// </summary>
        public Vector3 GetDestinationWorldPosition(DestinationData destination, Transform terminalOrigin)
        {
            return terminalOrigin.TransformPoint(destination.relativePosition * scaleFactor);
        }

        /// <summary>
        /// Validates that all waypoint connections are bidirectional and that
        /// destination references point to valid waypoints.
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            errorMessage = string.Empty;

            // Check all connections are bidirectional
            foreach (var wp in waypoints)
            {
                foreach (int connId in wp.connectedNodeIds)
                {
                    var connectedWp = GetWaypointById(connId);
                    if (connectedWp == null)
                    {
                        errorMessage = $"Waypoint {wp.nodeId} references non-existent waypoint {connId}";
                        return false;
                    }

                    if (!connectedWp.connectedNodeIds.Contains(wp.nodeId))
                    {
                        errorMessage = $"Connection from waypoint {wp.nodeId} to {connId} is not bidirectional";
                        return false;
                    }
                }
            }

            // Check destinations reference valid waypoints
            foreach (var dest in destinations)
            {
                if (GetWaypointById(dest.nearestWaypointIndex) == null)
                {
                    errorMessage = $"Destination '{dest.destinationName}' references non-existent waypoint {dest.nearestWaypointIndex}";
                    return false;
                }
            }

            return true;
        }
    }
}
