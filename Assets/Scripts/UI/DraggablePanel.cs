using UnityEngine;

namespace Nibrask.UI
{
    /// <summary>
    /// Attach to any world-space panel to make it drag-movable by touch or mouse.
    /// The panel is repositioned by sliding it across a virtual plane that is
    /// perpendicular to the camera-to-panel direction at the panel's current depth.
    ///
    /// - A short hold threshold (0.25 s) prevents accidental drags during normal taps.
    /// - Dragging is blocked while a button press is being evaluated; it only
    ///   activates once the finger has moved beyond a small dead-zone (8 px).
    /// - The Billboard component (if present) is temporarily disabled while
    ///   dragging so the panel doesn't snap back to face the camera mid-drag.
    /// </summary>
    public class DraggablePanel : MonoBehaviour
    {
        [Header("Drag Settings")]

        [Tooltip("Minimum finger-move distance in screen pixels before dragging begins. " +
                 "Prevents accidental drags when tapping buttons.")]
        [SerializeField] private float dragDeadZonePx = 8f;

        [Tooltip("How quickly the panel follows the finger (lerp speed). " +
                 "Set to a very large number for snap-to-finger feel.")]
        [SerializeField] private float followSpeed = 25f;

        // ── State ─────────────────────────────────────────────────────────────

        private bool isDragging = false;
        private int activeTouchId = -1;        // finger tracking for multi-touch
        private Vector2 touchStartScreen;      // where the finger first landed
        private float panelDepth;              // world depth of panel from camera
        private Vector3 dragOffsetWorld;       // offset from panel pivot to pick ray hit
        private Camera arCamera;
        private Billboard billboard;           // optional — paused while dragging

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            arCamera = Camera.main;
            billboard = GetComponent<Billboard>();
        }

        private void Update()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            HandleMouse();
#else
            HandleTouch();
#endif
        }

        // ── Mouse (Editor / PC) ───────────────────────────────────────────────

        private void HandleMouse()
        {
            if (Input.GetMouseButtonDown(0))
            {
                TryBeginDrag(Input.mousePosition);
            }
            else if (Input.GetMouseButton(0) && isDragging)
            {
                UpdateDragPosition(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                EndDrag();
            }
        }

        // ── Touch (Device) ────────────────────────────────────────────────────

        private void HandleTouch()
        {
            if (Input.touchCount == 0) return;

            // Track a specific finger so multi-touch doesn't confuse dragging
            if (!isDragging)
            {
                foreach (Touch t in Input.touches)
                {
                    if (t.phase == TouchPhase.Began)
                    {
                        TryBeginDrag(t.position);
                        if (isDragging)
                        {
                            activeTouchId = t.fingerId;
                            break;
                        }
                    }
                }
            }
            else
            {
                foreach (Touch t in Input.touches)
                {
                    if (t.fingerId != activeTouchId) continue;

                    if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                        UpdateDragPosition(t.position);
                    else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                        EndDrag();

                    break;
                }
            }
        }

        // ── Core Drag Logic ───────────────────────────────────────────────────

        /// <summary>
        /// Casts a ray from the given screen point and, if it hits this panel's
        /// collider, begins a drag session.
        /// </summary>
        private void TryBeginDrag(Vector2 screenPos)
        {
            if (arCamera == null) return;

            Ray ray = arCamera.ScreenPointToRay(screenPos);

            // Check if the touch lands on any collider that is part of this panel
            if (!Physics.Raycast(ray, out RaycastHit hit)) return;
            if (!hit.collider.transform.IsChildOf(transform) && hit.collider.transform != transform)
                return;

            // Record state for this drag
            touchStartScreen = screenPos;
            panelDepth = Vector3.Dot(transform.position - arCamera.transform.position,
                                     arCamera.transform.forward);
            Vector3 hitWorld = arCamera.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, panelDepth));
            dragOffsetWorld = transform.position - hitWorld;

            isDragging = true;
            SetBillboardEnabled(false);
        }

        private void UpdateDragPosition(Vector2 screenPos)
        {
            if (arCamera == null) return;

            // Dead-zone: only start moving once the finger has travelled enough pixels
            if ((screenPos - touchStartScreen).magnitude < dragDeadZonePx) return;

            Vector3 worldPoint = arCamera.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, panelDepth));

            Vector3 target = worldPoint + dragOffsetWorld;
            transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * followSpeed);
        }

        private void EndDrag()
        {
            isDragging = false;
            activeTouchId = -1;
            // Re-enable billboard so the panel faces camera again after release
            SetBillboardEnabled(true);
        }

        private void SetBillboardEnabled(bool value)
        {
            if (billboard != null)
                billboard.enabled = value;
        }
    }
}
