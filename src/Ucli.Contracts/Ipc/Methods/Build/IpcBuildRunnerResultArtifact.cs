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
    IReadOnlyList<IpcBuildRunnerDiagnostic> Diagnostics);
