namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines JSON property names accepted by <c>ucli.resolve</c> selector arguments. </summary>
public static class IpcResolveSelectorPropertyNames
{
    /// <summary> Gets the property name for a GlobalObjectId selector. </summary>
    public const string GlobalObjectId = "globalObjectId";

    /// <summary> Gets the property name for an asset GUID selector. </summary>
    public const string AssetGuid = "assetGuid";

    /// <summary> Gets the property name for an asset path selector. </summary>
    public const string AssetPath = "assetPath";

    /// <summary> Gets the property name for a project-scoped asset path selector. </summary>
    public const string ProjectAssetPath = "projectAssetPath";

    /// <summary> Gets the property name for a scene path selector. </summary>
    public const string Scene = "scene";

    /// <summary> Gets the property name for a prefab path selector. </summary>
    public const string Prefab = "prefab";

    /// <summary> Gets the property name for a scene or prefab hierarchy path selector. </summary>
    public const string HierarchyPath = "hierarchyPath";

    /// <summary> Gets the property name for a component type selector. </summary>
    public const string ComponentType = "componentType";
}
