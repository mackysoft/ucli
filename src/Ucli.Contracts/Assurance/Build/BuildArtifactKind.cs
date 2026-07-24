
namespace MackySoft.Ucli.Contracts.Assurance.Build;

/// <summary> Defines the stable references for build assurance artifacts and reports. </summary>
[VocabularyDefinition]
public enum BuildArtifactKind
{
    /// <summary> The <c>build.json</c> artifact. </summary>
    [VocabularyText("build")]
    Build = 1,

    /// <summary> The normalized Unity BuildReport artifact. </summary>
    [VocabularyText("buildReport")]
    BuildReport = 2,

    /// <summary> The player output manifest artifact. </summary>
    [VocabularyText("buildOutputManifest")]
    BuildOutputManifest = 3,

    /// <summary> The build log artifact. </summary>
    [VocabularyText("buildLog")]
    BuildLog = 4,
}
