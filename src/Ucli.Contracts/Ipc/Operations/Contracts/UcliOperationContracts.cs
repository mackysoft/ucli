using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines shared args and result contract types for built-in uCLI primitive operations. </summary>
public static class UcliOperationContracts
{
    public const string AliasPropertyName = "var";

    [UcliDescription("Object selector accepted by ucli.resolve.")]
    [UcliOneOfRequired(IpcResolveSelectorPropertyNames.GlobalObjectId)]
    [UcliOneOfRequired(IpcResolveSelectorPropertyNames.AssetGuid)]
    [UcliOneOfRequired(IpcResolveSelectorPropertyNames.AssetPath)]
    [UcliOneOfRequired(IpcResolveSelectorPropertyNames.ProjectAssetPath)]
    [UcliOneOfRequired(IpcResolveSelectorPropertyNames.Scene, IpcResolveSelectorPropertyNames.HierarchyPath)]
    [UcliOneOfRequired(IpcResolveSelectorPropertyNames.Prefab, IpcResolveSelectorPropertyNames.HierarchyPath)]
    [UcliIfRequiredThenOneOfRequired(IpcResolveSelectorPropertyNames.ComponentType, IpcResolveSelectorPropertyNames.Scene, IpcResolveSelectorPropertyNames.HierarchyPath)]
    public sealed record ResolveSelectorArgs
    {
        [JsonConstructor]
        public ResolveSelectorArgs (
            string? globalObjectId,
            string? assetGuid,
            string? assetPath,
            string? projectAssetPath,
            string? scene,
            string? prefab,
            string? hierarchyPath,
            string? componentType)
        {
            GlobalObjectId = globalObjectId;
            AssetGuid = assetGuid;
            AssetPath = assetPath;
            ProjectAssetPath = projectAssetPath;
            Scene = scene;
            Prefab = prefab;
            HierarchyPath = hierarchyPath;
            ComponentType = componentType;
        }

        [UcliDescription("Resolved Unity GlobalObjectId.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? GlobalObjectId { get; init; }

        [UcliDescription("Asset GUID selector.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AssetGuid { get; init; }

        [UcliDescription("Asset path selector under the Unity project.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AssetPath { get; init; }

        [UcliDescription("Project-scoped asset path selector, such as ProjectSettings assets.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ProjectAssetPath { get; init; }

        [UcliDescription("Scene asset path for a hierarchy selector.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Scene { get; init; }

        [UcliDescription("Prefab asset path for a hierarchy selector.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Prefab { get; init; }

        [UcliDescription("Unity hierarchy path inside the selected scene or prefab.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? HierarchyPath { get; init; }

        [UcliDescription("Component type identifier for component selection.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ComponentType { get; init; }
    }

    [UcliDescription("GameObject reference accepted by GameObject operations.")]
    [UcliOneOfRequired(AliasPropertyName)]
    [UcliOneOfRequired(IpcResolveSelectorPropertyNames.GlobalObjectId)]
    [UcliOneOfRequired(IpcResolveSelectorPropertyNames.Prefab, IpcResolveSelectorPropertyNames.HierarchyPath)]
    [UcliOneOfRequired(IpcResolveSelectorPropertyNames.Scene, IpcResolveSelectorPropertyNames.HierarchyPath)]
    public sealed record GameObjectReferenceArgs
    {
        [JsonConstructor]
        public GameObjectReferenceArgs (
            string? alias,
            string? globalObjectId,
            string? prefab,
            string? scene,
            string? hierarchyPath)
        {
            Alias = alias;
            GlobalObjectId = globalObjectId;
            Prefab = prefab;
            Scene = scene;
            HierarchyPath = hierarchyPath;
        }

        [UcliDescription("Temporary plan alias produced earlier in the same request.")]
        [UcliMinLength(1)]
        [JsonPropertyName(AliasPropertyName)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Alias { get; init; }

        [UcliDescription("Resolved Unity GlobalObjectId.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? GlobalObjectId { get; init; }

        [UcliDescription("Prefab asset path for a hierarchy selector.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Prefab { get; init; }

        [UcliDescription("Scene asset path for a hierarchy selector.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Scene { get; init; }

        [UcliDescription("Unity hierarchy path inside the selected scene or prefab.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? HierarchyPath { get; init; }
    }

    [UcliDescription("Scene GameObject reference accepted by prefab creation.")]
    [UcliOneOfRequired(AliasPropertyName)]
    [UcliOneOfRequired(IpcResolveSelectorPropertyNames.GlobalObjectId)]
    [UcliOneOfRequired(IpcResolveSelectorPropertyNames.Scene, IpcResolveSelectorPropertyNames.HierarchyPath)]
    public sealed record SceneGameObjectReferenceArgs
    {
        [JsonConstructor]
        public SceneGameObjectReferenceArgs (
            string? alias,
            string? globalObjectId,
            string? scene,
            string? hierarchyPath)
        {
            Alias = alias;
            GlobalObjectId = globalObjectId;
            Scene = scene;
            HierarchyPath = hierarchyPath;
        }

        [UcliDescription("Temporary plan alias produced earlier in the same request.")]
        [UcliMinLength(1)]
        [JsonPropertyName(AliasPropertyName)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Alias { get; init; }

        [UcliDescription("Resolved Unity GlobalObjectId.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? GlobalObjectId { get; init; }

        [UcliDescription("Scene asset path for a hierarchy selector.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Scene { get; init; }

        [UcliDescription("Unity hierarchy path inside the selected scene.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? HierarchyPath { get; init; }
    }

    [UcliDescription("Component reference accepted by component operations.")]
    [UcliOneOfRequired(AliasPropertyName)]
    [UcliOneOfRequired(IpcResolveSelectorPropertyNames.GlobalObjectId)]
    [UcliOneOfRequired(IpcResolveSelectorPropertyNames.Scene, IpcResolveSelectorPropertyNames.HierarchyPath, IpcResolveSelectorPropertyNames.ComponentType)]
    [UcliOneOfRequired(IpcResolveSelectorPropertyNames.Prefab, IpcResolveSelectorPropertyNames.HierarchyPath, IpcResolveSelectorPropertyNames.ComponentType)]
    public sealed record ComponentReferenceArgs
    {
        [JsonConstructor]
        public ComponentReferenceArgs (
            string? alias,
            string? globalObjectId,
            string? scene,
            string? prefab,
            string? hierarchyPath,
            string? componentType)
        {
            Alias = alias;
            GlobalObjectId = globalObjectId;
            Scene = scene;
            Prefab = prefab;
            HierarchyPath = hierarchyPath;
            ComponentType = componentType;
        }

        [UcliDescription("Temporary plan alias produced earlier in the same request.")]
        [UcliMinLength(1)]
        [JsonPropertyName(AliasPropertyName)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Alias { get; init; }

        [UcliDescription("Resolved Unity GlobalObjectId.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? GlobalObjectId { get; init; }

        [UcliDescription("Scene asset path for a hierarchy selector.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Scene { get; init; }

        [UcliDescription("Prefab asset path for a hierarchy selector.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Prefab { get; init; }

        [UcliDescription("Unity hierarchy path inside the selected scene or prefab.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? HierarchyPath { get; init; }

        [UcliDescription("Component type identifier.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ComponentType { get; init; }
    }

    [UcliDescription("Asset reference accepted by asset operations.")]
    [UcliOneOfRequired(AliasPropertyName)]
    [UcliOneOfRequired(IpcResolveSelectorPropertyNames.GlobalObjectId)]
    [UcliOneOfRequired(IpcResolveSelectorPropertyNames.AssetGuid)]
    [UcliOneOfRequired(IpcResolveSelectorPropertyNames.AssetPath)]
    [UcliOneOfRequired(IpcResolveSelectorPropertyNames.ProjectAssetPath)]
    public sealed record AssetReferenceArgs
    {
        [JsonConstructor]
        public AssetReferenceArgs (
            string? alias,
            string? globalObjectId,
            string? assetGuid,
            string? assetPath,
            string? projectAssetPath)
        {
            Alias = alias;
            GlobalObjectId = globalObjectId;
            AssetGuid = assetGuid;
            AssetPath = assetPath;
            ProjectAssetPath = projectAssetPath;
        }

        [UcliDescription("Temporary plan alias produced earlier in the same request.")]
        [UcliMinLength(1)]
        [JsonPropertyName(AliasPropertyName)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Alias { get; init; }

        [UcliDescription("Resolved Unity GlobalObjectId.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? GlobalObjectId { get; init; }

        [UcliDescription("Asset GUID selector.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AssetGuid { get; init; }

        [UcliDescription("Asset path selector under the Unity project.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AssetPath { get; init; }

        [UcliDescription("Project-scoped asset path selector.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ProjectAssetPath { get; init; }
    }

    [UcliDescription("Single path operation arguments.")]
    public sealed record PathArgs
    {
        [JsonConstructor]
        public PathArgs (string path)
        {
            Path = path;
        }

        [UcliRequired]
        [UcliDescription("Unity project relative asset path.")]
        [UcliMinLength(1)]
        public string Path { get; init; }
    }

    [UcliDescription("Single Unity type operation arguments.")]
    public sealed record TypeArgs
    {
        [JsonConstructor]
        public TypeArgs (string type)
        {
            Type = type;
        }

        [UcliRequired]
        [UcliDescription("Assembly-qualified or otherwise resolvable Unity type identifier.")]
        [UcliMinLength(1)]
        public string Type { get; init; }
    }

    [UcliDescription("Scene tree operation arguments.")]
    public sealed record SceneTreeArgs
    {
        [JsonConstructor]
        public SceneTreeArgs (
            string path,
            int? depth)
        {
            Path = path;
            Depth = depth;
        }

        [UcliRequired]
        [UcliDescription("Scene asset path to inspect.")]
        [UcliMinLength(1)]
        public string Path { get; init; }

        [UcliDescription("Maximum hierarchy depth to include; null means unbounded.")]
        [UcliMinimum(0)]
        public int? Depth { get; init; }
    }

    [UcliDescription("Scene query operation arguments.")]
    public sealed record SceneQueryArgs
    {
        [JsonConstructor]
        public SceneQueryArgs (
            string scene,
            string? pathPrefix,
            string? componentType)
        {
            Scene = scene;
            PathPrefix = pathPrefix;
            ComponentType = componentType;
        }

        [UcliRequired]
        [UcliDescription("Scene asset path to query.")]
        [UcliMinLength(1)]
        public string Scene { get; init; }

        [UcliDescription("Optional hierarchy path prefix filter.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PathPrefix { get; init; }

        [UcliDescription("Optional component type identifier filter.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ComponentType { get; init; }
    }

    [UcliDescription("Asset search operation arguments.")]
    [UcliMinProperties(1)]
    public sealed record AssetsFindArgs
    {
        [JsonConstructor]
        public AssetsFindArgs (
            string? type,
            string? pathPrefix,
            string? nameContains)
        {
            Type = type;
            PathPrefix = pathPrefix;
            NameContains = nameContains;
        }

        [UcliDescription("Optional asset type identifier filter.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Type { get; init; }

        [UcliDescription("Optional asset path prefix filter.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PathPrefix { get; init; }

        [UcliDescription("Optional case-sensitive asset name substring filter.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? NameContains { get; init; }
    }

    [UcliDescription("Asset creation operation arguments.")]
    public sealed record AssetCreateArgs
    {
        [JsonConstructor]
        public AssetCreateArgs (
            string type,
            string path)
        {
            Type = type;
            Path = path;
        }

        [UcliRequired]
        [UcliDescription("Unity asset type identifier to create.")]
        [UcliMinLength(1)]
        public string Type { get; init; }

        [UcliRequired]
        [UcliDescription("Unity project relative asset path to create.")]
        [UcliMinLength(1)]
        public string Path { get; init; }
    }

    [UcliDescription("Asset schema operation arguments.")]
    [UcliOneOfRequired("type")]
    [UcliOneOfRequired("target")]
    public sealed record AssetSchemaArgs
    {
        [JsonConstructor]
        public AssetSchemaArgs (
            string? type,
            AssetReferenceArgs? target)
        {
            Type = type;
            Target = target;
        }

        [UcliDescription("Unity asset type identifier to inspect.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Type { get; init; }

        [UcliDescription("Existing asset target to inspect.")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AssetReferenceArgs? Target { get; init; }
    }

    [UcliDescription("Serialized object set item.")]
    public sealed record SerializedObjectSetItemArgs
    {
        [JsonConstructor]
        public SerializedObjectSetItemArgs (
            string path,
            JsonElement value)
        {
            Path = path;
            Value = value;
        }

        [UcliRequired]
        [UcliDescription("SerializedProperty path to assign.")]
        [UcliMinLength(1)]
        public string Path { get; init; }

        [UcliRequired]
        [UcliDescription("JSON value assigned to the serialized property.")]
        [UcliSchemaAny]
        public JsonElement Value { get; init; }
    }

    [UcliDescription("Asset property set operation arguments.")]
    public sealed record AssetSetArgs
    {
        [JsonConstructor]
        public AssetSetArgs (
            AssetReferenceArgs target,
            IReadOnlyList<SerializedObjectSetItemArgs> sets)
        {
            Target = target;
            Sets = sets;
        }

        [UcliRequired]
        [UcliDescription("Target asset to modify.")]
        public AssetReferenceArgs Target { get; init; }

        [UcliRequired]
        [UcliDescription("Serialized property assignments.")]
        [UcliMinItems(1)]
        public IReadOnlyList<SerializedObjectSetItemArgs> Sets { get; init; }
    }

    [UcliDescription("Component property set operation arguments.")]
    public sealed record ComponentSetArgs
    {
        [JsonConstructor]
        public ComponentSetArgs (
            ComponentReferenceArgs target,
            IReadOnlyList<SerializedObjectSetItemArgs> sets)
        {
            Target = target;
            Sets = sets;
        }

        [UcliRequired]
        [UcliDescription("Target component to modify.")]
        public ComponentReferenceArgs Target { get; init; }

        [UcliRequired]
        [UcliDescription("Serialized property assignments.")]
        [UcliMinItems(1)]
        public IReadOnlyList<SerializedObjectSetItemArgs> Sets { get; init; }
    }

    [UcliDescription("Component ensure operation arguments.")]
    public sealed record ComponentEnsureArgs
    {
        [JsonConstructor]
        public ComponentEnsureArgs (
            GameObjectReferenceArgs target,
            string type)
        {
            Target = target;
            Type = type;
        }

        [UcliRequired]
        [UcliDescription("Target GameObject that should contain the component.")]
        public GameObjectReferenceArgs Target { get; init; }

        [UcliRequired]
        [UcliDescription("Component type identifier to ensure.")]
        [UcliMinLength(1)]
        public string Type { get; init; }
    }

    [UcliDescription("GameObject creation operation arguments.")]
    [UcliOneOfRequired("scene")]
    [UcliOneOfRequired("parent")]
    public sealed record GoCreateArgs
    {
        [JsonConstructor]
        public GoCreateArgs (
            string name,
            string? scene,
            GameObjectReferenceArgs? parent)
        {
            Name = name;
            Scene = scene;
            Parent = parent;
        }

        [UcliRequired]
        [UcliDescription("Name assigned to the created GameObject.")]
        [UcliMinLength(1)]
        public string Name { get; init; }

        [UcliDescription("Scene asset path that receives the new root GameObject.")]
        [UcliMinLength(1)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Scene { get; init; }

        [UcliDescription("Optional parent GameObject reference.")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GameObjectReferenceArgs? Parent { get; init; }
    }

    [UcliDescription("GameObject target operation arguments.")]
    public sealed record GoTargetArgs
    {
        [JsonConstructor]
        public GoTargetArgs (GameObjectReferenceArgs target)
        {
            Target = target;
        }

        [UcliRequired]
        [UcliDescription("Target GameObject reference.")]
        public GameObjectReferenceArgs Target { get; init; }
    }

    [UcliDescription("GameObject describe operation arguments.")]
    public sealed record GoDescribeArgs
    {
        [JsonConstructor]
        public GoDescribeArgs (
            GameObjectReferenceArgs target,
            int? depth)
        {
            Target = target;
            Depth = depth;
        }

        [UcliRequired]
        [UcliDescription("Target GameObject reference.")]
        public GameObjectReferenceArgs Target { get; init; }

        [UcliDescription("Maximum child hierarchy depth to include; null means unbounded.")]
        [UcliMinimum(0)]
        public int? Depth { get; init; }
    }

    [UcliDescription("GameObject reparent operation arguments.")]
    public sealed record GoReparentArgs
    {
        [JsonConstructor]
        public GoReparentArgs (
            GameObjectReferenceArgs target,
            GameObjectReferenceArgs parent)
        {
            Target = target;
            Parent = parent;
        }

        [UcliRequired]
        [UcliDescription("Target GameObject reference.")]
        public GameObjectReferenceArgs Target { get; init; }

        [UcliRequired]
        [UcliDescription("New parent GameObject reference.")]
        public GameObjectReferenceArgs Parent { get; init; }
    }

    [UcliDescription("Prefab creation operation arguments.")]
    public sealed record PrefabCreateArgs
    {
        [JsonConstructor]
        public PrefabCreateArgs (
            SceneGameObjectReferenceArgs target,
            string path)
        {
            Target = target;
            Path = path;
        }

        [UcliRequired]
        [UcliDescription("Source scene GameObject reference.")]
        public SceneGameObjectReferenceArgs Target { get; init; }

        [UcliRequired]
        [UcliDescription("Prefab asset path to create.")]
        [UcliMinLength(1)]
        public string Path { get; init; }
    }

    [UcliDescription("Assets find operation result.")]
    public sealed record AssetsFindResult
    {
        [JsonConstructor]
        public AssetsFindResult (IReadOnlyList<AssetsFindMatch> matches)
        {
            Matches = matches;
        }

        [UcliRequired]
        [UcliDescription("Matched assets in ordinal asset path order.")]
        public IReadOnlyList<AssetsFindMatch> Matches { get; init; }
    }

    [UcliDescription("Single asset search match.")]
    public sealed record AssetsFindMatch
    {
        [JsonConstructor]
        public AssetsFindMatch (
            string assetPath,
            string assetGuid,
            string name,
            string typeId)
        {
            AssetPath = assetPath;
            AssetGuid = assetGuid;
            Name = name;
            TypeId = typeId;
        }

        [UcliRequired]
        [UcliDescription("Unity project relative asset path.")]
        [UcliMinLength(1)]
        public string AssetPath { get; init; }

        [UcliRequired]
        [UcliDescription("Unity asset GUID.")]
        [UcliMinLength(1)]
        public string AssetGuid { get; init; }

        [UcliRequired]
        [UcliDescription("Asset object name.")]
        [UcliMinLength(1)]
        public string Name { get; init; }

        [UcliRequired]
        [UcliDescription("Asset type identifier.")]
        [UcliMinLength(1)]
        public string TypeId { get; init; }
    }

    [UcliDescription("Scene query operation result.")]
    public sealed record SceneQueryResult
    {
        [JsonConstructor]
        public SceneQueryResult (
            string scene,
            IReadOnlyList<SceneQueryMatch> matches)
        {
            Scene = scene;
            Matches = matches;
        }

        [UcliRequired]
        [UcliDescription("Scene asset path that was queried.")]
        [UcliMinLength(1)]
        public string Scene { get; init; }

        [UcliRequired]
        [UcliDescription("Matched scene objects or components.")]
        public IReadOnlyList<SceneQueryMatch> Matches { get; init; }
    }

    [UcliDescription("Single scene query match.")]
    public sealed record SceneQueryMatch
    {
        [JsonConstructor]
        public SceneQueryMatch (
            string kind,
            string hierarchyPath,
            string? componentType)
        {
            Kind = kind;
            HierarchyPath = hierarchyPath;
            ComponentType = componentType;
        }

        [UcliRequired]
        [UcliDescription("Matched target kind.")]
        [UcliMinLength(1)]
        public string Kind { get; init; }

        [UcliRequired]
        [UcliDescription("Matched GameObject hierarchy path.")]
        [UcliMinLength(1)]
        public string HierarchyPath { get; init; }

        [UcliDescription("Matched component type identifier for component matches.")]
        [UcliMinLength(1)]
        [UcliNullable]
        public string? ComponentType { get; init; }
    }

    [UcliDescription("Scene tree operation result.")]
    public sealed record SceneTreeResult
    {
        [JsonConstructor]
        public SceneTreeResult (
            string path,
            IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> roots)
        {
            Path = path;
            Roots = roots;
        }

        [UcliRequired]
        [UcliDescription("Scene asset path that was described.")]
        [UcliMinLength(1)]
        public string Path { get; init; }

        [UcliRequired]
        [UcliDescription("Root GameObjects in the scene.")]
        public IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> Roots { get; init; }
    }

    [UcliDescription("GameObject describe operation result.")]
    public sealed record GameObjectDescriptionResult
    {
        [JsonConstructor]
        public GameObjectDescriptionResult (
            string name,
            string globalObjectId,
            IReadOnlyList<GameObjectComponentDescriptionResult> components,
            IReadOnlyList<GameObjectDescriptionResult> children)
        {
            Name = name;
            GlobalObjectId = globalObjectId;
            Components = components;
            Children = children;
        }

        [UcliRequired]
        [UcliDescription("GameObject name.")]
        [UcliMinLength(1)]
        public string Name { get; init; }

        [UcliRequired]
        [UcliDescription("GameObject GlobalObjectId.")]
        [UcliMinLength(1)]
        public string GlobalObjectId { get; init; }

        [UcliRequired]
        [UcliDescription("Components attached to this GameObject.")]
        public IReadOnlyList<GameObjectComponentDescriptionResult> Components { get; init; }

        [UcliRequired]
        [UcliDescription("Child GameObject descriptions.")]
        public IReadOnlyList<GameObjectDescriptionResult> Children { get; init; }
    }

    [UcliDescription("Component entry in a GameObject description.")]
    public sealed record GameObjectComponentDescriptionResult
    {
        [JsonConstructor]
        public GameObjectComponentDescriptionResult (string? typeName)
        {
            TypeName = typeName;
        }

        [UcliDescription("Component type name, or null when the component script is missing.")]
        [UcliNullable]
        public string? TypeName { get; init; }
    }

}
