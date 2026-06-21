namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines the closed <c>build.run</c> stream event set. </summary>
public static class BuildRunProgressEventNames
{
    /// <summary> Gets the event emitted after build run identity and profile digest are established. </summary>
    public const string Started = "build.run.started";

    /// <summary> Gets the event emitted after Unity readiness is confirmed. </summary>
    public const string ReadinessCompleted = "build.readiness.completed";

    /// <summary> Gets the event emitted after the build runner is resolved. </summary>
    public const string RunnerResolved = "build.runner.resolved";

    /// <summary> Gets the event emitted when build runner invocation starts. </summary>
    public const string RunnerStarted = "build.runner.started";

    /// <summary> Gets the event emitted for one observed build log entry. </summary>
    public const string LogEntry = "build.log.entry";

    /// <summary> Gets the event emitted when the build runner reaches a terminal result. </summary>
    public const string RunnerCompleted = "build.runner.completed";

    /// <summary> Gets the event emitted after runner result normalization completes. </summary>
    public const string RunnerResultCompleted = "build.runnerResult.completed";

    /// <summary> Gets the event emitted after build artifacts are accounted. </summary>
    public const string ArtifactsCompleted = "build.artifacts.completed";

    /// <summary> Gets the event emitted after the final build payload has been built. </summary>
    public const string Completed = "build.run.completed";

    /// <summary> Gets the event emitted for build progress diagnostics. </summary>
    public const string Diagnostic = "build.diagnostic";
}
