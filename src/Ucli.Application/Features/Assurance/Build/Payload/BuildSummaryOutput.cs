using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents the BuildReport-derived build summary. </summary>
internal sealed record BuildSummaryOutput
{
    public BuildSummaryOutput (
        IpcBuildReportResult Result,
        long DurationMilliseconds,
        int ErrorCount,
        int WarningCount,
        BuildArtifactKind? ReportRef)
    {
        if (!ContractLiteralCodec.IsDefined(Result) || Result == IpcBuildReportResult.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(Result), Result, "Build summary result must be terminal.");
        }

        this.Result = Result;
        this.DurationMilliseconds = DurationMilliseconds;
        this.ErrorCount = ErrorCount;
        this.WarningCount = WarningCount;
        this.ReportRef = ReportRef;
    }

    public IpcBuildReportResult Result { get; }

    public long DurationMilliseconds { get; }

    public int ErrorCount { get; }

    public int WarningCount { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BuildArtifactKind? ReportRef { get; }
}
