namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines the closed <c>build.run</c> progress phase set. </summary>
public static class BuildRunProgressPhaseNames
{
    /// <summary> Gets the phase emitted after build run identity and profile digest are established. </summary>
    public const string Started = "started";

    /// <summary> Gets the phase emitted after Unity readiness is confirmed. </summary>
    public const string Readiness = "readiness";

    /// <summary> Gets the phase emitted after the build runner is resolved. </summary>
    public const string RunnerResolution = "runnerResolution";

    /// <summary> Gets the phase emitted while the build runner is invoked. </summary>
    public const string RunnerInvocation = "runnerInvocation";

    /// <summary> Gets the phase emitted after the runner terminal result is observed or normalized. </summary>
    public const string RunnerResult = "runnerResult";

    /// <summary> Gets the phase emitted after build artifacts are accounted. </summary>
    public const string ArtifactAccounting = "artifactAccounting";

    /// <summary> Gets the phase emitted after the final build payload has been built. </summary>
    public const string Completed = "completed";

    /// <summary> Gets the complete closed progress phase set. </summary>
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[]
    {
        Started,
        Readiness,
        RunnerResolution,
        RunnerInvocation,
        RunnerResult,
        ArtifactAccounting,
        Completed,
    });
}
