namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;

/// <summary> Defines the finite evidence kinds emitted by verify assurance. </summary>
[VocabularyDefinition]
internal enum VerifyEvidenceKind
{
    [VocabularyText("scriptCompilation")]
    ScriptCompilation = 1,

    [VocabularyText("domainReload")]
    DomainReload = 2,

    [VocabularyText("readyLifecycleSnapshot")]
    ReadyLifecycleSnapshot = 3,

    [VocabularyText("readinessDecision")]
    ReadinessDecision = 4,

    [VocabularyText("readIndexSummary")]
    ReadIndexSummary = 5,

    [VocabularyText("testSummary")]
    TestSummary = 6,

    [VocabularyText("fromResultSummary")]
    FromResultSummary = 7,

    [VocabularyText("compileLifecycleSnapshot")]
    CompileLifecycleSnapshot = 8,

    [VocabularyText("fromResultMissing")]
    FromResultMissing = 9,
}
