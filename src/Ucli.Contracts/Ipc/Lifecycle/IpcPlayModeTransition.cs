namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines typed Play Mode subsystem transitions used by runtime lifecycle logic. </summary>
public enum IpcPlayModeTransition
{
    /// <summary> No Play Mode transition is active. </summary>
    None,

    /// <summary> The Editor is entering Play Mode. </summary>
    Entering,

    /// <summary> The Editor is exiting Play Mode. </summary>
    Exiting,
}
