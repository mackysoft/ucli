using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Asset property set operation arguments.")]
public sealed record AssetSetArgs
{
    [JsonConstructor]
    public AssetSetArgs (
        AssetReferenceArgs target,
        IReadOnlyList<SerializedObjectSetItemArgs> sets)
    {
        Target = target;
        Sets = sets;
    }

    [UcliRequired]
    [UcliDescription("Target asset to modify.")]
    public AssetReferenceArgs Target { get; init; }

    [UcliRequired]
    [UcliDescription("Serialized property assignments.")]
    [UcliMinItems(1)]
    public IReadOnlyList<SerializedObjectSetItemArgs> Sets { get; init; }
}
