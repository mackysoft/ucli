using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("Asset save operation arguments.")]
public sealed record AssetSaveArgs
{
    [JsonConstructor]
    public AssetSaveArgs (AssetReferenceArgs target)
    {
        Target = ContractArgumentGuard.RequireNotNull(target, nameof(target));
    }

    [JsonInclude]
    [JsonRequired]
    [Description("Target asset or ProjectSettings asset to save.")]
    [UcliReferenceResolvable(UcliOperationReferenceTargetKind.Asset)]
    public AssetReferenceArgs Target { get; private init; }
}
