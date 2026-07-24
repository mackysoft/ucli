
namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Identifies the evidence category attached to a compile assurance claim. </summary>
[VocabularyDefinition]
public enum CompileEvidenceKind
{
    /// <summary> Script-compilation evidence. </summary>
    [VocabularyText("scriptCompilation")]
    ScriptCompilation = 1,

    /// <summary> Domain-reload evidence. </summary>
    [VocabularyText("domainReload")]
    DomainReload = 2,

    /// <summary> Unity lifecycle snapshot evidence. </summary>
    [VocabularyText("lifecycleSnapshot")]
    LifecycleSnapshot = 3,
}
