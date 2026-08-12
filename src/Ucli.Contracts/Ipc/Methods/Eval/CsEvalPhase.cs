namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies the dedicated C# evaluation protocol stage. </summary>
[VocabularyDefinition]
public enum CsEvalPhase
{
    [VocabularyText("plan")]
    Plan = 1,

    [VocabularyText("call")]
    Call = 2,
}
