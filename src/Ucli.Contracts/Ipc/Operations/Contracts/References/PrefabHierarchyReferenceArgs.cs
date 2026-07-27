using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> References a GameObject by prefab and hierarchy path. </summary>
[Description("Prefab hierarchy GameObject reference.")]
public sealed record PrefabHierarchyReferenceArgs : GameObjectReferenceArgs, ResolveSelectorArgs
{
    /// <summary> Initializes a new prefab hierarchy reference. </summary>
    /// <param name="prefab"> The Unity prefab asset path. </param>
    /// <param name="hierarchyPath"> The hierarchy path inside the prefab. </param>
    [JsonConstructor]
    public PrefabHierarchyReferenceArgs (
        PrefabAssetPath prefab,
        UnityHierarchyPath hierarchyPath)
    {
        Prefab = ContractArgumentGuard.RequireNotNull(prefab, nameof(prefab));
        HierarchyPath = ContractArgumentGuard.RequireNotNull(hierarchyPath, nameof(hierarchyPath));
    }

    /// <summary> Gets the Unity prefab asset path. </summary>
    [JsonInclude]
    [JsonRequired]
    [Description("Prefab asset path for the hierarchy selector.")]
    [UcliAssetExists(UcliOperationAssetKind.Prefab)]
    public PrefabAssetPath Prefab { get; private init; }

    /// <summary> Gets the hierarchy path inside the prefab. </summary>
    [JsonInclude]
    [JsonRequired]
    [Description("Unity hierarchy path inside the selected prefab.")]
    public UnityHierarchyPath HierarchyPath { get; private init; }
}
