using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Defines build profile output-kind literals. </summary>
internal enum BuildProfileOutputKind
{
    /// <summary> Writes build outputs under the uCLI artifact store. </summary>
    [UcliContractLiteral("ucliArtifact")]
    UcliArtifact = 0,
}
