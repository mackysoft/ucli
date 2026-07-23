
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines the closed set of IPC streaming frame shapes. </summary>
[VocabularyDefinition]
public enum IpcStreamFrameKind
{
    /// <summary> Carries one non-terminal progress event. </summary>
    [VocabularyText("progress")]
    Progress = 1,

    /// <summary> Carries the terminal IPC response. </summary>
    [VocabularyText("terminal")]
    Terminal = 2,
}
