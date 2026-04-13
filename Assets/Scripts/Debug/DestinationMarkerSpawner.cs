using UnityEngine;
using TMPro;
using Nibrask.Core;
using Nibrask.Data;

namespace Nibrask.Debug
{
    /// <summary>
    /// Debug helper that spawns a unique colored primitive at each destination's
    /// world position so you can visually identify locations in your room.
    /// Attach to any GameObject in the scene.
    /// </summary>
    public class DestinationMarkerSpawner : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField]
        [Tooltip("Scale of each marker primitive")]
        private float markerScale = 0.15f;

        [SerializeField]
        [Tooltip("Height offset above the floor")]
        private float heightOffset = 0.1f;

        private Transform terminalOrigin;
        private bool hasSpawned = false;

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
            terminalOrigin = anchor;

            var mapData = AppStateManager.Instance?.TerminalMap;
            if (mapData == null)
            {
                UnityEngine.Debug.LogWarning("[DestinationMarkerSpawner] No TerminalMapData found.");
                return;
            }

            foreach (var destination in mapData.destinations)
            {
                SpawnMarker(destination, mapData);
            }

            UnityEngine.Debug.Log($"[DestinationMarkerSpawner] Spawned {mapData.destinations.Count} destination markers.");
        }

        private void SpawnMarker(DestinationData dest, TerminalMapData mapData)
        {
            // Get world position
            Vector3 worldPos = mapData.GetDestinationWorldPosition(dest, terminalOrigin);
            worldPos.y += heightOffset;

            // Pick primitive type and color based on destination name
            PrimitiveType primType;
            Color color;
            GetMarkerStyle(dest, out primType, out color);

            // Create the primitive
            GameObject marker = GameObject.CreatePrimitive(primType);
            marker.name = $"Marker_{dest.destinationName}";
            marker.transform.position = worldPos;
            marker.transform.localScale = Vector3.one * markerScale;

            // Remove collider (not needed, avoids physics interference)
            var col = marker.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Set color
            var renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = color;
                // Make it emissive so it's visible in AR
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 1.5f);
                renderer.material = mat;
            }

            // Add a floating text label above the marker
            SpawnLabel(dest.destinationName, worldPos + Vector3.up * (markerScale + 0.05f));
        }

        private void SpawnLabel(string text, Vector3 position)
        {
            // Create a world-space canvas for the label
            GameObject labelGo = new GameObject($"Label_{text}");
            labelGo.transform.position = position;

            var canvas = labelGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rectTransform = labelGo.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(0.5f, 0.1f);
            labelGo.transform.localScale = Vector3.one * 0.005f;

            // Add TMP text
            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(labelGo.transform, false);

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 24;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            // Add billboard behavior so it faces the camera
            labelGo.AddComponent<Nibrask.UI.Billboard>();
        }

        private void GetMarkerStyle(DestinationData dest, out PrimitiveType primType, out Color color)
        {
            // Each destination gets a unique primitive + color combo
            switch (dest.destinationName)
            {
                case "Gate A12":
                    primType = PrimitiveType.Cube;
                    color = Color.green;
                    break;
                case "Gate A14":
                    primType = PrimitiveType.Cube;
                    color = Color.blue;
                    break;
                case "Gate B3":
                    primType = PrimitiveType.Cube;
                    color = Color.yellow;
                    break;
                case "Gate B7":
                    primType = PrimitiveType.Cube;
                    color = Color.magenta;
                    break;
                case "Main Exit":
                    primType = PrimitiveType.Cylinder;
                    color = Color.red;
                    break;
                case "Sky Lounge Caf\u00e9":
                    primType = PrimitiveType.Sphere;
                    color = new Color(1f, 0.5f, 0f); // Orange
                    break;
                case "Restroom A":
                    primType = PrimitiveType.Sphere;
                    color = Color.cyan;
                    break;
                case "Security Checkpoint":
                    primType = PrimitiveType.Capsule;
                    color = new Color(1f, 1f, 0.5f); // Light yellow
                    break;
                default:
                    primType = PrimitiveType.Sphere;
                    color = Color.white;
                    break;
            }
        }
    }
}
