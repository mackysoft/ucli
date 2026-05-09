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

    /// <summary> Gets the reason value used when daemon reaches readiness but fails the stability window. </summary>
    public const string StartupUnstable = "startupUnstable";

    /// <summary> Gets the reason value used when supervisor observes unexpected daemon process termination. </summary>
    public const string UnexpectedExit = "unexpectedExit";

    /// <summary> Gets the reason value used when CLI infers external process termination without persisted diagnosis. </summary>
    public const string ExternalTerminationSuspected = "externalTerminationSuspected";

    /// <summary> Gets the reason value used when a detected GUI Editor process does not register an endpoint before timeout. </summary>
    public const string GuiEndpointNotRegistered = "guiEndpointNotRegistered";
}
