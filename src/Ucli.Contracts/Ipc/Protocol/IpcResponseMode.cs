
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines IPC response framing mode literals. </summary>
[VocabularyDefinition]
public enum IpcResponseMode
{
    /// <summary> Single terminal response mode. </summary>
    [VocabularyText("single")]
    Single = 0,

    /// <summary> Progress-frame stream followed by one terminal response mode. </summary>
    [VocabularyText("stream")]
    Stream = 1,
}
