namespace MackySoft.Ucli.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Identifies the normalized selector shape supplied to <c>ucli resolve</c>. </summary>
internal enum ResolveSelectorKind
{
    GlobalObjectId,
    AssetGuid,
    AssetPath,
    ProjectAssetPath,
    SceneHierarchyPath,
    SceneComponent,
    PrefabHierarchyPath,
}