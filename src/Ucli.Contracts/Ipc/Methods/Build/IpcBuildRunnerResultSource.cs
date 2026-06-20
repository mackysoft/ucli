using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines build runner terminal result source literals. </summary>
public enum IpcBuildRunnerResultSource
{
    /// <summary> The terminal result was normalized from Unity BuildPipeline BuildReport. </summary>
    [UcliContractLiteral("buildPipelineBuildReport")]
    BuildPipelineBuildReport = 0,

    /// <summary> The terminal result was returned by <c>UcliBuildRunnerResult</c>. </summary>
    [UcliContractLiteral("ucliBuildRunnerResult")]
    UcliBuildRunnerResult = 1,
}
