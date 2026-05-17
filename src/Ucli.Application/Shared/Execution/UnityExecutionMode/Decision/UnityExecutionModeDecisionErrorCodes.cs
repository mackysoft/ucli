namespace MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

/// <summary> Defines machine-readable error codes returned from mode decision contract checks. </summary>
internal static class UnityExecutionModeDecisionErrorCodes
{
    /// <summary> Gets the error code emitted when daemon mode is requested but daemon is not reachable. </summary>
    public static readonly UcliCodeValue DaemonNotRunning = new UcliCodeValue("DAEMON_NOT_RUNNING");

    /// <summary> Gets the error code emitted when oneshot mode is requested while daemon is reachable. </summary>
    public static readonly UcliCodeValue DaemonRunningOneshotForbidden = new UcliCodeValue("DAEMON_RUNNING_ONESHOT_FORBIDDEN");
}
