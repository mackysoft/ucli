using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("Asset property set operation arguments.")]
public sealed record AssetSetArgs
{
    [JsonConstructor]
    public AssetSetArgs (
        AssetReferenceArgs target,
        IReadOnlyList<SerializedObjectSetItemArgs> sets)
    {
        Target = ContractArgumentGuard.RequireNotNull(target, nameof(target));
        Sets = ContractArgumentGuard.RequireItems(sets, nameof(sets));
    }

    [JsonInclude]
    [JsonRequired]
    [Description("Target asset to modify.")]
    [UcliReferenceResolvable(UcliOperationReferenceTargetKind.Asset)]
    public AssetReferenceArgs Target { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("Serialized property assignments.")]
    [ItemCount(1, int.MaxValue)]
    [UcliSerializedProperty(UcliOperationSerializedPropertyAccess.Write)]
    public IReadOnlyList<SerializedObjectSetItemArgs> Sets { get; private init; }
}
