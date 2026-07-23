
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines stable project mutation change-kind literals. </summary>
[VocabularyDefinition]
public enum IpcBuildProjectMutationChangeKind
{
    /// <summary> A project file was added. </summary>
    [VocabularyText("added")]
    Added = 1,

    /// <summary> A project file changed content. </summary>
    [VocabularyText("modified")]
    Modified = 2,

    /// <summary> A project file was deleted. </summary>
    [VocabularyText("deleted")]
    Deleted = 3,
}
