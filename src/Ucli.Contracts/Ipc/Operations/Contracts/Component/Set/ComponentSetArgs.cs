using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

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
