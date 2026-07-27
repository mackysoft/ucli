using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> References a GameObject by scene and hierarchy path. </summary>
[Description("Scene hierarchy GameObject reference.")]
public sealed record SceneHierarchyReferenceArgs :
    GameObjectReferenceArgs,
    SceneGameObjectReferenceArgs,
    ResolveSelectorArgs
{
    /// <summary> Initializes a new scene hierarchy reference. </summary>
    /// <param name="scene"> The Unity scene asset path. </param>
    /// <param name="hierarchyPath"> The hierarchy path inside the scene. </param>
    [JsonConstructor]
    public SceneHierarchyReferenceArgs (
        SceneAssetPath scene,
        UnityHierarchyPath hierarchyPath)
    {
        Scene = ContractArgumentGuard.RequireNotNull(scene, nameof(scene));
        HierarchyPath = ContractArgumentGuard.RequireNotNull(hierarchyPath, nameof(hierarchyPath));
    }

    /// <summary> Gets the Unity scene asset path. </summary>
    [JsonInclude]
    [JsonRequired]
    [Description("Scene asset path for the hierarchy selector.")]
    [UcliAssetExists(UcliOperationAssetKind.Scene)]
    public SceneAssetPath Scene { get; private init; }

    /// <summary> Gets the hierarchy path inside the scene. </summary>
    [JsonInclude]
    [JsonRequired]
    [Description("Unity hierarchy path inside the selected scene.")]
    public UnityHierarchyPath HierarchyPath { get; private init; }
}
