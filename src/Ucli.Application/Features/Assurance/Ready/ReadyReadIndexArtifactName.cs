
namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Identifies a read-index artifact represented by ready assurance evidence. </summary>
[VocabularyDefinition]
internal enum ReadyReadIndexArtifactName
{
    /// <summary> The read-index mode validation result. </summary>
    [VocabularyText("readIndex.mode")]
    Mode = 1,

    /// <summary> The operation catalog artifact. </summary>
    [VocabularyText("ops.catalog")]
    OpsCatalog = 2,

    /// <summary> The asset-search lookup artifact. </summary>
    [VocabularyText("asset-search.lookup")]
    AssetSearchLookup = 3,

    /// <summary> The GUID-to-path lookup artifact. </summary>
    [VocabularyText("guid-path.lookup")]
    GuidPathLookup = 4,
}
