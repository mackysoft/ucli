using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve.Contracts;

/// <summary> Represents a selector that resolves a component by scene, hierarchy path, and component type. </summary>
internal sealed record ResolveSceneComponentSelectorInput : ResolveSelectorInput
{
    /// <summary> Initializes a scene component selector with validated semantic values. </summary>
    /// <param name="scene"> The Unity scene asset path. </param>
    /// <param name="hierarchyPath"> The hierarchy path inside the scene. </param>
    /// <param name="componentType"> The component type identifier. </param>
    /// <exception cref="ArgumentNullException"> Thrown when any required value is <see langword="null" />. </exception>
    public ResolveSceneComponentSelectorInput (
        SceneAssetPath scene,
        UnityHierarchyPath hierarchyPath,
        UnityComponentTypeId componentType)
    {
        Scene = scene ?? throw new ArgumentNullException(nameof(scene));
        HierarchyPath = hierarchyPath ?? throw new ArgumentNullException(nameof(hierarchyPath));
        ComponentType = componentType ?? throw new ArgumentNullException(nameof(componentType));
    }

    /// <summary> Gets the Unity scene asset path. </summary>
    public SceneAssetPath Scene { get; }

    /// <summary> Gets the hierarchy path inside the scene. </summary>
    public UnityHierarchyPath HierarchyPath { get; }

    /// <summary> Gets the component type identifier. </summary>
    public UnityComponentTypeId ComponentType { get; }
}
