namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies the level of a log entry emitted by evaluated C# code. </summary>
[VocabularyDefinition]
public enum CsEvalLogLevel
{
    /// <summary> Indicates a diagnostic log entry. </summary>
    [VocabularyText("debug")]
    Debug = 1,

    /// <summary> Indicates an informational log entry. </summary>
    [VocabularyText("info")]
    Info = 2,

    /// <summary> Indicates a warning log entry. </summary>
    [VocabularyText("warning")]
    Warning = 3,

    /// <summary> Indicates an error log entry. </summary>
    [VocabularyText("error")]
    Error = 4,
}
