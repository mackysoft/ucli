using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Operations;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests;

internal sealed class InMemoryOperationCatalogProvider : IOperationCatalogProvider
{
    private static readonly string AssetTargetArgsSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "globalObjectId": { "type": "string", "minLength": 1 },
            "assetGuid": { "type": "string", "minLength": 1 },
            "assetPath": { "type": "string", "minLength": 1 },
            "projectAssetPath": { "type": "string", "minLength": 1 }
          },
          "oneOf": [
            { "required": ["globalObjectId"] },
            { "required": ["assetGuid"] },
            { "required": ["assetPath"] },
            { "required": ["projectAssetPath"] }
          ]
        }
        """;

    private static readonly string GameObjectTargetArgsSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "globalObjectId": { "type": "string", "minLength": 1 },
            "scene": { "type": "string", "minLength": 1 },
            "prefab": { "type": "string", "minLength": 1 },
            "hierarchyPath": { "type": "string", "minLength": 1 }
          },
          "oneOf": [
            { "required": ["globalObjectId"] },
            { "required": ["scene", "hierarchyPath"] },
            { "required": ["prefab", "hierarchyPath"] }
          ]
        }
        """;

    private static readonly string SceneGameObjectTargetArgsSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "globalObjectId": { "type": "string", "minLength": 1 },
            "scene": { "type": "string", "minLength": 1 },
            "hierarchyPath": { "type": "string", "minLength": 1 }
          },
          "oneOf": [
            { "required": ["globalObjectId"] },
            { "required": ["scene", "hierarchyPath"] }
          ]
        }
        """;

    private static readonly string ComponentTargetArgsSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "globalObjectId": { "type": "string", "minLength": 1 },
            "scene": { "type": "string", "minLength": 1 },
            "prefab": { "type": "string", "minLength": 1 },
            "hierarchyPath": { "type": "string", "minLength": 1 },
            "componentType": { "type": "string", "minLength": 1 }
          },
          "oneOf": [
            { "required": ["globalObjectId"] },
            { "required": ["scene", "hierarchyPath", "componentType"] },
            { "required": ["prefab", "hierarchyPath", "componentType"] }
          ]
        }
        """;

    private const string ResolveArgsSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "globalObjectId": { "type": "string", "minLength": 1 },
            "assetGuid": { "type": "string", "minLength": 1 },
            "assetPath": { "type": "string", "minLength": 1 },
            "projectAssetPath": { "type": "string", "minLength": 1 },
            "scene": { "type": "string", "minLength": 1 },
            "prefab": { "type": "string", "minLength": 1 },
            "hierarchyPath": { "type": "string", "minLength": 1 },
            "componentType": { "type": "string", "minLength": 1 }
          },
          "oneOf": [
            { "required": ["globalObjectId"] },
            { "required": ["assetGuid"] },
            { "required": ["assetPath"] },
            { "required": ["projectAssetPath"] },
            { "required": ["scene", "hierarchyPath"] },
            { "required": ["prefab", "hierarchyPath"] }
          ],
          "allOf": [
            {
              "if": { "required": ["componentType"] },
              "then": {
                "oneOf": [
                  { "required": ["scene", "hierarchyPath"] }
                ]
              }
            }
          ]
        }
        """;

    private const string ScenePathArgsSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "path": { "type": "string", "minLength": 1 }
          },
          "required": ["path"]
        }
        """;

    private const string SceneTreeArgsSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "path": { "type": "string", "minLength": 1 },
            "depth": {
              "type": ["integer", "null"],
              "minimum": 0
            }
          },
          "required": ["path"]
        }
        """;

    private const string GoCreateArgsSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "name": { "type": "string", "minLength": 1 },
            "scene": { "type": "string", "minLength": 1 },
            "parent": {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "globalObjectId": { "type": "string", "minLength": 1 },
                "scene": { "type": "string", "minLength": 1 },
                "prefab": { "type": "string", "minLength": 1 },
                "hierarchyPath": { "type": "string", "minLength": 1 }
              },
              "oneOf": [
                { "required": ["globalObjectId"] },
                { "required": ["scene", "hierarchyPath"] },
                { "required": ["prefab", "hierarchyPath"] }
              ]
            }
          },
          "required": ["name"],
          "oneOf": [
            { "required": ["scene"] },
            { "required": ["parent"] }
          ]
        }
        """;

    private const string GoDescribeArgsSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "target": {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "globalObjectId": { "type": "string", "minLength": 1 },
                "prefab": { "type": "string", "minLength": 1 },
                "scene": { "type": "string", "minLength": 1 },
                "hierarchyPath": { "type": "string", "minLength": 1 }
              },
              "oneOf": [
                { "required": ["globalObjectId"] },
                { "required": ["prefab", "hierarchyPath"] },
                { "required": ["scene", "hierarchyPath"] }
              ]
            },
            "depth": {
              "type": ["integer", "null"],
              "minimum": 0
            }
          },
          "required": ["target"]
        }
        """;

    private const string SceneQueryArgsSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "scene": { "type": "string", "minLength": 1 },
            "pathPrefix": { "type": "string", "minLength": 1 },
            "componentType": { "type": "string", "minLength": 1 }
          },
          "required": ["scene"]
        }
        """;

    private static readonly string ComponentEnsureArgsSchemaJson = $$"""
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "target": {{GameObjectTargetArgsSchemaJson}},
            "type": { "type": "string", "minLength": 1 }
          },
          "required": ["target", "type"]
        }
        """;

    private static readonly string ComponentSetArgsSchemaJson = $$"""
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "target": {{ComponentTargetArgsSchemaJson}},
            "sets": {
              "type": "array",
              "minItems": 1,
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "path": { "type": "string", "minLength": 1 },
                  "value": {}
                },
                "required": ["path", "value"]
              }
            }
          },
          "required": ["target", "sets"]
        }
        """;

    private static readonly string GoDeleteArgsSchemaJson = $$"""
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "target": {{GameObjectTargetArgsSchemaJson}}
          },
          "required": ["target"]
        }
        """;

    private static readonly string GoReparentArgsSchemaJson = $$"""
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "target": {{GameObjectTargetArgsSchemaJson}},
            "parent": {{GameObjectTargetArgsSchemaJson}}
          },
          "required": ["target", "parent"]
        }
        """;

    private const string AssetCreateArgsSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "path": { "type": "string", "minLength": 1 },
            "type": { "type": "string", "minLength": 1 }
          },
          "required": ["path", "type"]
        }
        """;

    private const string AssetsFindArgsSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "type": { "type": "string", "minLength": 1 },
            "pathPrefix": { "type": "string", "minLength": 1 },
            "nameContains": { "type": "string", "minLength": 1 }
          },
          "minProperties": 1
        }
        """;

    private static readonly string AssetSchemaArgsSchemaJson = $$"""
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "type": { "type": "string", "minLength": 1 },
            "target": {{AssetTargetArgsSchemaJson}}
          },
          "oneOf": [
            { "required": ["type"] },
            { "required": ["target"] }
          ]
        }
        """;

    private const string CompSchemaArgsSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "type": { "type": "string", "minLength": 1 }
          },
          "required": ["type"]
        }
        """;

    private static readonly string AssetSetArgsSchemaJson = $$"""
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "target": {{AssetTargetArgsSchemaJson}},
            "sets": {
              "type": "array",
              "minItems": 1,
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "path": { "type": "string", "minLength": 1 },
                  "value": {}
                },
                "required": ["path", "value"]
              }
            }
          },
          "required": ["target", "sets"]
        }
        """;

    private static readonly string PrefabCreateArgsSchemaJson = $$"""
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "target": {{SceneGameObjectTargetArgsSchemaJson}},
            "path": { "type": "string", "minLength": 1 }
          },
          "required": ["target", "path"]
        }
        """;

    private const string StrictEmptyObjectArgsSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false
        }
        """;

    private static readonly IReadOnlyList<UcliOperationDescriptor> Operations =
    [
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve, UcliOperationKind.Query, OperationPolicy.Safe, ResolveArgsSchemaJson),
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.AssetCreate, UcliOperationKind.Mutation, OperationPolicy.Advanced, AssetCreateArgsSchemaJson),
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.AssetsFind, UcliOperationKind.Query, OperationPolicy.Safe, AssetsFindArgsSchemaJson),
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.AssetSchema, UcliOperationKind.Query, OperationPolicy.Safe, AssetSchemaArgsSchemaJson),
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.AssetSet, UcliOperationKind.Mutation, OperationPolicy.Advanced, AssetSetArgsSchemaJson),
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.CompEnsure, UcliOperationKind.Mutation, OperationPolicy.Advanced, ComponentEnsureArgsSchemaJson),
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.CompSchema, UcliOperationKind.Query, OperationPolicy.Safe, CompSchemaArgsSchemaJson),
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.CompSet, UcliOperationKind.Mutation, OperationPolicy.Advanced, ComponentSetArgsSchemaJson),
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen, UcliOperationKind.Query, OperationPolicy.Safe, ScenePathArgsSchemaJson),
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneQuery, UcliOperationKind.Query, OperationPolicy.Safe, SceneQueryArgsSchemaJson),
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneTree, UcliOperationKind.Query, OperationPolicy.Safe, SceneTreeArgsSchemaJson),
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave, UcliOperationKind.Mutation, OperationPolicy.Advanced, ScenePathArgsSchemaJson),
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoCreate, UcliOperationKind.Mutation, OperationPolicy.Advanced, GoCreateArgsSchemaJson),
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDelete, UcliOperationKind.Mutation, OperationPolicy.Advanced, GoDeleteArgsSchemaJson),
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, UcliOperationKind.Query, OperationPolicy.Safe, GoDescribeArgsSchemaJson),
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoReparent, UcliOperationKind.Mutation, OperationPolicy.Advanced, GoReparentArgsSchemaJson),
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.PrefabCreate, UcliOperationKind.Mutation, OperationPolicy.Advanced, PrefabCreateArgsSchemaJson),
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.PrefabOpen, UcliOperationKind.Query, OperationPolicy.Safe, ScenePathArgsSchemaJson),
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.PrefabSave, UcliOperationKind.Mutation, OperationPolicy.Advanced, ScenePathArgsSchemaJson),
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh, UcliOperationKind.Mutation, OperationPolicy.Advanced, StrictEmptyObjectArgsSchemaJson),
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectSave, UcliOperationKind.Mutation, OperationPolicy.Advanced, StrictEmptyObjectArgsSchemaJson),
    ];

    public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetOperations (CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Operations);
    }

    public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetOperations (
        ResolvedUnityProjectContext unityProject,
        UcliConfig config,
        UnityExecutionMode mode = UnityExecutionMode.Auto,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(config);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Operations);
    }
}