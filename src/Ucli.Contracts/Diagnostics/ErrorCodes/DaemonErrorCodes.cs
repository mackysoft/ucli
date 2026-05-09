namespace MackySoft.Ucli.Contracts;

/// <summary> Defines daemon lifecycle machine-readable error code values. </summary>
public static class DaemonErrorCodes
{
    /// <summary> Gets the error code emitted when a requested Editor mode conflicts with an existing daemon session. </summary>
    public static readonly UcliErrorCode DaemonEditorModeMismatch = new("DAEMON_EDITOR_MODE_MISMATCH");

    /// <summary> Gets the error code emitted when daemon startup is blocked by a known Unity Editor startup condition. </summary>
    public static readonly UcliErrorCode DaemonStartupBlocked = new("DAEMON_STARTUP_BLOCKED");
}
