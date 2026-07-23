
namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Defines supported asset-kind constraint parameters. </summary>
[VocabularyDefinition]
public enum UcliOperationAssetKind
{
    /// <summary> Regular Unity asset. </summary>
    [VocabularyText("asset")]
    Asset = 1,

    /// <summary> Unity prefab asset. </summary>
    [VocabularyText("prefab")]
    Prefab = 2,

    /// <summary> Unity project settings asset. </summary>
    [VocabularyText("projectSettings")]
    ProjectSettings = 3,

    /// <summary> Unity scene asset. </summary>
    [VocabularyText("scene")]
    Scene = 4,
}
