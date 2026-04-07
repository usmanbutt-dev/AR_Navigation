using UnityEngine;

namespace Nibrask.Data
{
    /// <summary>
    /// Types of destinations available in the airport terminal.
    /// </summary>
    public enum DestinationType
    {
        Gate,
        Restroom,
        Restaurant,
        SecurityCheckpoint,
        Exit,
        Information,
        Lounge
    }

    /// <summary>
    /// ScriptableObject representing a single navigable destination in the airport terminal.
    /// Contains metadata like name, type, icon, and spatial position relative to the terminal origin.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDestination", menuName = "Nibrask/Destination Data")]
    public class DestinationData : ScriptableObject
    {
        [Header("General Info")]
        [Tooltip("Display name of the destination (e.g., Gate A12, Restroom B)")]
        public string destinationName;

        [Tooltip("Category of this destination")]
        public DestinationType destinationType;

        [Tooltip("Icon to display in the selection menu")]
        public Sprite icon;

        [Header("Spatial Data")]
        [Tooltip("Position relative to the terminal origin anchor")]
        public Vector3 relativePosition;

        [Tooltip("Index of the nearest waypoint node in the terminal map")]
        public int nearestWaypointIndex;

        [Header("Flight Info (Gates Only)")]
        [Tooltip("Flight number associated with this gate")]
        public string flightNumber;

        [Tooltip("Boarding time for the flight")]
        public string boardingTime;

        [Tooltip("Airline name")]
        public string airlineName;

        /// <summary>
        /// Returns a formatted display string for the destination.
        /// </summary>
        public string GetDisplayName()
        {
            if (destinationType == DestinationType.Gate && !string.IsNullOrEmpty(flightNumber))
            {
                return $"{destinationName} — {flightNumber}";
            }
            return destinationName;
        }

        /// <summary>
        /// Returns the icon name fallback based on destination type.
        /// </summary>
        public string GetTypeLabel()
        {
            return destinationType switch
            {
                DestinationType.Gate => "🛫 Gate",
                DestinationType.Restroom => "🚻 Restroom",
                DestinationType.Restaurant => "🍽️ Restaurant",
                DestinationType.SecurityCheckpoint => "🔒 Security",
                DestinationType.Exit => "🚪 Exit",
                DestinationType.Information => "ℹ️ Info",
                DestinationType.Lounge => "🛋️ Lounge",
                _ => "📍 Location"
            };
        }
    }
}
