namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines Play Mode subsystem state literals. </summary>
public static class IpcPlayModeStateNames
{
    /// <summary> Gets the state used when Play Mode is stopped. </summary>
    public const string Stopped = "stopped";

    /// <summary> Gets the state used while the Editor is entering Play Mode. </summary>
    public const string Entering = "entering";

    /// <summary> Gets the state used when Play Mode is active. </summary>
    public const string Playing = "playing";

    /// <summary> Gets the state used while the Editor is exiting Play Mode. </summary>
    public const string Exiting = "exiting";

    /// <summary> Gets the state used when Play Mode state cannot be classified. </summary>
    public const string Unknown = "unknown";
}
