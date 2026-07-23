
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines log query target literals. </summary>
[VocabularyDefinition]
public enum IpcLogQueryTarget
{
    /// <summary> Searches the normalized message. </summary>
    [VocabularyText("message")]
    Message = 1,

    /// <summary> Searches stack-trace or raw detail text. </summary>
    [VocabularyText("stack")]
    Stack = 2,

    /// <summary> Searches both message and secondary detail text. </summary>
    [VocabularyText("both")]
    Both = 3,
}
