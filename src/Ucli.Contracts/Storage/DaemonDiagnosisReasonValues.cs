namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Defines normalized daemon diagnosis reason values used across CLI and Unity batchmode. </summary>
public static class DaemonDiagnosisReasonValues
{
    /// <summary> Gets the reason value used when daemon shutdown was requested normally. </summary>
    public const string ShutdownRequested = "shutdownRequested";

    /// <summary> Gets the reason value used when daemon startup failed before running state was established. </summary>
    public const string StartupFailed = "startupFailed";

    /// <summary> Gets the reason value used when the IPC server loop terminated unexpectedly. </summary>
    public const string ListenerTerminated = "listenerTerminated";

    /// <summary> Gets the reason value used when daemon bootstrap failed with an unhandled exception. </summary>
    public const string UnhandledException = "unhandledException";

    /// <summary> Gets the reason value used when CLI infers external process termination without persisted diagnosis. </summary>
    public const string ExternalTerminationSuspected = "externalTerminationSuspected";
}
