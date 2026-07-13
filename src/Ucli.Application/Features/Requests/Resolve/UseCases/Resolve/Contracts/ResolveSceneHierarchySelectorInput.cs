using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve.Contracts;

/// <summary> Represents a selector that resolves a GameObject by scene and hierarchy path. </summary>
internal sealed record ResolveSceneHierarchySelectorInput : ResolveSelectorInput
{
    /// <summary> Initializes a scene hierarchy selector with validated paths. </summary>
    /// <param name="scene"> The Unity scene asset path. </param>
    /// <param name="hierarchyPath"> The hierarchy path inside the scene. </param>
    /// <exception cref="ArgumentNullException"> Thrown when either path is <see langword="null" />. </exception>
    public ResolveSceneHierarchySelectorInput (
        SceneAssetPath scene,
        UnityHierarchyPath hierarchyPath)
    {
        Scene = scene ?? throw new ArgumentNullException(nameof(scene));
        HierarchyPath = hierarchyPath ?? throw new ArgumentNullException(nameof(hierarchyPath));
    }

    /// <summary> Gets the Unity scene asset path. </summary>
    public SceneAssetPath Scene { get; }

    /// <summary> Gets the hierarchy path inside the scene. </summary>
    public UnityHierarchyPath HierarchyPath { get; }
}
