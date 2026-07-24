
namespace MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;

/// <summary> Defines build claim evidence kind literals. </summary>
[VocabularyDefinition]
internal enum BuildEvidenceKind
{
    /// <summary> Evidence derived from the resolved build profile. </summary>
    [VocabularyText("buildProfile")]
    BuildProfile = 0,

    /// <summary> Evidence derived from resolved BuildPipeline input. </summary>
    [VocabularyText("buildInput")]
    BuildInput = 1,
}
