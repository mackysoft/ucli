
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies the requested Play Mode transition. </summary>
[VocabularyDefinition]
public enum IpcPlayTransitionCommand
{
    /// <summary> Requests entry into Play Mode. </summary>
    [VocabularyText("enter")]
    Enter = 1,

    /// <summary> Requests exit from Play Mode. </summary>
    [VocabularyText("exit")]
    Exit = 2,
}
