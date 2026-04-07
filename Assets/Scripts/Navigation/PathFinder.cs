using System.Collections.Generic;
using UnityEngine;

namespace Nibrask.Navigation
{
    /// <summary>
    /// A* pathfinding algorithm operating on the WaypointNode graph.
    /// Finds the shortest path between two waypoint nodes using Euclidean distance heuristic.
    /// </summary>
    public static class PathFinder
    {
        /// <summary>
        /// Internal node used during A* search to track costs and parent chain.
        /// </summary>
        private class AStarNode
        {
            public WaypointNode Waypoint;
            public AStarNode Parent;
            public float GCost; // Cost from start to this node
            public float HCost; // Heuristic cost from this node to end
            public float FCost => GCost + HCost; // Total estimated cost
        }

        /// <summary>
        /// Finds the shortest path between two waypoint nodes using A* algorithm.
        /// </summary>
        /// <param name="start">Starting waypoint node</param>
        /// <param name="end">Destination waypoint node</param>
        /// <returns>Ordered list of waypoint nodes from start to end, or empty list if no path found.</returns>
        public static List<WaypointNode> FindPath(WaypointNode start, WaypointNode end)
        {
            if (start == null || end == null)
            {
                Debug.LogWarning("[PathFinder] Start or end node is null.");
                return new List<WaypointNode>();
            }

            if (start == end)
            {
                return new List<WaypointNode> { start };
            }

            var openSet = new List<AStarNode>();
            var closedSet = new HashSet<int>(); // Track by nodeId

            var startNode = new AStarNode
            {
                Waypoint = start,
                Parent = null,
                GCost = 0f,
                HCost = Heuristic(start, end)
            };

            openSet.Add(startNode);

            int maxIterations = 1000; // Safety limit
            int iterations = 0;

            while (openSet.Count > 0 && iterations < maxIterations)
            {
                iterations++;

                // Find the node with the lowest F cost
                AStarNode current = GetLowestFCostNode(openSet);

                // Check if we've reached the destination
                if (current.Waypoint.nodeId == end.nodeId)
                {
                    return ReconstructPath(current);
                }

                openSet.Remove(current);
                closedSet.Add(current.Waypoint.nodeId);

                // Explore neighbors
                foreach (var neighbor in current.Waypoint.connectedNodes)
                {
                    if (neighbor == null || closedSet.Contains(neighbor.nodeId))
                        continue;

                    float tentativeGCost = current.GCost + current.Waypoint.DistanceTo(neighbor);

                    // Check if this neighbor is already in the open set
                    AStarNode existingNode = FindInOpenSet(openSet, neighbor.nodeId);

                    if (existingNode != null)
                    {
                        // If we found a better path to this node, update it
                        if (tentativeGCost < existingNode.GCost)
                        {
                            existingNode.GCost = tentativeGCost;
                            existingNode.Parent = current;
                        }
                    }
                    else
                    {
                        // Add new node to open set
                        openSet.Add(new AStarNode
                        {
                            Waypoint = neighbor,
                            Parent = current,
                            GCost = tentativeGCost,
                            HCost = Heuristic(neighbor, end)
                        });
                    }
                }
            }

            if (iterations >= maxIterations)
            {
                Debug.LogWarning("[PathFinder] Max iterations reached — possible infinite loop or disconnected graph.");
            }

            Debug.LogWarning($"[PathFinder] No path found between node {start.nodeId} and node {end.nodeId}.");
            return new List<WaypointNode>();
        }

        /// <summary>
        /// Heuristic function: Euclidean distance between two waypoints.
        /// </summary>
        private static float Heuristic(WaypointNode a, WaypointNode b)
        {
            return Vector3.Distance(a.WorldPosition, b.WorldPosition);
        }

        /// <summary>
        /// Finds the node with the lowest F cost in the open set.
        /// </summary>
        private static AStarNode GetLowestFCostNode(List<AStarNode> openSet)
        {
            AStarNode lowest = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].FCost < lowest.FCost ||
                    (Mathf.Approximately(openSet[i].FCost, lowest.FCost) && openSet[i].HCost < lowest.HCost))
                {
                    lowest = openSet[i];
                }
            }
            return lowest;
        }

        /// <summary>
        /// Searches for a node with the given ID in the open set.
        /// </summary>
        private static AStarNode FindInOpenSet(List<AStarNode> openSet, int nodeId)
        {
            for (int i = 0; i < openSet.Count; i++)
            {
                if (openSet[i].Waypoint.nodeId == nodeId)
                    return openSet[i];
            }
            return null;
        }

        /// <summary>
        /// Reconstructs the path by following parent pointers from the end node.
        /// </summary>
        private static List<WaypointNode> ReconstructPath(AStarNode endNode)
        {
            var path = new List<WaypointNode>();
            var current = endNode;

            while (current != null)
            {
                path.Add(current.Waypoint);
                current = current.Parent;
            }

            path.Reverse();
            return path;
        }

        /// <summary>
        /// Calculates the total distance along a path.
        /// </summary>
        public static float CalculatePathDistance(List<WaypointNode> path)
        {
            if (path == null || path.Count < 2) return 0f;

            float totalDistance = 0f;
            for (int i = 0; i < path.Count - 1; i++)
            {
                totalDistance += path[i].DistanceTo(path[i + 1]);
            }
            return totalDistance;
        }
    }
}
