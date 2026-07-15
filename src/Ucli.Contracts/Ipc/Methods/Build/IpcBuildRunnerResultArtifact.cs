using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a normalized build runner terminal result. </summary>
public sealed record IpcBuildRunnerResultArtifact
{
    /// <summary> Initializes one normalized build runner terminal result. </summary>
    [JsonConstructor]
    public IpcBuildRunnerResultArtifact (
        IpcBuildRunnerResultSource Source,
        IpcBuildReportResult Status,
        long DurationMilliseconds,
        int ErrorCount,
        int WarningCount,
        IReadOnlyList<IpcBuildRunnerDiagnostic> Diagnostics,
        IReadOnlyList<string> Outputs,
        IpcBuildRunnerResultBuildReport? BuildReport)
    {
        if (!ContractLiteralCodec.IsDefined(Source))
        {
            throw new ArgumentOutOfRangeException(nameof(Source), Source, "Build runner result source must be specified.");
        }

        if (!ContractLiteralCodec.IsDefined(Status) || Status == IpcBuildReportResult.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(Status), Status, "Build runner status must be terminal.");
        }

        this.Source = Source;
        this.Status = Status;
        this.DurationMilliseconds = DurationMilliseconds;
        this.ErrorCount = ErrorCount;
        this.WarningCount = WarningCount;
        this.Diagnostics = ContractArgumentGuard.RequireItems(Diagnostics, nameof(Diagnostics));
        this.Outputs = ContractArgumentGuard.RequireItems(Outputs, nameof(Outputs));
        this.BuildReport = BuildReport;
    }

    public IpcBuildRunnerResultSource Source { get; }

    public IpcBuildReportResult Status { get; }

    public long DurationMilliseconds { get; }

    public int ErrorCount { get; }

    public int WarningCount { get; }

    public IReadOnlyList<IpcBuildRunnerDiagnostic> Diagnostics { get; }

    /// <summary> Gets the runner-declared output paths relative to the runner output directory. </summary>
    public IReadOnlyList<string> Outputs { get; }

    /// <summary> Gets optional BuildReport evidence source declared by the runner. </summary>
    public IpcBuildRunnerResultBuildReport? BuildReport { get; }
}
