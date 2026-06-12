using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Defines stable semantic kinds used by build artifact references. </summary>
internal enum BuildArtifactKind
{
    /// <summary> Represents the build metadata artifact. </summary>
    [UcliContractLiteral("buildMetadata")]
    BuildMetadata = 0,

    /// <summary> Represents the Unity BuildReport artifact. </summary>
    [UcliContractLiteral("buildReport")]
    BuildReport = 1,

    /// <summary> Represents the player output manifest artifact. </summary>
    [UcliContractLiteral("buildOutputManifest")]
    BuildOutputManifest = 2,

    /// <summary> Represents the build log artifact. </summary>
    [UcliContractLiteral("buildLog")]
    BuildLog = 3,
}
