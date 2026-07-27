using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> References a Unity object through an earlier request step alias. </summary>
[Description("Request-local alias reference.")]
public sealed record UcliAliasReferenceArgs :
    AssetReferenceArgs,
    ComponentReferenceArgs,
    GameObjectReferenceArgs,
    SceneGameObjectReferenceArgs
{
    /// <summary> Initializes a new request-local alias reference. </summary>
    /// <param name="alias"> The alias produced by an earlier request step. </param>
    [JsonConstructor]
    public UcliAliasReferenceArgs (UcliPlanAlias alias)
    {
        Alias = ContractArgumentGuard.RequireNotNull(alias, nameof(alias));
    }

    /// <summary> Gets the alias produced by an earlier request step. </summary>
    [JsonInclude]
    [JsonRequired]
    [JsonPropertyName(UcliOperationContractPropertyNames.Alias)]
    [Description("Request-local alias produced by an earlier plan step.")]
    public UcliPlanAlias Alias { get; private init; }
}
