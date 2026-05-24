namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Defines supported asset-kind constraint parameters. </summary>
public enum UcliOperationAssetKind
{
    /// <summary> No asset kind parameter is specified. </summary>
    Unspecified = 0,

    /// <summary> Regular Unity asset. </summary>
    Asset = 1,

    /// <summary> Unity prefab asset. </summary>
    Prefab = 2,

    /// <summary> Unity project settings asset. </summary>
    ProjectSettings = 3,

    /// <summary> Unity scene asset. </summary>
    Scene = 4,
}
