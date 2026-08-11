using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines why a requested missing-script scan target was not fully observed. </summary>
[VocabularyDefinition]
public enum MissingScriptsUnscannedReason
{
    /// <summary> Unity could not enumerate the requested asset scope. </summary>
    [VocabularyText("scopeReadFailed")]
    ScopeReadFailed = 1,

    /// <summary> The asset changed or disappeared after its scope was enumerated. </summary>
    [VocabularyText("assetChanged")]
    AssetChanged = 2,

    /// <summary> Unity could not read the saved asset contents. </summary>
    [VocabularyText("assetReadFailed")]
    AssetReadFailed = 3,

    /// <summary> At least one GameObject hierarchy cannot be represented by the operation result contract. </summary>
    [VocabularyText("hierarchyPathUnrepresentable")]
    HierarchyPathUnrepresentable = 4,
}
