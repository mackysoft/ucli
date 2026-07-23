
namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines <c>build.log.entry</c> level literals. </summary>
[VocabularyDefinition]
public enum BuildLogEntryLevel
{
    /// <summary> Trace log level. </summary>
    [VocabularyText("trace")]
    Trace = 1,

    /// <summary> Debug log level. </summary>
    [VocabularyText("debug")]
    Debug = 2,

    /// <summary> Informational log level. </summary>
    [VocabularyText("info")]
    Info = 3,

    /// <summary> Warning log level. </summary>
    [VocabularyText("warning")]
    Warning = 4,

    /// <summary> Error log level. </summary>
    [VocabularyText("error")]
    Error = 5,
}
