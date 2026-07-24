
namespace MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

/// <summary> Identifies the semantic role of a code catalog entry. </summary>
[VocabularyDefinition]
internal enum CodeCatalogKind
{
    /// <summary> A command failure code emitted at <c>errors[].code</c>. </summary>
    [VocabularyText("error")]
    Error = 1,

    /// <summary> A diagnostic evidence code. </summary>
    [VocabularyText("diagnostic")]
    Diagnostic = 2,

    /// <summary> A machine-readable reason code. </summary>
    [VocabularyText("reason")]
    Reason = 3,

    /// <summary> An assurance claim code. </summary>
    [VocabularyText("claim")]
    Claim = 4,

    /// <summary> A residual risk code. </summary>
    [VocabularyText("risk")]
    Risk = 5,

    /// <summary> A code that is not registered in the local catalog. </summary>
    [VocabularyText("unknown")]
    Unknown = 6,
}
