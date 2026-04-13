using UnityEngine;
using Nibrask.Core;

namespace Nibrask.DebugUtils
{
    /// <summary>
    /// A simple on-screen debugger for forcing AppState transitions.
    /// Attach this to Canvas_WorldSpace or an empty GameObject to easily test panel transitions.
    /// </summary>
    public class CanvasDebugger : MonoBehaviour
    {
        [Tooltip("If true, draws big buttons on screen to manually change states.")]
        public bool showDebugUI = true;

        private void OnGUI()
        {
            if (!showDebugUI) return;
            if (AppStateManager.Instance == null) return;

            // Make the buttons large enough to press on a phone screen
            GUI.skin.button.fontSize = Screen.width / 30;
            if (GUI.skin.button.fontSize < 30) GUI.skin.button.fontSize = 30;

            int width = Screen.width / 2;
            int height = Screen.height / 10;

            GUILayout.BeginArea(new Rect(20, 50, width, Screen.height - 100));

            GUILayout.Label("FORCE APP STATE:", GUI.skin.button);

            if (GUILayout.Button("1. Onboarding", GUILayout.Height(height)))
                AppStateManager.Instance.TransitionTo(AppState.Onboarding);

            if (GUILayout.Button("2. Scanning", GUILayout.Height(height)))
                AppStateManager.Instance.TransitionTo(AppState.Scanning);

            if (GUILayout.Button("3. Dest Selection", GUILayout.Height(height)))
                AppStateManager.Instance.TransitionTo(AppState.DestinationSelection);

            if (GUILayout.Button("4. Navigating", GUILayout.Height(height)))
                AppStateManager.Instance.TransitionTo(AppState.Navigating);

            if (GUILayout.Button("5. Reached Dest", GUILayout.Height(height)))
                AppStateManager.Instance.TransitionTo(AppState.Arrival);

            GUILayout.EndArea();
        }
    }
}
