using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Defines build output-kind literals emitted by the build run payload. </summary>
internal enum BuildProfileOutputKind
{
    /// <summary> Stores build outputs under the uCLI artifact store. </summary>
    [UcliContractLiteral("ucliArtifact")]
    UcliArtifact = 0,
}
