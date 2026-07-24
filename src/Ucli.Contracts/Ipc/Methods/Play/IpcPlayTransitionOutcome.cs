
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies the outcome of one requested Play Mode transition. </summary>
[VocabularyDefinition]
public enum IpcPlayTransitionOutcome
{
    /// <summary> Indicates that this request entered Play Mode. </summary>
    [VocabularyText("entered")]
    Entered = 1,

    /// <summary> Indicates that Play Mode was already active. </summary>
    [VocabularyText("alreadyEntered")]
    AlreadyEntered = 2,

    /// <summary> Indicates that this request exited Play Mode. </summary>
    [VocabularyText("exited")]
    Exited = 3,

    /// <summary> Indicates that Play Mode was already stopped. </summary>
    [VocabularyText("alreadyExited")]
    AlreadyExited = 4,

    /// <summary> Indicates that the requested transition exceeded its deadline. </summary>
    [VocabularyText("timeout")]
    Timeout = 5,

    /// <summary> Indicates that the Editor state blocked the requested transition. </summary>
    [VocabularyText("blocked")]
    Blocked = 6,
}
