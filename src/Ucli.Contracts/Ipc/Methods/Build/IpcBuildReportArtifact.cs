using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the normalized Unity BuildReport artifact. </summary>
public sealed record IpcBuildReportArtifact
{
    /// <summary> Initializes one normalized Unity BuildReport artifact. </summary>
    [JsonConstructor]
    public IpcBuildReportArtifact (
        int SchemaVersion,
        IpcBuildReportResult Result,
        string UnityBuildTarget,
        string OutputPath,
        long DurationMilliseconds,
        long TotalSizeBytes,
        int ErrorCount,
        int WarningCount,
        IReadOnlyList<IpcBuildReportStep> Steps,
        IReadOnlyList<IpcBuildReportMessage> Messages)
    {
        if (!ContractLiteralCodec.IsDefined(Result))
        {
            throw new ArgumentOutOfRangeException(nameof(Result), Result, "Build report result must be specified.");
        }

        this.SchemaVersion = SchemaVersion;
        this.Result = Result;
        this.UnityBuildTarget = UnityBuildTarget;
        this.OutputPath = OutputPath;
        this.DurationMilliseconds = DurationMilliseconds;
        this.TotalSizeBytes = TotalSizeBytes;
        this.ErrorCount = ErrorCount;
        this.WarningCount = WarningCount;
        this.Steps = ContractArgumentGuard.RequireItems(Steps, nameof(Steps));
        this.Messages = ContractArgumentGuard.RequireItems(Messages, nameof(Messages));
    }

    public int SchemaVersion { get; }

    public IpcBuildReportResult Result { get; }

    public string UnityBuildTarget { get; }

    public string OutputPath { get; }

    public long DurationMilliseconds { get; }

    public long TotalSizeBytes { get; }

    public int ErrorCount { get; }

    public int WarningCount { get; }

    public IReadOnlyList<IpcBuildReportStep> Steps { get; }

    public IReadOnlyList<IpcBuildReportMessage> Messages { get; }
}
