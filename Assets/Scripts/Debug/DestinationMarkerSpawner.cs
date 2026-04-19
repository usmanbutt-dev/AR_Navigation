using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Nibrask.Core;
using Nibrask.Data;

namespace Nibrask.DebugUtils
{
    /// <summary>
    /// Spawns a prefab (or colored primitive fallback) at each destination's
    /// world-space position when the terminal anchor is placed.
    /// Assign prefabs per destination type in the Inspector — leave a slot
    /// empty to use the colored-primitive fallback for that type.
    /// </summary>
    public class DestinationMarkerSpawner : MonoBehaviour
    {
        // ── Per-type prefabs ──────────────────────────────────────────────
        [System.Serializable]
        public class TypePrefabEntry
        {
            [Tooltip("Destination type this prefab represents")]
            public DestinationType destinationType;

            [Tooltip("Prefab to instantiate. Leave empty to use the colored-primitive fallback.")]
            public GameObject prefab;
        }

        [Header("Prefabs — assign one per destination type")]
        [Tooltip("Map destination types to prefabs. Any unassigned type falls back to a colored primitive.")]
        [SerializeField]
        private List<TypePrefabEntry> typePrefabs = new List<TypePrefabEntry>();

        [Header("Fallback primitive settings (used when no prefab is assigned)")]
        [SerializeField]
        [Tooltip("Scale of the fallback primitive marker")]
        private float fallbackMarkerScale = 0.15f;

        [Header("Label settings")]
        [SerializeField]
        [Tooltip("Show a floating text label above each marker")]
        private bool showLabels = true;

        [SerializeField]
        [Tooltip("Height above the marker to place the label")]
        private float labelHeightOffset = 0.6f;

        [Header("General")]
        [SerializeField]
        [Tooltip("Height offset above the floor anchor")]
        private float heightOffset = 0.1f;

        // Internal
        private bool hasSpawned = false;
        private readonly List<GameObject> spawnedMarkers = new List<GameObject>();

        private void OnEnable()
        {
            AppEvents.OnTerminalAnchorPlaced += HandleAnchorPlaced;
        }

        private void OnDisable()
        {
            AppEvents.OnTerminalAnchorPlaced -= HandleAnchorPlaced;
        }

        private void HandleAnchorPlaced(Transform anchor)
        {
            if (hasSpawned) return;
            hasSpawned = true;

            var mapData = AppStateManager.Instance?.TerminalMap;
            if (mapData == null)
            {
                Debug.LogWarning("[DestinationMarkerSpawner] No TerminalMapData found on AppStateManager.");
                return;
            }

            foreach (var destination in mapData.destinations)
            {
                SpawnMarker(destination, mapData, anchor);
            }

            Debug.Log($"[DestinationMarkerSpawner] Spawned {mapData.destinations.Count} destination markers.");
        }

        private void SpawnMarker(DestinationData dest, TerminalMapData mapData, Transform anchor)
        {
            Vector3 worldPos = mapData.GetDestinationWorldPosition(dest, anchor);
            worldPos.y += heightOffset;

            GameObject marker = null;

            // Try to find a prefab for this destination type
            GameObject prefab = GetPrefabForType(dest.destinationType);

            if (prefab != null)
            {
                // Instantiate the assigned prefab
                marker = Instantiate(prefab, worldPos, Quaternion.identity, transform);
                marker.name = $"Marker_{dest.destinationName}";

                // Add a colored ring/base under the prefab to differentiate
                // gates that share the same prefab model
                SpawnColoredBase(dest, worldPos, marker.transform);
            }
            else
            {
                // No prefab assigned — fall back to a colored primitive
                marker = SpawnFallbackPrimitive(dest, worldPos);
            }

            if (marker == null) return;
            spawnedMarkers.Add(marker);

            // Add floating label if enabled
            if (showLabels)
            {
                SpawnLabel(dest.destinationName, worldPos + Vector3.up * labelHeightOffset, marker.transform);
            }
        }

        /// <summary>
        /// Spawns a flat colored cylinder ring under a marker to visually differentiate
        /// destinations that share the same prefab.
        /// </summary>
        private void SpawnColoredBase(DestinationData dest, Vector3 worldPos, Transform parent)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "ColorBase";
            ring.transform.SetParent(parent, worldPositionStays: true);
            ring.transform.position = worldPos - Vector3.up * heightOffset; // flush with floor
            ring.transform.localScale = new Vector3(0.5f, 0.01f, 0.5f); // flat disc

            // Remove collider
            var col = ring.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Unique color per destination using name hash
            Color baseColor = GetDestinationColor(dest);
            var rend = ring.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (mat == null) mat = new Material(Shader.Find("Standard"));
                mat.color = baseColor;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", baseColor * 2f);
                rend.material = mat;
            }
        }

        /// <summary>
        /// Returns a unique color for a destination, using the name hash for gates
        /// and fixed colors for service types.
        /// </summary>
        private Color GetDestinationColor(DestinationData dest)
        {
            switch (dest.destinationType)
            {
                case DestinationType.Gate:
                    float h = (float)(Mathf.Abs(dest.destinationName.GetHashCode()) % 360) / 360f;
                    return Color.HSVToRGB(h, 0.8f, 1f);
                case DestinationType.Restroom:
                    return Color.cyan;
                case DestinationType.Restaurant:
                    return new Color(1f, 0.5f, 0f);
                case DestinationType.SecurityCheckpoint:
                    return new Color(1f, 1f, 0.3f);
                case DestinationType.Exit:
                    return Color.red;
                case DestinationType.Lounge:
                    return new Color(0.6f, 0.2f, 1f);
                default:
                    return Color.white;
            }
        }

        /// <summary>
        /// Returns the prefab assigned to the given destination type, or null if none.
        /// </summary>
        private GameObject GetPrefabForType(DestinationType type)
        {
            foreach (var entry in typePrefabs)
            {
                if (entry.destinationType == type && entry.prefab != null)
                    return entry.prefab;
            }
            return null;
        }

        /// <summary>
        /// Creates a simple colored primitive as a fallback when no prefab is assigned.
        /// Each destination type gets a unique shape and color.
        /// </summary>
        private GameObject SpawnFallbackPrimitive(DestinationData dest, Vector3 worldPos)
        {
            PrimitiveType primType;
            Color color;
            GetFallbackStyle(dest, out primType, out color);

            var marker = GameObject.CreatePrimitive(primType);
            marker.name = $"Marker_{dest.destinationName}";
            marker.transform.position = worldPos;
            marker.transform.localScale = Vector3.one * fallbackMarkerScale;
            marker.transform.SetParent(transform);

            // Remove collider
            var col = marker.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Apply color with emission
            var rend = marker.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (mat == null) mat = new Material(Shader.Find("Standard"));
                mat.color = color;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 1.5f);
                rend.material = mat;
            }

            return marker;
        }

        /// <summary>
        /// Spawns a world-space text label above a marker.
        /// </summary>
        private void SpawnLabel(string text, Vector3 position, Transform parent)
        {
            var labelGo = new GameObject($"Label_{text}");
            labelGo.transform.position = position;
            labelGo.transform.SetParent(parent, worldPositionStays: true);

            var canvas = labelGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            // Scale large enough to be readable from 5–10m away
            labelGo.transform.localScale = Vector3.one * 0.005f;

            var rectTransform = labelGo.GetComponent<RectTransform>();
            // Wide canvas so long names fit comfortably on one line
            rectTransform.sizeDelta = new Vector2(400f, 60f);

            // Dark background panel for readability against any AR scene
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(labelGo.transform, false);
            var bgImage = bgGo.AddComponent<UnityEngine.UI.Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.7f);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = new Vector2(-16f, -6f);
            bgRect.offsetMax = new Vector2(16f, 6f);

            // Text element — large font, auto-sizing to fit
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(labelGo.transform, false);

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 36;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 14;
            tmp.fontSizeMax = 36;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            // Billboard so the label faces the camera
            labelGo.AddComponent<Nibrask.UI.Billboard>();
        }

        /// <summary>
        /// Returns a fallback primitive type and color based on the destination.
        /// Called only when no prefab is assigned for that type.
        /// </summary>
        private void GetFallbackStyle(DestinationData dest, out PrimitiveType primType, out Color color)
        {
            switch (dest.destinationType)
            {
                case DestinationType.Gate:
                    primType = PrimitiveType.Cube;
                    // Different shades of blue/green for each gate based on name hash
                    float h = (float)(Mathf.Abs(dest.destinationName.GetHashCode()) % 100) / 100f;
                    color = Color.HSVToRGB(h * 0.3f + 0.4f, 0.9f, 1f); // green-cyan range
                    break;
                case DestinationType.Restroom:
                    primType = PrimitiveType.Sphere;
                    color = Color.cyan;
                    break;
                case DestinationType.Restaurant:
                    primType = PrimitiveType.Sphere;
                    color = new Color(1f, 0.5f, 0f); // Orange
                    break;
                case DestinationType.SecurityCheckpoint:
                    primType = PrimitiveType.Capsule;
                    color = new Color(1f, 1f, 0.3f); // Yellow
                    break;
                case DestinationType.Exit:
                    primType = PrimitiveType.Cylinder;
                    color = Color.red;
                    break;
                case DestinationType.Lounge:
                    primType = PrimitiveType.Sphere;
                    color = new Color(0.6f, 0.2f, 1f); // Purple
                    break;
                default:
                    primType = PrimitiveType.Sphere;
                    color = Color.white;
                    break;
            }
        }

        /// <summary>
        /// Destroys all spawned markers (useful if you want to re-spawn after map reload).
        /// </summary>
        public void ClearMarkers()
        {
            foreach (var marker in spawnedMarkers)
            {
                if (marker != null) Destroy(marker);
            }
            spawnedMarkers.Clear();
            hasSpawned = false;
        }
    }
}

