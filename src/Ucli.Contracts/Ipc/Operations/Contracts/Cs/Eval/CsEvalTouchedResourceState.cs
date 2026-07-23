
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies the completeness of C# eval touched-resource declarations. </summary>
[VocabularyDefinition]
public enum CsEvalTouchedResourceState
{
    /// <summary> Indicates that the evaluated code did not provide a complete declaration. </summary>
    [VocabularyText("unknown")]
    Unknown = 1,

    /// <summary> Indicates that the evaluated code explicitly declared no touched resources. </summary>
    [VocabularyText("none")]
    None = 2,

    /// <summary> Indicates that the evaluated code declared one or more touched resources. </summary>
    [VocabularyText("declared")]
    Declared = 3,
}
