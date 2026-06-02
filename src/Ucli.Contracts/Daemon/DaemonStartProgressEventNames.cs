namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Defines the closed host-visible <c>daemon.start</c> progress stream event set. </summary>
public static class DaemonStartProgressEventNames
{
    /// <summary> Gets the event emitted after daemon-start context resolution completes. </summary>
    public const string Started = "daemon.start.started";

    /// <summary> Gets the event emitted before uCLI Unity plugin verification begins. </summary>
    public const string PluginVerificationStarted = "daemon.start.pluginVerification.started";

    /// <summary> Gets the event emitted after uCLI Unity plugin verification completes. </summary>
    public const string PluginVerificationCompleted = "daemon.start.pluginVerification.completed";

    /// <summary> Gets the event emitted before supervisor bootstrap begins. </summary>
    public const string SupervisorBootstrapStarted = "daemon.start.supervisorBootstrap.started";

    /// <summary> Gets the event emitted after supervisor bootstrap completes. </summary>
    public const string SupervisorBootstrapCompleted = "daemon.start.supervisorBootstrap.completed";

    /// <summary> Gets the event emitted before the supervisor ensureRunning request begins. </summary>
    public const string EnsureRunningStarted = "daemon.start.ensureRunning.started";

    /// <summary> Gets the event emitted after the supervisor ensureRunning request completes. </summary>
    public const string EnsureRunningCompleted = "daemon.start.ensureRunning.completed";

    /// <summary> Gets the event emitted after daemon-start final output is determined. </summary>
    public const string Completed = "daemon.start.completed";
}
