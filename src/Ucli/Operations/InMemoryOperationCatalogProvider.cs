using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Operations;

/// <summary> Provides temporary in-memory operation metadata for foundation-stage development. </summary>
internal sealed class InMemoryOperationCatalogProvider : IOperationCatalogProvider
{
    private const string DefaultArgsSchemaJson = """{"type":"object"}""";

    private const string ResolveArgsSchemaJson =
        """
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

    private const string ScenePathArgsSchemaJson =
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "path": { "type": "string", "minLength": 1 }
          },
          "required": ["path"]
        }
        """;

    private const string SceneTreeArgsSchemaJson =
        """
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

    private static readonly IReadOnlyList<UcliOperationDescriptor> Operations =
    [
        new UcliOperationDescriptor("ucli.asset.create", UcliOperationKind.Mutation, OperationPolicy.Advanced, DefaultArgsSchemaJson),
        new UcliOperationDescriptor("ucli.asset.schema", UcliOperationKind.Query, OperationPolicy.Safe, DefaultArgsSchemaJson),
        new UcliOperationDescriptor("ucli.asset.set", UcliOperationKind.Mutation, OperationPolicy.Advanced, DefaultArgsSchemaJson),

        new UcliOperationDescriptor("ucli.assets.find", UcliOperationKind.Query, OperationPolicy.Safe, DefaultArgsSchemaJson),

        new UcliOperationDescriptor("ucli.comp.ensure", UcliOperationKind.Mutation, OperationPolicy.Advanced, DefaultArgsSchemaJson),
        new UcliOperationDescriptor("ucli.comp.schema", UcliOperationKind.Query, OperationPolicy.Safe, DefaultArgsSchemaJson),
        new UcliOperationDescriptor("ucli.comp.set", UcliOperationKind.Mutation, OperationPolicy.Advanced, DefaultArgsSchemaJson),

        new UcliOperationDescriptor("ucli.cs.invoke", UcliOperationKind.Mutation, OperationPolicy.Dangerous, DefaultArgsSchemaJson),

        new UcliOperationDescriptor("ucli.go.create", UcliOperationKind.Mutation, OperationPolicy.Advanced, DefaultArgsSchemaJson),
        new UcliOperationDescriptor("ucli.go.describe", UcliOperationKind.Query, OperationPolicy.Safe, DefaultArgsSchemaJson),

        new UcliOperationDescriptor("ucli.prefab.create", UcliOperationKind.Mutation, OperationPolicy.Advanced, DefaultArgsSchemaJson),
        new UcliOperationDescriptor("ucli.prefab.open", UcliOperationKind.Query, OperationPolicy.Safe, DefaultArgsSchemaJson),
        new UcliOperationDescriptor("ucli.prefab.save", UcliOperationKind.Mutation, OperationPolicy.Advanced, DefaultArgsSchemaJson),

        new UcliOperationDescriptor("ucli.project.refresh", UcliOperationKind.Mutation, OperationPolicy.Advanced, DefaultArgsSchemaJson),
        new UcliOperationDescriptor("ucli.project.save", UcliOperationKind.Mutation, OperationPolicy.Advanced, DefaultArgsSchemaJson),

        new UcliOperationDescriptor("ucli.resolve", UcliOperationKind.Query, OperationPolicy.Safe, ResolveArgsSchemaJson),

        new UcliOperationDescriptor("ucli.scene.open", UcliOperationKind.Query, OperationPolicy.Safe, ScenePathArgsSchemaJson),
        new UcliOperationDescriptor("ucli.scene.save", UcliOperationKind.Mutation, OperationPolicy.Advanced, ScenePathArgsSchemaJson),
        new UcliOperationDescriptor("ucli.scene.tree", UcliOperationKind.Query, OperationPolicy.Safe, SceneTreeArgsSchemaJson),

        new UcliOperationDescriptor("ucli.scenes.findComponents", UcliOperationKind.Query, OperationPolicy.Safe, DefaultArgsSchemaJson),
    ];

    /// <summary> Asynchronously gets operation descriptor values used for catalog construction. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the operation descriptor collection. </returns>
    public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetOperations (CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Operations);
    }
}