using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("Component property set operation arguments.")]
public sealed record ComponentSetArgs
{
    [JsonConstructor]
    public ComponentSetArgs (
        ComponentReferenceArgs target,
        IReadOnlyList<SerializedObjectSetItemArgs> sets)
    {
        Target = ContractArgumentGuard.RequireNotNull(target, nameof(target));
        Sets = ContractArgumentGuard.RequireItems(sets, nameof(sets));
    }

    [JsonInclude]
    [JsonRequired]
    [Description("Target component to modify.")]
    [UcliReferenceResolvable(UcliOperationReferenceTargetKind.Component)]
    public ComponentReferenceArgs Target { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("Serialized property assignments.")]
    [ItemCount(1, int.MaxValue)]
    [UcliSerializedProperty(UcliOperationSerializedPropertyAccess.Write)]
    public IReadOnlyList<SerializedObjectSetItemArgs> Sets { get; private init; }
}
