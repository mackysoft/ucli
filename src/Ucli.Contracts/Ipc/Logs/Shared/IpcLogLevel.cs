
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines daemon and Unity log level literals. </summary>
[VocabularyDefinition]
public enum IpcLogLevel
{
    /// <summary> Error log level. </summary>
    [VocabularyText("error")]
    Error = 1,

    /// <summary> Warning log level. </summary>
    [VocabularyText("warning")]
    Warning = 2,

    /// <summary> Informational log level. </summary>
    [VocabularyText("info")]
    Info = 3,
}
