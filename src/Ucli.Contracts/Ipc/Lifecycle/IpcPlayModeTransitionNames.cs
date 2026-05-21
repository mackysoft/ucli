namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines Play Mode subsystem transition literals. </summary>
public static class IpcPlayModeTransitionNames
{
    /// <summary> Gets the transition used when no Play Mode transition is active. </summary>
    public const string None = "none";

    /// <summary> Gets the transition used while the Editor is entering Play Mode. </summary>
    public const string Entering = "entering";

    /// <summary> Gets the transition used while the Editor is exiting Play Mode. </summary>
    public const string Exiting = "exiting";
}
