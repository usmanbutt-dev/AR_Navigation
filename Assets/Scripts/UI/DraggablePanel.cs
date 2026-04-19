using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nibrask.UI
{
    /// <summary>
    /// Attach to any world-space UI panel to make it drag-movable by touch.
    ///
    /// Uses Unity's EventSystem drag interfaces so it works with UI Canvases
    /// (GraphicRaycaster) instead of Physics.Raycast which only hits 3D colliders.
    ///
    /// How it plays with buttons:
    ///   - A quick tap on a button fires the button's onClick normally.
    ///   - A held drag on a button (or any graphic child) bubbles up to this
    ///     handler and moves the panel instead. Unity's EventSystem handles the
    ///     distinction automatically via its drag threshold.
    ///
    /// If the panel root has no Graphic component, an invisible Image is added
    /// at Awake so the full panel area is draggable, not just the buttons/text.
    /// </summary>
    public class DraggablePanel : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        // ── State ─────────────────────────────────────────────────────────────

        private Camera arCamera;
        private float panelDepth;          // distance from camera along forward
        private Vector3 dragOffset;        // offset from pivot to first touch point
        private Billboard billboard;       // paused during drag to prevent snap-back

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            arCamera = Camera.main;
            billboard = GetComponent<Billboard>();

            // The EventSystem will only detect touches on GameObjects that have
            // a Graphic component (Image, Text, etc.). If this panel root doesn't
            // have one, we add an invisible Image so the entire rect is draggable.
            EnsureRaycastGraphic();
        }

        // ── IBeginDragHandler ─────────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (arCamera == null) arCamera = Camera.main;
            if (arCamera == null) return;

            // Capture the panel's distance from camera so we move on the same plane
            panelDepth = Vector3.Dot(
                transform.position - arCamera.transform.position,
                arCamera.transform.forward);

            // Calculate offset so the panel doesn't snap its pivot to the finger
            Vector3 worldPoint = arCamera.ScreenToWorldPoint(
                new Vector3(eventData.position.x, eventData.position.y, panelDepth));
            dragOffset = transform.position - worldPoint;

            // Pause billboard so the panel doesn't fight the drag
            if (billboard != null)
                billboard.enabled = false;
        }

        // ── IDragHandler ──────────────────────────────────────────────────────

        public void OnDrag(PointerEventData eventData)
        {
            if (arCamera == null) return;

            Vector3 worldPoint = arCamera.ScreenToWorldPoint(
                new Vector3(eventData.position.x, eventData.position.y, panelDepth));
            transform.position = worldPoint + dragOffset;
        }

        // ── IEndDragHandler ───────────────────────────────────────────────────

        public void OnEndDrag(PointerEventData eventData)
        {
            // Re-enable billboard so the panel faces camera again after release
            if (billboard != null)
                billboard.enabled = true;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Ensures this GameObject has a raycast-targetable Graphic so the
        /// EventSystem can detect drags even on "empty" areas of the panel
        /// (not just on child buttons/text).
        /// </summary>
        private void EnsureRaycastGraphic()
        {
            // If there's already a Graphic on this GO, we're fine
            if (GetComponent<Graphic>() != null) return;

            var img = gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f); // fully transparent
            img.raycastTarget = true;

            Debug.Log($"[DraggablePanel] Added invisible raycast Image to '{gameObject.name}'.");
        }
    }
}
