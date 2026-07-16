using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance.Build;

/// <summary> Defines the stable references for build assurance artifacts and reports. </summary>
public enum BuildArtifactKind
{
    /// <summary> The <c>build.json</c> artifact. </summary>
    [UcliContractLiteral("build")]
    Build = 1,

    /// <summary> The normalized Unity BuildReport artifact. </summary>
    [UcliContractLiteral("buildReport")]
    BuildReport = 2,

    /// <summary> The player output manifest artifact. </summary>
    [UcliContractLiteral("buildOutputManifest")]
    BuildOutputManifest = 3,

    /// <summary> The build log artifact. </summary>
    [UcliContractLiteral("buildLog")]
    BuildLog = 4,
}
