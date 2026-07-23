
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines Unity log source literals. </summary>
[VocabularyDefinition]
public enum IpcUnityLogSource
{
    /// <summary> Unity compilation log source. </summary>
    [VocabularyText("compile")]
    Compile = 1,

    /// <summary> Unity runtime log source. </summary>
    [VocabularyText("runtime")]
    Runtime = 2,
}
