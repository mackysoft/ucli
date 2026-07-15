using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines normalized build log completion reason literals. </summary>
public enum IpcBuildLogCompletionReason
{
    /// <summary> BuildPipeline completed successfully. </summary>
    [UcliContractLiteral("completed")]
    Completed = 1,

    /// <summary> BuildPipeline completed with a failure result. </summary>
    [UcliContractLiteral("failed")]
    Failed = 2,

    /// <summary> BuildPipeline completed with a canceled result. </summary>
    [UcliContractLiteral("canceled")]
    Canceled = 3,
}
