
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies the Unity process bootstrap target. </summary>
[VocabularyDefinition]
public enum IpcBootstrapTarget
{
    /// <summary> Starts the long-lived daemon host. </summary>
    [VocabularyText("daemon")]
    Daemon = 1,

    /// <summary> Starts a transient one-shot host. </summary>
    [VocabularyText("oneshot")]
    Oneshot = 2,
}
