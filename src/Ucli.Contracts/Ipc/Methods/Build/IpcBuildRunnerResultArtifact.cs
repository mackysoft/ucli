namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a normalized build runner terminal result. </summary>
/// <param name="Source"> The runner result source literal. </param>
/// <param name="Status"> The terminal status literal. </param>
/// <param name="DurationMilliseconds"> The runner invocation duration in milliseconds. </param>
/// <param name="ErrorCount"> The runner-observed error count. </param>
/// <param name="WarningCount"> The runner-observed warning count. </param>
/// <param name="Diagnostics"> The runner diagnostics. </param>
public sealed record IpcBuildRunnerResultArtifact (
    string Source,
    string Status,
    long DurationMilliseconds,
    int ErrorCount,
    int WarningCount,
    IReadOnlyList<IpcBuildRunnerDiagnostic> Diagnostics)
{
    /// <summary> Gets the runner-declared output paths relative to the runner output directory. </summary>
    public IReadOnlyList<string> Outputs { get; init; } = Array.Empty<string>();

    /// <summary> Gets optional BuildReport evidence source declared by the runner. </summary>
    public IpcBuildRunnerResultBuildReport? BuildReport { get; init; }
}
