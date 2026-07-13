using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve.Contracts;

/// <summary> Represents a selector that resolves a prefab child by prefab path and hierarchy path. </summary>
internal sealed record ResolvePrefabHierarchySelectorInput : ResolveSelectorInput
{
    /// <summary> Initializes a prefab hierarchy selector with validated paths. </summary>
    /// <param name="prefab"> The Unity prefab asset path. </param>
    /// <param name="hierarchyPath"> The hierarchy path inside the prefab. </param>
    /// <exception cref="ArgumentNullException"> Thrown when either path is <see langword="null" />. </exception>
    public ResolvePrefabHierarchySelectorInput (
        PrefabAssetPath prefab,
        UnityHierarchyPath hierarchyPath)
    {
        Prefab = prefab ?? throw new ArgumentNullException(nameof(prefab));
        HierarchyPath = hierarchyPath ?? throw new ArgumentNullException(nameof(hierarchyPath));
    }

    /// <summary> Gets the Unity prefab asset path. </summary>
    public PrefabAssetPath Prefab { get; }

    /// <summary> Gets the hierarchy path inside the prefab. </summary>
    public UnityHierarchyPath HierarchyPath { get; }
}
