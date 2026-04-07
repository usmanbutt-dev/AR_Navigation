using UnityEngine;
using Nibrask.Core;

namespace Nibrask.Feedback
{
    /// <summary>
    /// Provides haptic/vibration feedback for key navigation events on Android devices.
    /// Uses Unity's Handheld.Vibrate() for basic vibration and Android's VibrationEffect
    /// for more nuanced patterns when available.
    /// </summary>
    public class HapticFeedbackManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField]
        [Tooltip("Whether haptic feedback is enabled")]
        private bool hapticsEnabled = true;

        [SerializeField]
        [Tooltip("Vibration duration for checkpoint events (milliseconds)")]
        private long checkpointVibrationMs = 50;

        [SerializeField]
        [Tooltip("Vibration duration for off-route warning (milliseconds)")]
        private long offRouteVibrationMs = 200;

        [SerializeField]
        [Tooltip("Vibration duration for arrival (milliseconds)")]
        private long arrivalVibrationMs = 300;

        [SerializeField]
        [Tooltip("Minimum time between vibrations (seconds)")]
        private float vibrationCooldown = 0.5f;

        private float lastVibrationTime;

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject vibrator;
        private bool hasVibrator = false;
#endif

        private void Awake()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    hasVibrator = vibrator != null && vibrator.Call<bool>("hasVibrator");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[HapticFeedbackManager] Failed to initialize Android vibrator: {e.Message}");
                hasVibrator = false;
            }
#endif
        }

        private void OnEnable()
        {
            AppEvents.OnCheckpointReached += HandleCheckpointReached;
            AppEvents.OnOffRoute += HandleOffRoute;
            AppEvents.OnBackOnRoute += HandleBackOnRoute;
            AppEvents.OnArrived += HandleArrived;
            AppEvents.OnNavigationStarted += HandleNavigationStarted;
        }

        private void OnDisable()
        {
            AppEvents.OnCheckpointReached -= HandleCheckpointReached;
            AppEvents.OnOffRoute -= HandleOffRoute;
            AppEvents.OnBackOnRoute -= HandleBackOnRoute;
            AppEvents.OnArrived -= HandleArrived;
            AppEvents.OnNavigationStarted -= HandleNavigationStarted;
        }

        /// <summary>
        /// Triggers a haptic vibration with the specified duration.
        /// </summary>
        public void Vibrate(long durationMs)
        {
            if (!hapticsEnabled) return;
            if (Time.time - lastVibrationTime < vibrationCooldown) return;

            lastVibrationTime = Time.time;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (hasVibrator && vibrator != null)
            {
                try
                {
                    // Use VibrationEffect for API 26+ (which we target)
                    using (var vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    {
                        var effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                            "createOneShot", durationMs, -1 // -1 = default amplitude
                        );
                        vibrator.Call("vibrate", effect);
                    }
                }
                catch (System.Exception)
                {
                    // Fallback to simple vibrate
                    Handheld.Vibrate();
                }
            }
            else
            {
                Handheld.Vibrate();
            }
#else
            Debug.Log($"[HapticFeedback] Vibrate {durationMs}ms (editor simulation)");
#endif
        }

        /// <summary>
        /// Triggers a vibration pattern (alternating wait/vibrate durations).
        /// </summary>
        public void VibratePattern(long[] pattern, int repeat = -1)
        {
            if (!hapticsEnabled) return;
            if (Time.time - lastVibrationTime < vibrationCooldown) return;

            lastVibrationTime = Time.time;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (hasVibrator && vibrator != null)
            {
                try
                {
                    vibrator.Call("vibrate", pattern, repeat);
                }
                catch (System.Exception)
                {
                    Handheld.Vibrate();
                }
            }
#else
            Debug.Log($"[HapticFeedback] Pattern vibrate (editor simulation)");
#endif
        }

        /// <summary>
        /// Enables or disables haptic feedback.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            hapticsEnabled = enabled;
        }

        // ── Event Handlers ─────────────────────────────────────────────

        private void HandleNavigationStarted()
        {
            Vibrate(checkpointVibrationMs);
        }

        private void HandleCheckpointReached(int index)
        {
            Vibrate(checkpointVibrationMs);
        }

        private void HandleOffRoute()
        {
            // Double-pulse pattern for warning
            VibratePattern(new long[] { 0, offRouteVibrationMs, 100, offRouteVibrationMs });
        }

        private void HandleBackOnRoute()
        {
            Vibrate(checkpointVibrationMs);
        }

        private void HandleArrived(Data.DestinationData destination)
        {
            // Success pattern: three quick pulses
            VibratePattern(new long[] { 0, arrivalVibrationMs, 100, arrivalVibrationMs / 2, 100, arrivalVibrationMs / 2 });
        }
    }
}
