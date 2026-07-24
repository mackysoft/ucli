
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies the persistence commit requested by an <c>execute</c> edit step. </summary>
[VocabularyDefinition]
public enum IpcExecutePostReadCommit
{
    /// <summary> Indicates that the source requested no persistence commit. </summary>
    [VocabularyText("none")]
    None = 1,

    /// <summary> Indicates that the source requested a context-scoped commit. </summary>
    [VocabularyText("context")]
    Context = 2,

    /// <summary> Indicates that the source requested a project-scoped commit. </summary>
    [VocabularyText("project")]
    Project = 3,
}
