
namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines the finite verdict values emitted by assurance commands. </summary>
[VocabularyDefinition]
public enum AssuranceVerdict
{
    /// <summary> All required claims passed with full coverage. </summary>
    [VocabularyText("pass")]
    Pass = 1,

    /// <summary> A blocking risk or required claim failed. </summary>
    [VocabularyText("fail")]
    Fail = 2,

    /// <summary> A required claim did not reach complete evidence. </summary>
    [VocabularyText("incomplete")]
    Incomplete = 3,
}
