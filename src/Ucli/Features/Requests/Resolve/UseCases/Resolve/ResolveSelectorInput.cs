namespace MackySoft.Ucli.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Represents one normalized selector supplied to <c>ucli resolve</c>. </summary>
internal sealed record ResolveSelectorInput (
    ResolveSelectorKind Kind,
    string? GlobalObjectId,
    string? AssetGuid,
    string? AssetPath,
    string? ProjectAssetPath,
    string? Scene,
    string? HierarchyPath,
    string? ComponentType,
    string? Prefab)
{
    /// <summary> Gets a value indicating whether this selector can be resolved from scene-tree-lite read-index data. </summary>
    public bool IsSceneHierarchyGameObject => Kind == ResolveSelectorKind.SceneHierarchyPath;
}