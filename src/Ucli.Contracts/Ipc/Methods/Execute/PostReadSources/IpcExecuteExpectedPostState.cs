
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies the post-state availability expected from an <c>execute</c> source fact. </summary>
[VocabularyDefinition]
public enum IpcExecuteExpectedPostState
{
    /// <summary> Indicates a deterministic post-state observation target. </summary>
    [VocabularyText("deterministic")]
    Deterministic = 1,

    /// <summary> Indicates that the expected post-state cannot be derived from the source alone. </summary>
    [VocabularyText("unavailable")]
    Unavailable = 2,
}
