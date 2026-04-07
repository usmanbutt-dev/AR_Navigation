using UnityEngine;

namespace Nibrask.UI
{
    /// <summary>
    /// Makes a world-space UI element always face the AR camera.
    /// Attach to any world-space Canvas or 3D object that should billboard towards the viewer.
    /// Supports Y-axis-only rotation for more natural looking panels.
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField]
        [Tooltip("If true, only rotates around the Y axis (no tilting up/down)")]
        private bool yAxisOnly = true;

        [SerializeField]
        [Tooltip("If true, smoothly interpolates rotation instead of snapping")]
        private bool smoothRotation = true;

        [SerializeField]
        [Tooltip("Rotation interpolation speed (higher = faster)")]
        private float rotationSpeed = 8f;

        [SerializeField]
        [Tooltip("Optional: specific camera to face. If null, uses Camera.main")]
        private Camera targetCamera;

        private Transform cameraTransform;

        private void Start()
        {
            FindCamera();
        }

        private void LateUpdate()
        {
            if (cameraTransform == null)
            {
                FindCamera();
                if (cameraTransform == null) return;
            }

            Vector3 directionToCamera = cameraTransform.position - transform.position;

            if (yAxisOnly)
            {
                directionToCamera.y = 0f;
            }

            if (directionToCamera.sqrMagnitude < 0.001f) return;

            Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);

            if (smoothRotation)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    Time.deltaTime * rotationSpeed
                );
            }
            else
            {
                transform.rotation = targetRotation;
            }
        }

        private void FindCamera()
        {
            if (targetCamera != null)
            {
                cameraTransform = targetCamera.transform;
            }
            else
            {
                var mainCam = Camera.main;
                if (mainCam != null)
                    cameraTransform = mainCam.transform;
            }
        }
    }
}
