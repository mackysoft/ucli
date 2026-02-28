namespace MackySoft.Ucli.Execution;

/// <summary> Defines machine-readable error codes returned from mode decision contract checks. </summary>
internal static class UnityExecutionModeDecisionErrorCodes
{
    /// <summary> Gets the error code emitted when daemon mode is requested but daemon is not reachable. </summary>
    public const string DaemonNotRunning = "DAEMON_NOT_RUNNING";

    /// <summary> Gets the error code emitted when oneshot mode is requested while daemon is reachable. </summary>
    public const string DaemonRunningOneshotForbidden = "DAEMON_RUNNING_ONESHOT_FORBIDDEN";
}