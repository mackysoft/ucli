namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Identifies the evidence category attached to a compile assurance claim. </summary>
[VocabularyDefinition]
public enum CompileEvidenceKind
{
    [VocabularyText("scriptCompilation")]
    ScriptCompilation = 1,

    [VocabularyText("domainReload")]
    DomainReload = 2,

    [VocabularyText("lifecycleSnapshot")]
    LifecycleSnapshot = 3,
}
