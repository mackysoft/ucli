namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines IPC streaming frame kinds. </summary>
public static class IpcStreamFrameKinds
{
    /// <summary> Gets the progress-frame kind. </summary>
    public const string Progress = "progress";

    /// <summary> Gets the terminal-frame kind. </summary>
    public const string Terminal = "terminal";
}
