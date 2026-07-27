namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies how an asset schema is selected. </summary>
[VocabularyDefinition]
public enum AssetSchemaSelectionKind
{
    /// <summary> Selects a schema by Unity type identifier. </summary>
    [VocabularyText("type")]
    Type = 1,

    /// <summary> Selects a schema from an existing asset target. </summary>
    [VocabularyText("target")]
    Target = 2,
}
