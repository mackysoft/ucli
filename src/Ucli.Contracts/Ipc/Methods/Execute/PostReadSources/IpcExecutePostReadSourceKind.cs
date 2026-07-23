
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies the mutation source represented by an <c>execute</c> post-read fact. </summary>
[VocabularyDefinition]
public enum IpcExecutePostReadSourceKind
{
    /// <summary> Indicates a public edit step source. </summary>
    [VocabularyText("edit")]
    Edit = 1,

    /// <summary> Indicates a public raw-operation step source. </summary>
    [VocabularyText("operation")]
    Operation = 2,

    /// <summary> Indicates a project-refresh source. </summary>
    [VocabularyText("refresh")]
    Refresh = 3,
}
