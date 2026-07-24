
namespace MackySoft.Ucli.Application.Features.Assurance;

/// <summary> Defines the finite evidence coverage values emitted for assurance claims. </summary>
[VocabularyDefinition]
internal enum AssuranceCoverage
{
    /// <summary> Evidence covers the complete claim. </summary>
    [VocabularyText("full")]
    Full = 1,

    /// <summary> Evidence covers only part of the claim. </summary>
    [VocabularyText("partial")]
    Partial = 2,

    /// <summary> No evidence coverage is available. </summary>
    [VocabularyText("none")]
    None = 3,
}
