namespace MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;

/// <summary> Defines the finite evidence kinds emitted by build assurance. </summary>
[VocabularyDefinition]
internal enum BuildEvidenceKind
{
    [VocabularyText("buildProfile")]
    BuildProfile = 1,

    [VocabularyText("buildInput")]
    BuildInput = 2,

    [VocabularyText("readyLifecycleSnapshot")]
    ReadyLifecycleSnapshot = 3,

    [VocabularyText("buildRunner")]
    BuildRunner = 4,

    [VocabularyText("buildReportSummary")]
    BuildReportSummary = 5,

    [VocabularyText("buildLogSummary")]
    BuildLogSummary = 6,

    [VocabularyText("buildOutputManifest")]
    BuildOutputManifest = 7,

    [VocabularyText("generationSnapshot")]
    GenerationSnapshot = 8,

    [VocabularyText("projectMutationAudit")]
    ProjectMutationAudit = 9,

    [VocabularyText("buildRunnerResult")]
    BuildRunnerResult = 10,

    [VocabularyText("buildSummary")]
    BuildSummary = 11,

    [VocabularyText("buildOutputAccounting")]
    BuildOutputAccounting = 12,
}
