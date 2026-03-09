using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Operations;

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

    private const string StrictEmptyObjectArgsSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false
        }
        """;

    private static readonly IReadOnlyList<UcliOperationDescriptor> Operations =
    [
        new UcliOperationDescriptor("ucli.resolve", UcliOperationKind.Query, OperationPolicy.Safe, ResolveArgsSchemaJson),
        new UcliOperationDescriptor("ucli.scene.open", UcliOperationKind.Query, OperationPolicy.Safe, ScenePathArgsSchemaJson),
        new UcliOperationDescriptor("ucli.scene.tree", UcliOperationKind.Query, OperationPolicy.Safe, SceneTreeArgsSchemaJson),
        new UcliOperationDescriptor("ucli.scene.save", UcliOperationKind.Mutation, OperationPolicy.Advanced, ScenePathArgsSchemaJson),
        new UcliOperationDescriptor("ucli.go.create", UcliOperationKind.Mutation, OperationPolicy.Advanced, GoCreateArgsSchemaJson),
        new UcliOperationDescriptor("ucli.go.describe", UcliOperationKind.Query, OperationPolicy.Safe, GoDescribeArgsSchemaJson),
        new UcliOperationDescriptor("ucli.project.refresh", UcliOperationKind.Mutation, OperationPolicy.Advanced, StrictEmptyObjectArgsSchemaJson),
        new UcliOperationDescriptor("ucli.project.save", UcliOperationKind.Mutation, OperationPolicy.Advanced, StrictEmptyObjectArgsSchemaJson),
    ];

    public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetOperations (CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Operations);
    }
}
