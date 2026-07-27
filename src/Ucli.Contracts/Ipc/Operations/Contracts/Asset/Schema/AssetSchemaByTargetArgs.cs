using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Selects an asset schema from an existing asset target. </summary>
public sealed record AssetSchemaByTargetArgs : AssetSchemaArgs
{
    /// <summary> Initializes a new target-based asset-schema selection. </summary>
    /// <param name="target"> The existing asset target to inspect. </param>
    [JsonConstructor]
    public AssetSchemaByTargetArgs (AssetReferenceArgs target)
    {
        Target = ContractArgumentGuard.RequireNotNull(target, nameof(target));
    }

    /// <summary> Gets the existing asset target to inspect. </summary>
    [JsonInclude]
    [JsonRequired]
    [Description("Existing asset target to inspect.")]
    [UcliReferenceResolvable(UcliOperationReferenceTargetKind.Asset)]
    public AssetReferenceArgs Target { get; private init; }
}
