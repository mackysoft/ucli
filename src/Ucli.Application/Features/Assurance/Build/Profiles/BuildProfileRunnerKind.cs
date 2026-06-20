using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Defines build profile runner-kind literals. </summary>
internal enum BuildProfileRunnerKind
{
    /// <summary> Invokes Unity <c>BuildPipeline</c> through the uCLI Unity runtime. </summary>
    [UcliContractLiteral("buildPipeline")]
    BuildPipeline = 0,
}
