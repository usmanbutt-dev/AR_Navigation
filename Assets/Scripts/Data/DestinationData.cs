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
                DestinationType.Gate => "Gate",
                DestinationType.Restroom => "Restroom",
                DestinationType.Restaurant => "Restaurant",
                DestinationType.SecurityCheckpoint => "Security",
                DestinationType.Exit => "Exit",
                DestinationType.Information => "Info",
                DestinationType.Lounge => "Lounge",
                _ => "Location"
            };
        }

        /// <summary>
        /// Calculates the time remaining until boarding based on the boardingTime field
        /// and the device's current time. Returns a human-readable string like "1h 23m",
        /// "12 min", or "Boarding now". Returns null if not a gate or no boarding time set.
        /// </summary>
        public string GetTimeUntilBoarding()
        {
            if (destinationType != DestinationType.Gate || string.IsNullOrEmpty(boardingTime))
                return null;

            // Parse "HH:mm" format
            string[] parts = boardingTime.Split(':');
            if (parts.Length != 2) return null;

            if (!int.TryParse(parts[0], out int hours) || !int.TryParse(parts[1], out int minutes))
                return null;

            var now = System.DateTime.Now;
            var boardingToday = new System.DateTime(now.Year, now.Month, now.Day, hours, minutes, 0);

            // If boarding time has already passed today, assume it's tomorrow
            if (boardingToday < now)
                boardingToday = boardingToday.AddDays(1);

            var remaining = boardingToday - now;

            if (remaining.TotalMinutes <= 0)
                return "Boarding now";
            else if (remaining.TotalMinutes < 60)
                return $"{(int)remaining.TotalMinutes} min";
            else
                return $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}m";
        }
    }
}
