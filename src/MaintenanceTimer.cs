using System.Collections.Generic;

namespace MaintenanceTimersPlugin;

/// <summary>
/// Class representing a maintenance timer
/// </summary>
public class MaintenanceTimer
{
    /// <summary>
    /// Short name of the timer used to identify it
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// User-defined title for this timer
    /// </summary>
    public string Title { get; set; }

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
    /// List of conditions (expressions) to be met in order for this timer to be updated
    /// </summary>
    public List<string> Conditions { get; set; } = new List<string>();

    /// <summary>
    /// Threshold value (in mins) or -1 if not applicable
    /// </summary>
    public int ThresholdValue { get; set; }

    /// <summary>
    /// Action to perform (G/M/T-code) when the timer reaches the threshold value
    /// </summary>
    public string Action { get; set; }

    /// <summary>
    /// Can this timer be reset.
    /// </summary>
    public bool CanReset { get; set; } = true;
}
