
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines Unity log stack-trace inclusion mode literals. </summary>
[VocabularyDefinition]
public enum IpcUnityLogStackTraceMode
{
    /// <summary> Suppresses stack traces. </summary>
    [VocabularyText("none")]
    None = 1,

    /// <summary> Includes stack traces for error events only. </summary>
    [VocabularyText("error")]
    Error = 2,

    /// <summary> Includes stack traces for all events. </summary>
    [VocabularyText("all")]
    All = 3,
}
