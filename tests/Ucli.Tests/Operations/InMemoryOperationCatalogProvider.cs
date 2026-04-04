using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Operations;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests;

internal sealed class InMemoryOperationCatalogProvider : IOperationCatalogProvider
{
    private const string ResolveArgsSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "globalObjectId": { "type": "string", "minLength": 1 },
            "assetGuid": { "type": "string", "minLength": 1 },
            "assetPath": { "type": "string", "minLength": 1 },
            "scene": { "type": "string", "minLength": 1 },
            "hierarchyPath": { "type": "string", "minLength": 1 }
          },
          "oneOf": [
            { "required": ["globalObjectId"] },
            { "required": ["assetGuid"] },
            { "required": ["assetPath"] },
            { "required": ["scene", "hierarchyPath"] }
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
                "var": { "type": "string", "minLength": 1 },
                "globalObjectId": { "type": "string", "minLength": 1 },
                "scene": { "type": "string", "minLength": 1 },
                "hierarchyPath": { "type": "string", "minLength": 1 }
              },
              "oneOf": [
                { "required": ["var"] },
                { "required": ["globalObjectId"] },
                { "required": ["scene", "hierarchyPath"] }
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
                "var": { "type": "string", "minLength": 1 },
                "globalObjectId": { "type": "string", "minLength": 1 },
                "scene": { "type": "string", "minLength": 1 },
                "hierarchyPath": { "type": "string", "minLength": 1 }
              },
              "oneOf": [
                { "required": ["var"] },
                { "required": ["globalObjectId"] },
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

    private const string ComponentEnsureArgsSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "target": { "type": "object" },
            "type": { "type": "string", "minLength": 1 }
          },
          "required": ["target", "type"]
        }
        """;

    private const string ComponentSetArgsSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "target": { "type": "object" },
            "sets": {
              "type": "array",
              "items": { "type": "object" }
            }
          },
          "required": ["target", "sets"]
        }
        """;

    private const string GoDeleteArgsSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "target": { "type": "object" }
          },
          "required": ["target"]
        }
        """;

    private const string GoReparentArgsSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "target": { "type": "object" },
            "parent": { "type": "object" }
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

    private const string AssetMutationArgsSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "target": { "type": "object" }
          },
          "required": ["target"]
        }
        """;

    private const string PrefabCreateArgsSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "target": { "type": "object" },
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
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.AssetSchema, UcliOperationKind.Query, OperationPolicy.Safe, AssetMutationArgsSchemaJson),
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.AssetSet, UcliOperationKind.Mutation, OperationPolicy.Advanced, AssetMutationArgsSchemaJson),
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.CompEnsure, UcliOperationKind.Mutation, OperationPolicy.Advanced, ComponentEnsureArgsSchemaJson),
        new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.CompSchema, UcliOperationKind.Query, OperationPolicy.Safe, AssetMutationArgsSchemaJson),
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(config);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Operations);
    }
}