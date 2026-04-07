using UnityEngine;
using Nibrask.Core;

namespace Nibrask.Feedback
{
    /// <summary>
    /// Manages audio feedback for navigation events.
    /// Plays sound effects for key events like navigation start, checkpoint reached,
    /// off-route warning, route recalculation, and arrival.
    /// Uses AudioSource pooling to handle overlapping sounds.
    /// </summary>
    public class AudioFeedbackManager : MonoBehaviour
    {
        [Header("Audio Clips")]
        [SerializeField]
        [Tooltip("Sound played when navigation starts")]
        private AudioClip navigationStartClip;

        [SerializeField]
        [Tooltip("Sound played when a checkpoint is reached")]
        private AudioClip checkpointClip;

        [SerializeField]
        [Tooltip("Sound played when the user goes off-route")]
        private AudioClip offRouteClip;

        [SerializeField]
        [Tooltip("Sound played when the route is recalculated")]
        private AudioClip routeRecalculatedClip;

        [SerializeField]
        [Tooltip("Sound played when the user returns to the route")]
        private AudioClip backOnRouteClip;

        [SerializeField]
        [Tooltip("Sound played on arrival")]
        private AudioClip arrivalClip;

        [Header("Settings")]
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Master volume for all feedback sounds")]
        private float masterVolume = 0.7f;

        [SerializeField]
        [Tooltip("Number of pooled AudioSources for overlapping sounds")]
        private int audioSourcePoolSize = 4;

        [SerializeField]
        [Tooltip("Minimum time between same sound plays (prevents spam)")]
        private float minPlayInterval = 0.5f;

        private AudioSource[] audioSourcePool;
        private int currentSourceIndex = 0;
        private float lastPlayTime = 0f;

        private void Awake()
        {
            // Create audio source pool
            audioSourcePool = new AudioSource[audioSourcePoolSize];
            for (int i = 0; i < audioSourcePoolSize; i++)
            {
                var sourceGo = new GameObject($"AudioSource_{i}");
                sourceGo.transform.SetParent(transform);
                audioSourcePool[i] = sourceGo.AddComponent<AudioSource>();
                audioSourcePool[i].playOnAwake = false;
                audioSourcePool[i].spatialBlend = 0f; // 2D sound (non-spatial)
            }
        }

        private void OnEnable()
        {
            AppEvents.OnNavigationStarted += HandleNavigationStarted;
            AppEvents.OnCheckpointReached += HandleCheckpointReached;
            AppEvents.OnOffRoute += HandleOffRoute;
            AppEvents.OnBackOnRoute += HandleBackOnRoute;
            AppEvents.OnRouteRecalculated += HandleRouteRecalculated;
            AppEvents.OnArrived += HandleArrived;
        }

        private void OnDisable()
        {
            AppEvents.OnNavigationStarted -= HandleNavigationStarted;
            AppEvents.OnCheckpointReached -= HandleCheckpointReached;
            AppEvents.OnOffRoute -= HandleOffRoute;
            AppEvents.OnBackOnRoute -= HandleBackOnRoute;
            AppEvents.OnRouteRecalculated -= HandleRouteRecalculated;
            AppEvents.OnArrived -= HandleArrived;
        }

        /// <summary>
        /// Plays an audio clip using the next available AudioSource from the pool.
        /// </summary>
        public void PlayClip(AudioClip clip, float volumeMultiplier = 1f)
        {
            if (clip == null) return;

            // Prevent spamming
            if (Time.time - lastPlayTime < minPlayInterval) return;
            lastPlayTime = Time.time;

            var source = audioSourcePool[currentSourceIndex];
            source.clip = clip;
            source.volume = masterVolume * volumeMultiplier;
            source.Play();

            currentSourceIndex = (currentSourceIndex + 1) % audioSourcePoolSize;
        }

        /// <summary>
        /// Sets the master volume for all feedback sounds.
        /// </summary>
        public void SetVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
        }

        // ── Event Handlers ─────────────────────────────────────────────

        private void HandleNavigationStarted()
        {
            PlayClip(navigationStartClip);
        }

        private void HandleCheckpointReached(int index)
        {
            PlayClip(checkpointClip, 0.6f);
        }

        private void HandleOffRoute()
        {
            PlayClip(offRouteClip, 1f);
        }

        private void HandleBackOnRoute()
        {
            PlayClip(backOnRouteClip, 0.8f);
        }

        private void HandleRouteRecalculated()
        {
            PlayClip(routeRecalculatedClip, 0.7f);
        }

        private void HandleArrived(Data.DestinationData destination)
        {
            PlayClip(arrivalClip, 1f);
        }
    }
}
