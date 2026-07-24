
namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Defines schema-entry kinds stored in index schema catalogs. </summary>
[VocabularyDefinition]
public enum IndexSchemaKind
{
    /// <summary> Represents one component schema entry. </summary>
    [VocabularyText("comp")]
    Comp = 0,

    /// <summary> Represents one asset schema entry. </summary>
    [VocabularyText("asset")]
    Asset = 1,
}
