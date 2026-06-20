using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;

/// <summary> Defines build output kind literals emitted by build payloads. </summary>
internal enum BuildOutputKind
{
    /// <summary> Represents a uCLI-managed build artifact output. </summary>
    [UcliContractLiteral("ucliArtifact")]
    UcliArtifact = 0,
}
