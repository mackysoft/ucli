
namespace MackySoft.Ucli.Contracts;

/// <summary> Defines the finite outcomes of applying a declared condition to confirmed observations. </summary>
[VocabularyDefinition]
public enum Verdict
{
    /// <summary> All observations required by the declared condition satisfy it. </summary>
    [VocabularyText("pass")]
    Pass = 1,

    /// <summary> At least one confirmed observation does not satisfy the declared condition. </summary>
    [VocabularyText("fail")]
    Fail = 2,

    /// <summary> No failure was confirmed, but the observations required to claim success are incomplete. </summary>
    [VocabularyText("incomplete")]
    Incomplete = 3,
}
