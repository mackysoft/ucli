using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents the public projection of a build runner terminal result. </summary>
internal sealed record BuildRunnerResultOutput
{
    public BuildRunnerResultOutput (
        IpcBuildRunnerResultSource Source,
        IpcBuildReportResult Status)
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
    }

    public IpcBuildRunnerResultSource Source { get; }

    public IpcBuildReportResult Status { get; }
}
