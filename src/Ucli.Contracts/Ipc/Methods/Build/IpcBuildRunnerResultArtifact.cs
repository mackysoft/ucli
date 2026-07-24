using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a normalized build runner terminal result. </summary>
public sealed record IpcBuildRunnerResultArtifact
{
    /// <summary> Initializes one normalized build runner terminal result. </summary>
    /// <param name="Source"> The defined evidence source. </param>
    /// <param name="Status"> A succeeded, failed, or canceled terminal status. </param>
    /// <param name="DurationMilliseconds"> The non-negative runner duration in milliseconds. </param>
    /// <param name="ErrorCount"> The non-negative error count. </param>
    /// <param name="WarningCount"> The non-negative warning count. </param>
    /// <param name="Diagnostics"> The diagnostics, containing no <see langword="null" /> items. </param>
    /// <param name="Outputs"> The normalized uCLI runner-output-relative paths, containing no <see langword="null" /> items. </param>
    /// <param name="BuildReport"> Optional BuildReport evidence declared by a uCLI runner. </param>
    /// <exception cref="ArgumentNullException"> Thrown when a required collection is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a required collection contains a <see langword="null" /> item, when a successful uCLI runner result declares no outputs,
    /// or when a BuildPipeline result declares runner-output evidence.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when a literal is undefined, the status is not terminal, or a summary value is negative. </exception>
    [JsonConstructor]
    public IpcBuildRunnerResultArtifact (
        IpcBuildRunnerResultSource Source,
        IpcBuildReportResult Status,
        long DurationMilliseconds,
        int ErrorCount,
        int WarningCount,
        IReadOnlyList<IpcBuildRunnerDiagnostic> Diagnostics,
        IReadOnlyList<BuildRunnerOutputPath> Outputs,
        IpcBuildRunnerResultBuildReport? BuildReport)
    {
        if (!TextVocabulary.IsDefined(Source))
        {
            throw new ArgumentOutOfRangeException(nameof(Source), Source, "Build runner result source must be specified.");
        }

        if (!TextVocabulary.IsDefined(Status) || Status == IpcBuildReportResult.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(Status), Status, "Build runner status must be terminal.");
        }

        if (DurationMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DurationMilliseconds),
                DurationMilliseconds,
                "Build runner duration must not be negative.");
        }

        var errorCount = ContractArgumentGuard.RequireNonNegative(ErrorCount, nameof(ErrorCount));
        var warningCount = ContractArgumentGuard.RequireNonNegative(WarningCount, nameof(WarningCount));
        var diagnostics = ContractArgumentGuard.RequireItems(Diagnostics, nameof(Diagnostics));
        var outputs = ContractArgumentGuard.RequireItems(Outputs, nameof(Outputs));
        if (Source == IpcBuildRunnerResultSource.BuildPipelineBuildReport && outputs.Count != 0)
        {
            throw new ArgumentException("A BuildPipeline result must not declare runner output paths.", nameof(Outputs));
        }

        if (Source == IpcBuildRunnerResultSource.BuildPipelineBuildReport && BuildReport != null)
        {
            throw new ArgumentException("A BuildPipeline result must not declare runner-relative BuildReport evidence.", nameof(BuildReport));
        }

        if (Source == IpcBuildRunnerResultSource.UcliBuildRunnerResult
            && Status == IpcBuildReportResult.Succeeded
            && outputs.Count == 0)
        {
            throw new ArgumentException("A successful uCLI build runner result must declare at least one output.", nameof(Outputs));
        }

        this.Source = Source;
        this.Status = Status;
        this.DurationMilliseconds = DurationMilliseconds;
        this.ErrorCount = errorCount;
        this.WarningCount = warningCount;
        this.Diagnostics = diagnostics;
        this.Outputs = outputs;
        this.BuildReport = BuildReport;
    }

    public IpcBuildRunnerResultSource Source { get; }

    public IpcBuildReportResult Status { get; }

    public long DurationMilliseconds { get; }

    public int ErrorCount { get; }

    public int WarningCount { get; }

    public IReadOnlyList<IpcBuildRunnerDiagnostic> Diagnostics { get; }

    /// <summary> Gets the uCLI runner-declared output paths relative to the runner output directory. </summary>
    public IReadOnlyList<BuildRunnerOutputPath> Outputs { get; }

    /// <summary> Gets optional BuildReport evidence source declared by a uCLI runner. </summary>
    public IpcBuildRunnerResultBuildReport? BuildReport { get; }
}
