
namespace MackySoft.Ucli.Contracts;

/// <summary> Identifies the severity of a structured uCLI diagnostic. </summary>
[VocabularyDefinition]
public enum UcliDiagnosticSeverity
{
    /// <summary> Indicates an informational diagnostic. </summary>
    [VocabularyText("info")]
    Info = 1,

    /// <summary> Indicates a warning diagnostic. </summary>
    [VocabularyText("warning")]
    Warning = 2,

    /// <summary> Indicates an error diagnostic. </summary>
    [VocabularyText("error")]
    Error = 3,
}
