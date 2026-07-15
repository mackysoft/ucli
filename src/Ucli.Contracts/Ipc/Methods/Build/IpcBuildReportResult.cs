using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines normalized Unity BuildReport result literals. </summary>
public enum IpcBuildReportResult
{
    /// <summary> The BuildReport indicated success. </summary>
    [UcliContractLiteral("succeeded")]
    Succeeded = 1,

    /// <summary> The BuildReport indicated failure. </summary>
    [UcliContractLiteral("failed")]
    Failed = 2,

    /// <summary> The BuildReport indicated cancellation. </summary>
    [UcliContractLiteral("canceled")]
    Canceled = 3,

    /// <summary> The BuildReport result could not be classified. </summary>
    [UcliContractLiteral("unknown")]
    Unknown = 4,
}
