using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> References a Component by scene, hierarchy path, and component type. </summary>
[Description("Scene hierarchy Component reference.")]
public sealed record SceneComponentReferenceArgs : ComponentReferenceArgs, ResolveSelectorArgs
{
    /// <summary> Initializes a new scene Component reference. </summary>
    /// <param name="scene"> The Unity scene asset path. </param>
    /// <param name="hierarchyPath"> The hierarchy path inside the scene. </param>
    /// <param name="componentType"> The component type identifier. </param>
    [JsonConstructor]
    public SceneComponentReferenceArgs (
        SceneAssetPath scene,
        UnityHierarchyPath hierarchyPath,
        UnityComponentTypeId componentType)
    {
        Scene = ContractArgumentGuard.RequireNotNull(scene, nameof(scene));
        HierarchyPath = ContractArgumentGuard.RequireNotNull(hierarchyPath, nameof(hierarchyPath));
        ComponentType = ContractArgumentGuard.RequireNotNull(componentType, nameof(componentType));
    }

    /// <summary> Gets the Unity scene asset path. </summary>
    [JsonInclude]
    [JsonRequired]
    [Description("Scene asset path for the component selector.")]
    [UcliAssetExists(UcliOperationAssetKind.Scene)]
    public SceneAssetPath Scene { get; private init; }

    /// <summary> Gets the hierarchy path inside the scene. </summary>
    [JsonInclude]
    [JsonRequired]
    [Description("Unity hierarchy path inside the selected scene.")]
    public UnityHierarchyPath HierarchyPath { get; private init; }

    /// <summary> Gets the component type identifier. </summary>
    [JsonInclude]
    [JsonRequired]
    [Description("Component type identifier.")]
    public UnityComponentTypeId ComponentType { get; private init; }
}
