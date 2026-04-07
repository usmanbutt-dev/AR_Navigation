using System.Collections.Generic;
using UnityEngine;
using Nibrask.Core;

namespace Nibrask.Navigation
{
    /// <summary>
    /// Renders the navigation path on the AR floor using a LineRenderer.
    /// Displays a glowing line from the user's position through waypoints to the destination.
    /// Supports animated UV scrolling for directional flow indication.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class PathRenderer : MonoBehaviour
    {
        [Header("Path Settings")]
        [SerializeField]
        [Tooltip("Height offset above the floor for the path line (meters)")]
        private float heightOffset = 0.02f;

        [SerializeField]
        [Tooltip("Width of the path line")]
        private float lineWidth = 0.15f;

        [SerializeField]
        [Tooltip("Number of interpolation points between waypoints for smoother curves")]
        private int interpolationSteps = 3;

        [Header("Colors")]
        [SerializeField]
        private Color pathColorNear = new Color(0.0f, 0.9f, 0.4f, 0.8f);  // Bright green

        [SerializeField]
        private Color pathColorFar = new Color(0.0f, 0.5f, 0.9f, 0.5f);   // Blue

        [Header("Animation")]
        [SerializeField]
        [Tooltip("Speed of the UV scroll animation along the path")]
        private float scrollSpeed = 1.5f;

        [SerializeField]
        [Tooltip("Material for the path line (should support UV animation)")]
        private Material pathMaterial;

        private LineRenderer lineRenderer;
        private List<Vector3> pathPoints = new List<Vector3>();
        private bool isActive = false;

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            ConfigureLineRenderer();
        }

        private void Update()
        {
            if (!isActive || pathMaterial == null) return;

            // Animate the UV offset for directional flow effect
            float offset = Time.time * scrollSpeed;
            pathMaterial.SetTextureOffset("_BaseMap", new Vector2(-offset, 0f));
        }

        /// <summary>
        /// Configures the LineRenderer with optimal settings for AR floor rendering.
        /// </summary>
        private void ConfigureLineRenderer()
        {
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.useWorldSpace = true;
            lineRenderer.alignment = LineAlignment.TransformZ;
            lineRenderer.textureMode = LineTextureMode.Tile;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.numCornerVertices = 4;
            lineRenderer.numCapVertices = 4;

            if (pathMaterial != null)
            {
                lineRenderer.material = pathMaterial;
            }

            // Set gradient: green near → blue far
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(pathColorNear, 0f),
                    new GradientColorKey(pathColorFar, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.5f, 1f)
                }
            );
            lineRenderer.colorGradient = gradient;
        }

        /// <summary>
        /// Renders the path along the given list of waypoint nodes.
        /// </summary>
        public void RenderPath(List<WaypointNode> path)
        {
            if (path == null || path.Count < 2)
            {
                ClearPath();
                return;
            }

            pathPoints.Clear();

            // Generate smooth points along the path
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 start = path[i].WorldPosition + Vector3.up * heightOffset;
                Vector3 end = path[i + 1].WorldPosition + Vector3.up * heightOffset;

                // Add interpolated points for smoother curves
                for (int step = 0; step < interpolationSteps; step++)
                {
                    float t = (float)step / interpolationSteps;
                    pathPoints.Add(Vector3.Lerp(start, end, t));
                }
            }

            // Add the final point
            pathPoints.Add(path[path.Count - 1].WorldPosition + Vector3.up * heightOffset);

            // Apply to LineRenderer
            lineRenderer.positionCount = pathPoints.Count;
            lineRenderer.SetPositions(pathPoints.ToArray());
            lineRenderer.enabled = true;
            isActive = true;

            Debug.Log($"[PathRenderer] Rendered path with {pathPoints.Count} points across {path.Count} waypoints.");
        }

        /// <summary>
        /// Updates the start of the path to follow the user's position.
        /// </summary>
        public void UpdateStartPosition(Vector3 userPosition)
        {
            if (!isActive || pathPoints.Count == 0) return;

            pathPoints[0] = userPosition + Vector3.up * heightOffset;
            lineRenderer.SetPosition(0, pathPoints[0]);
        }

        /// <summary>
        /// Removes waypoints that the user has already passed (shrinks the path from the start).
        /// </summary>
        public void TrimPathToCheckpoint(int checkpointIndex, List<WaypointNode> fullPath)
        {
            if (checkpointIndex <= 0 || checkpointIndex >= fullPath.Count) return;

            // Re-render from the current checkpoint onwards
            var remainingPath = fullPath.GetRange(checkpointIndex, fullPath.Count - checkpointIndex);
            RenderPath(remainingPath);
        }

        /// <summary>
        /// Clears the rendered path.
        /// </summary>
        public void ClearPath()
        {
            pathPoints.Clear();
            lineRenderer.positionCount = 0;
            lineRenderer.enabled = false;
            isActive = false;
        }
    }
}
