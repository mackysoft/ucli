
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies how an <c>execute</c> diagnostic affects coverage. </summary>
[VocabularyDefinition]
public enum IpcExecuteDiagnosticCoverageImpact
{
    /// <summary> Indicates that the diagnostic has no coverage impact. </summary>
    [VocabularyText("none")]
    None = 1,

    /// <summary> Indicates that the operation covered only part of the requested target set. </summary>
    [VocabularyText("partial")]
    Partial = 2,

    /// <summary> Indicates that coverage could not be determined. </summary>
    [VocabularyText("indeterminate")]
    Indeterminate = 3,
}
