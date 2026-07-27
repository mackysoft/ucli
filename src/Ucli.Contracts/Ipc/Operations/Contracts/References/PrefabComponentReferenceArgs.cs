using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> References a Component by prefab, hierarchy path, and component type. </summary>
[Description("Prefab hierarchy Component reference.")]
public sealed record PrefabComponentReferenceArgs : ComponentReferenceArgs, ResolveSelectorArgs
{
    /// <summary> Initializes a new prefab Component reference. </summary>
    /// <param name="prefab"> The Unity prefab asset path. </param>
    /// <param name="hierarchyPath"> The hierarchy path inside the prefab. </param>
    /// <param name="componentType"> The component type identifier. </param>
    [JsonConstructor]
    public PrefabComponentReferenceArgs (
        PrefabAssetPath prefab,
        UnityHierarchyPath hierarchyPath,
        UnityComponentTypeId componentType)
    {
        Prefab = ContractArgumentGuard.RequireNotNull(prefab, nameof(prefab));
        HierarchyPath = ContractArgumentGuard.RequireNotNull(hierarchyPath, nameof(hierarchyPath));
        ComponentType = ContractArgumentGuard.RequireNotNull(componentType, nameof(componentType));
    }

    /// <summary> Gets the Unity prefab asset path. </summary>
    [JsonInclude]
    [JsonRequired]
    [Description("Prefab asset path for the component selector.")]
    [UcliAssetExists(UcliOperationAssetKind.Prefab)]
    public PrefabAssetPath Prefab { get; private init; }

    /// <summary> Gets the hierarchy path inside the prefab. </summary>
    [JsonInclude]
    [JsonRequired]
    [Description("Unity hierarchy path inside the selected prefab.")]
    public UnityHierarchyPath HierarchyPath { get; private init; }

    /// <summary> Gets the component type identifier. </summary>
    [JsonInclude]
    [JsonRequired]
    [Description("Component type identifier.")]
    public UnityComponentTypeId ComponentType { get; private init; }
}
