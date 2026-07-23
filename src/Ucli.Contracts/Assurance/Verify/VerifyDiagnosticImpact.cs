
namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Identifies the strongest diagnostic impact projected into verify post-read evidence. </summary>
[VocabularyDefinition]
public enum VerifyDiagnosticImpact
{
    /// <summary> Diagnostics do not affect the claim result. </summary>
    [VocabularyText("none")]
    None = 1,

    /// <summary> Diagnostics reduce claim coverage to a partial result. </summary>
    [VocabularyText("partial")]
    Partial = 2,

    /// <summary> Diagnostics prevent coverage from being determined. </summary>
    [VocabularyText("indeterminate")]
    Indeterminate = 3,

    /// <summary> An error diagnostic fails the claim. </summary>
    [VocabularyText("error")]
    Error = 4,
}
