
namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Defines high-level operation kinds exposed by the operation catalog. </summary>
[VocabularyDefinition]
public enum UcliOperationKind
{
    /// <summary> Represents read-only operations. </summary>
    [VocabularyText("query")]
    Query = 0,

    /// <summary> Represents state-changing operations that can dirty or persist project content. </summary>
    [VocabularyText("mutation")]
    Mutation = 1,

    /// <summary> Represents editor-state commands that are not content mutations. </summary>
    [VocabularyText("command")]
    Command = 2,
}
