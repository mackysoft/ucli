using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;

/// <summary> Defines build claim evidence kind literals. </summary>
internal enum BuildEvidenceKind
{
    /// <summary> Evidence derived from the resolved build profile. </summary>
    [UcliContractLiteral("buildProfile")]
    BuildProfile = 0,

    /// <summary> Evidence derived from resolved BuildPipeline input. </summary>
    [UcliContractLiteral("buildInput")]
    BuildInput = 1,
}
