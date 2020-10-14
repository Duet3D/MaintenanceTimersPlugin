using System.Collections.Generic;

namespace MaintenanceTimersPlugin
{
    /// <summary>
    /// Class representing a maintenance timer
    /// </summary>
    public class MaintenanceTimer
    {
        /// <summary>
        /// Initial value of this timer (in mins).
        /// If it is 0, the timer counts up
        /// </summary>
        public int InitialValue { get; set; }

        /// <summary>
        /// Current value of this timer
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// List of conditions to be met in order for this timer to be updated
        /// </summary>
        public List<string> Conditions { get; set; } = new List<string>();

        /// <summary>
        /// Threshold value (in mins) or -1 
        /// </summary>
        public int ThresholdValue { get; set; }

        /// <summary>
        /// Action to perform (G/M/T-code) when the timer reaches the threshold value
        /// </summary>
        public string Action { get; set; }
    }
}
