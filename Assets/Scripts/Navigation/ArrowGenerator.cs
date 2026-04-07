using System.Collections.Generic;
using UnityEngine;
using Nibrask.Core;

namespace Nibrask.Navigation
{
    /// <summary>
    /// Places and manages 3D arrow prefabs along the navigation path.
    /// Arrows are oriented in the direction of travel, change color when the user
    /// passes them (checkpoint feedback), and have a subtle bobbing animation.
    /// Uses object pooling for performance.
    /// </summary>
    public class ArrowGenerator : MonoBehaviour
    {
        [Header("Arrow Settings")]
        [SerializeField]
        [Tooltip("Prefab for the directional arrow")]
        private GameObject arrowPrefab;

        [SerializeField]
        [Tooltip("Distance between arrows along the path (meters)")]
        private float arrowSpacing = 2.0f;

        [SerializeField]
        [Tooltip("Height offset above the floor (meters)")]
        private float heightOffset = 0.05f;

        [SerializeField]
        [Tooltip("Scale of each arrow")]
        private Vector3 arrowScale = new Vector3(0.3f, 0.3f, 0.3f);

        [Header("Colors")]
        [SerializeField]
        private Color activeColor = new Color(0.0f, 0.9f, 0.3f, 1.0f);    // Green - ahead

        [SerializeField]
        private Color passedColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);    // Dim gray - passed

        [SerializeField]
        private Color turnColor = new Color(1.0f, 0.8f, 0.0f, 1.0f);      // Gold - at turns

        [Header("Animation")]
        [SerializeField]
        [Tooltip("Bobbing amplitude (meters)")]
        private float bobAmplitude = 0.03f;

        [SerializeField]
        [Tooltip("Bobbing speed")]
        private float bobSpeed = 2.0f;

        [SerializeField]
        [Tooltip("Pulse scale multiplier")]
        private float pulseAmount = 0.1f;

        [Header("Pool Settings")]
        [SerializeField]
        [Tooltip("Maximum number of arrows to keep active")]
        private int maxActiveArrows = 30;

        /// <summary>
        /// Active arrow instances and their metadata.
        /// </summary>
        private readonly List<ArrowInstance> activeArrows = new List<ArrowInstance>();
        private readonly Queue<GameObject> arrowPool = new Queue<GameObject>();

        private struct ArrowInstance
        {
            public GameObject gameObject;
            public Renderer renderer;
            public Vector3 basePosition;
            public int pathSegmentIndex;
            public bool isPassed;
            public bool isTurn;
            public float spawnTime;
        }

        private void OnEnable()
        {
            AppEvents.OnCheckpointReached += HandleCheckpointReached;
        }

        private void OnDisable()
        {
            AppEvents.OnCheckpointReached -= HandleCheckpointReached;
        }

        private void Update()
        {
            // Animate active arrows
            for (int i = 0; i < activeArrows.Count; i++)
            {
                var arrow = activeArrows[i];
                if (arrow.gameObject == null || !arrow.gameObject.activeSelf || arrow.isPassed) continue;

                // Bobbing animation
                float bob = Mathf.Sin((Time.time + arrow.spawnTime) * bobSpeed) * bobAmplitude;
                arrow.gameObject.transform.position = arrow.basePosition + Vector3.up * bob;

                // Subtle pulse
                float pulse = 1f + Mathf.Sin(Time.time * bobSpeed * 1.5f) * pulseAmount;
                arrow.gameObject.transform.localScale = arrowScale * pulse;
            }
        }

        /// <summary>
        /// Generates arrows along the specified navigation path.
        /// </summary>
        public void GenerateArrows(List<WaypointNode> path)
        {
            ClearArrows();

            if (path == null || path.Count < 2)
            {
                Debug.LogWarning("[ArrowGenerator] Path too short for arrows.");
                return;
            }

            int arrowCount = 0;

            for (int i = 0; i < path.Count - 1 && arrowCount < maxActiveArrows; i++)
            {
                Vector3 from = path[i].WorldPosition;
                Vector3 to = path[i + 1].WorldPosition;
                Vector3 direction = (to - from).normalized;
                float segmentLength = Vector3.Distance(from, to);

                // Detect turns: check angle between this segment and the next
                bool isTurnSegment = false;
                if (i + 2 < path.Count)
                {
                    Vector3 nextDir = (path[i + 2].WorldPosition - path[i + 1].WorldPosition).normalized;
                    float angle = Vector3.Angle(direction, nextDir);
                    isTurnSegment = angle > 30f;
                }

                // Place arrows along this segment
                float distance = 0f;
                while (distance < segmentLength && arrowCount < maxActiveArrows)
                {
                    Vector3 position = from + direction * distance;
                    position.y += heightOffset;

                    // Calculate rotation — arrow points in the direction of travel
                    Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);

                    // Mark the last arrow before a turn
                    bool isTurnArrow = isTurnSegment && (segmentLength - distance) < arrowSpacing;

                    SpawnArrow(position, rotation, i, isTurnArrow);
                    arrowCount++;
                    distance += arrowSpacing;
                }
            }

            // Place a final arrow at the destination
            if (path.Count >= 2 && arrowCount < maxActiveArrows)
            {
                Vector3 lastPos = path[path.Count - 1].WorldPosition + Vector3.up * heightOffset;
                Vector3 lastDir = (path[path.Count - 1].WorldPosition - path[path.Count - 2].WorldPosition).normalized;
                SpawnArrow(lastPos, Quaternion.LookRotation(lastDir, Vector3.up), path.Count - 1, false);
            }

            Debug.Log($"[ArrowGenerator] Generated {activeArrows.Count} arrows along path.");
        }

        /// <summary>
        /// Spawns or recycles an arrow at the specified position and rotation.
        /// </summary>
        private void SpawnArrow(Vector3 position, Quaternion rotation, int segmentIndex, bool isTurn)
        {
            GameObject arrowGo;

            if (arrowPool.Count > 0)
            {
                arrowGo = arrowPool.Dequeue();
                arrowGo.transform.position = position;
                arrowGo.transform.rotation = rotation;
                arrowGo.SetActive(true);
            }
            else if (arrowPrefab != null)
            {
                arrowGo = Instantiate(arrowPrefab, position, rotation, transform);
            }
            else
            {
                // Fallback: create a simple primitive arrow
                arrowGo = CreatePrimitiveArrow(position, rotation);
            }

            arrowGo.transform.localScale = arrowScale;

            var renderer = arrowGo.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                // Use material property block to avoid instantiating materials
                var mpb = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", isTurn ? turnColor : activeColor);

                // Set emission for URP
                mpb.SetColor("_EmissionColor", (isTurn ? turnColor : activeColor) * 2f);
                renderer.SetPropertyBlock(mpb);
            }

            activeArrows.Add(new ArrowInstance
            {
                gameObject = arrowGo,
                renderer = renderer,
                basePosition = position,
                pathSegmentIndex = segmentIndex,
                isPassed = false,
                isTurn = isTurn,
                spawnTime = Random.Range(0f, Mathf.PI * 2f) // Random phase offset for variety
            });
        }

        /// <summary>
        /// Creates a simple arrow using Unity primitives as fallback.
        /// </summary>
        private GameObject CreatePrimitiveArrow(Vector3 position, Quaternion rotation)
        {
            var arrow = new GameObject("Arrow");
            arrow.transform.position = position;
            arrow.transform.rotation = rotation;
            arrow.transform.SetParent(transform);

            // Arrow body (thin cube)
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.transform.SetParent(arrow.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(0.3f, 0.05f, 0.8f);
            // Remove the collider to avoid physics overhead
            var bodyCollider = body.GetComponent<Collider>();
            if (bodyCollider != null) Destroy(bodyCollider);

            // Arrow head (rotated cube as a diamond shape)
            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.transform.SetParent(arrow.transform);
            head.transform.localPosition = new Vector3(0f, 0f, 0.6f);
            head.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            head.transform.localScale = new Vector3(0.4f, 0.05f, 0.4f);
            var headCollider = head.GetComponent<Collider>();
            if (headCollider != null) Destroy(headCollider);

            return arrow;
        }

        /// <summary>
        /// Handles checkpoint events — marks arrows before the checkpoint as "passed".
        /// </summary>
        private void HandleCheckpointReached(int checkpointIndex)
        {
            for (int i = 0; i < activeArrows.Count; i++)
            {
                var arrow = activeArrows[i];
                if (arrow.pathSegmentIndex < checkpointIndex && !arrow.isPassed)
                {
                    arrow.isPassed = true;
                    activeArrows[i] = arrow;

                    // Fade to passed color
                    if (arrow.renderer != null)
                    {
                        var mpb = new MaterialPropertyBlock();
                        arrow.renderer.GetPropertyBlock(mpb);
                        mpb.SetColor("_BaseColor", passedColor);
                        mpb.SetColor("_EmissionColor", Color.black);
                        arrow.renderer.SetPropertyBlock(mpb);
                    }

                    // Optionally disable the arrow to reduce rendering cost
                    // arrow.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Removes all arrows and returns them to the pool.
        /// </summary>
        public void ClearArrows()
        {
            foreach (var arrow in activeArrows)
            {
                if (arrow.gameObject != null)
                {
                    arrow.gameObject.SetActive(false);
                    arrowPool.Enqueue(arrow.gameObject);
                }
            }
            activeArrows.Clear();
        }
    }
}
