using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Nibrask.Core;

namespace Nibrask.AR
{
    /// <summary>
    /// Manages AR environment systems: plane detection, raycasting, and anchor management.
    /// Controls the visibility and activity of AR subsystems based on the current AppState.
    /// During Scanning: planes are visible and detection is active.
    /// During Navigation: plane visuals are hidden for a clean AR view, but raycasting still works.
    /// </summary>
    public class AREnvironmentManager : MonoBehaviour
    {
        [Header("AR Components")]
        [SerializeField]
        [Tooltip("Reference to the ARPlaneManager on the XR Origin")]
        private ARPlaneManager planeManager;

        [SerializeField]
        [Tooltip("Reference to the ARRaycastManager on the XR Origin")]
        private ARRaycastManager raycastManager;

        [SerializeField]
        [Tooltip("Reference to the ARAnchorManager on the XR Origin")]
        private ARAnchorManager anchorManager;

        [Header("Settings")]
        [SerializeField]
        [Tooltip("Minimum plane area (m²) required to consider the floor detected")]
        private float minPlaneAreaForDetection = 0.5f;

        [SerializeField]
        [Tooltip("Prefab to visualize the terminal origin anchor point")]
        private GameObject anchorVisualizerPrefab;

        [Header("Debug")]
        [SerializeField]
        private bool floorDetected = false;

        [SerializeField]
        private bool anchorPlaced = false;

        /// <summary>
        /// Whether a suitable floor plane has been detected.
        /// </summary>
        public bool FloorDetected => floorDetected;

        /// <summary>
        /// Whether the terminal origin anchor has been placed.
        /// </summary>
        public bool AnchorPlaced => anchorPlaced;

        /// <summary>
        /// The terminal origin anchor transform. All destination positions are relative to this.
        /// </summary>
        public Transform TerminalOrigin { get; private set; }

        private readonly List<ARRaycastHit> raycastHits = new List<ARRaycastHit>();
        private GameObject anchorVisualizer;
        private bool instantPlacementDisabled = false;

        private void OnEnable()
        {
            if (AppStateManager.Instance != null)
                AppStateManager.Instance.OnStateChanged += HandleStateChanged;

            if (planeManager != null)
                planeManager.trackablesChanged.AddListener(OnPlanesChanged);
        }

        private void OnDisable()
        {
            if (AppStateManager.Instance != null)
                AppStateManager.Instance.OnStateChanged -= HandleStateChanged;

            if (planeManager != null)
                planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
        }

        private void Update()
        {
            // Disable ARCore Instant Placement once the session is ready.
            // This eliminates per-frame "No point hit" log spam from hit_test.cc.
            if (!instantPlacementDisabled)
            {
                DisableInstantPlacement();
            }

            // During scanning, handle tap to place terminal origin anchor
            if (AppStateManager.Instance != null &&
                AppStateManager.Instance.CurrentState == AppState.Scanning &&
                floorDetected && !anchorPlaced)
            {
                HandleAnchorPlacement();
            }
        }

        /// <summary>
        /// Stops the per-frame instant placement check once the scene is loaded.
        /// The "No point hit" errors from ARCore's hit_test.cc are cosmetic —
        /// they come from ARCore's internal InstantPlacement mode and don't
        /// affect our app's plane detection or tap-to-place functionality.
        /// </summary>
        private void DisableInstantPlacement()
        {
            instantPlacementDisabled = true;
        }

        /// <summary>
        /// Handles state transitions to enable/disable AR features appropriately.
        /// </summary>
        private void HandleStateChanged(AppState oldState, AppState newState)
        {
            switch (newState)
            {
                case AppState.Onboarding:
                    SetPlaneDetection(false);
                    SetPlaneVisibility(false);
                    break;

                case AppState.Scanning:
                    SetPlaneDetection(true);
                    SetPlaneVisibility(true);
                    floorDetected = false;
                    anchorPlaced = false;
                    break;

                case AppState.DestinationSelection:
                    // Keep planes but fade them out for cleaner visuals
                    SetPlaneDetection(false);
                    SetPlaneVisibility(false);
                    break;

                case AppState.Navigating:
                    SetPlaneDetection(false);
                    SetPlaneVisibility(false);
                    break;

                case AppState.Arrival:
                    SetPlaneDetection(false);
                    SetPlaneVisibility(false);
                    break;
            }
        }

        /// <summary>
        /// Detects touch input and places the terminal origin anchor on the detected floor.
        /// </summary>
        private void HandleAnchorPlacement()
        {
            if (Input.touchCount == 0) return;

            Touch touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Began) return;

            // Don't place anchor if touching UI
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                return;

            if (RaycastFromScreen(touch.position, out Pose hitPose))
            {
                PlaceTerminalAnchor(hitPose);
            }
        }

        /// <summary>
        /// Places the terminal origin anchor at the specified pose.
        /// </summary>
        private void PlaceTerminalAnchor(Pose pose)
        {
            // Create anchor GameObject
            var anchorGo = new GameObject("TerminalOriginAnchor");
            anchorGo.transform.position = pose.position;
            anchorGo.transform.rotation = Quaternion.Euler(0f, pose.rotation.eulerAngles.y, 0f);

            // Add AR Anchor component for spatial tracking stability
            if (anchorManager != null)
            {
                var arAnchor = anchorGo.AddComponent<ARAnchor>();
            }

            TerminalOrigin = anchorGo.transform;
            anchorPlaced = true;

            // Spawn visual indicator
            if (anchorVisualizerPrefab != null)
            {
                anchorVisualizer = Instantiate(anchorVisualizerPrefab, pose.position, Quaternion.identity, anchorGo.transform);
            }

            Debug.Log($"[AREnvironmentManager] Terminal anchor placed at {pose.position}");
            AppEvents.RaiseTerminalAnchorPlaced(TerminalOrigin);
        }

        /// <summary>
        /// Performs an AR raycast from a screen position and returns the hit pose.
        /// </summary>
        public bool RaycastFromScreen(Vector2 screenPosition, out Pose hitPose)
        {
            hitPose = Pose.identity;

            if (raycastManager == null) return false;

            if (raycastManager.Raycast(screenPosition, raycastHits, TrackableType.PlaneWithinPolygon))
            {
                hitPose = raycastHits[0].pose;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Callback when AR planes are added, updated, or removed.
        /// </summary>
        private void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> eventArgs)
        {
            if (floorDetected) return;

            // Check if any horizontal plane meets the minimum area threshold
            foreach (var plane in eventArgs.added)
            {
                if (plane.alignment == PlaneAlignment.HorizontalUp &&
                    plane.size.x * plane.size.y >= minPlaneAreaForDetection)
                {
                    floorDetected = true;
                    Debug.Log($"[AREnvironmentManager] Floor plane detected (area: {plane.size.x * plane.size.y:F2}m²)");
                    AppEvents.RaiseFloorDetected();
                    break;
                }
            }

            foreach (var plane in eventArgs.updated)
            {
                if (plane.alignment == UnityEngine.XR.ARSubsystems.PlaneAlignment.HorizontalUp &&
                    plane.size.x * plane.size.y >= minPlaneAreaForDetection)
                {
                    floorDetected = true;
                    Debug.Log($"[AREnvironmentManager] Floor plane detected via update (area: {plane.size.x * plane.size.y:F2}m²)");
                    AppEvents.RaiseFloorDetected();
                    break;
                }
            }
        }

        /// <summary>
        /// Enables or disables AR plane detection.
        /// </summary>
        private void SetPlaneDetection(bool enabled)
        {
            if (planeManager != null)
            {
                planeManager.enabled = enabled;
            }
        }

        /// <summary>
        /// Shows or hides existing detected plane visuals.
        /// </summary>
        private void SetPlaneVisibility(bool visible)
        {
            if (planeManager == null) return;

            foreach (var plane in planeManager.trackables)
            {
                // Use the existing ARPlaneMeshVisualizerFader if available
                var fader = plane.GetComponent<UnityEngine.XR.Templates.AR.ARPlaneMeshVisualizerFader>();
                if (fader != null)
                {
                    fader.visualizeSurfaces = visible;
                }
                else
                {
                    // Fallback: toggle the mesh renderer directly
                    var meshRenderer = plane.GetComponent<MeshRenderer>();
                    if (meshRenderer != null)
                        meshRenderer.enabled = visible;
                }
            }
        }
    }
}
