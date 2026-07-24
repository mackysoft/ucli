
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines typed Play Mode subsystem transitions used by runtime lifecycle logic. </summary>
[VocabularyDefinition]
public enum IpcPlayModeTransition
{
    /// <summary> No Play Mode transition is active. </summary>
    [VocabularyText("none")]
    None,

    /// <summary> The Editor is entering Play Mode. </summary>
    [VocabularyText("entering")]
    Entering,

    /// <summary> The Editor is exiting Play Mode. </summary>
    [VocabularyText("exiting")]
    Exiting,
}
