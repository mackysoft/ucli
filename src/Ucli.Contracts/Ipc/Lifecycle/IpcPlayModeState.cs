namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines typed Play Mode subsystem states used by runtime lifecycle logic. </summary>
public enum IpcPlayModeState
{
    /// <summary> Play Mode is inactive and no transition is pending. </summary>
    Stopped,

    /// <summary> The Editor is entering Play Mode. </summary>
    Entering,

    /// <summary> Play Mode is active. </summary>
    Playing,

    /// <summary> The Editor is exiting Play Mode. </summary>
    Exiting,

    /// <summary> Play Mode state could not be classified. </summary>
    Unknown,
}
