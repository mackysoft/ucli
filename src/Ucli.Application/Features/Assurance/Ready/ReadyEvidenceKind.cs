namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Defines the finite evidence kinds emitted by readiness assurance. </summary>
[VocabularyDefinition]
internal enum ReadyEvidenceKind
{
    [VocabularyText("lifecycleSnapshot")]
    LifecycleSnapshot = 1,

    [VocabularyText("readinessDecision")]
    ReadinessDecision = 2,

    [VocabularyText("readIndexSummary")]
    ReadIndexSummary = 3,
}
