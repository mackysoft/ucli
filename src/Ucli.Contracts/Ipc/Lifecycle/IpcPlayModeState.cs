
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines typed Play Mode subsystem states used by runtime lifecycle logic. </summary>
[VocabularyDefinition]
public enum IpcPlayModeState
{
    /// <summary> Play Mode is inactive and no transition is pending. </summary>
    [VocabularyText("stopped")]
    Stopped,

    /// <summary> The Editor is entering Play Mode. </summary>
    [VocabularyText("entering")]
    Entering,

    /// <summary> Play Mode is active. </summary>
    [VocabularyText("playing")]
    Playing,

    /// <summary> The Editor is exiting Play Mode. </summary>
    [VocabularyText("exiting")]
    Exiting,

    /// <summary> Play Mode state could not be classified. </summary>
    [VocabularyText("unknown")]
    Unknown,
}
