using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Scene tree operation arguments.")]
public sealed record SceneTreeArgs
{
    [JsonConstructor]
    public SceneTreeArgs (
        SceneAssetPath path,
        int? depth,
        int? limit,
        string? cursor)
    {
        Path = path;
        Depth = depth;
        Limit = limit;
        Cursor = cursor;
    }

    [UcliRequired]
    [UcliDescription("Scene asset path to inspect.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.AssetExists, AssetKind = UcliOperationAssetKind.Scene)]
    public SceneAssetPath Path { get; init; }

    [UcliDescription("Maximum hierarchy depth to include; null means unbounded.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.Range, Min = 0)]
    public int? Depth { get; init; }

    [UcliDescription("Maximum number of hierarchy nodes to include in the response window.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.Range, Min = 1, Max = BoundedWindowConstants.MaxLimit)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Limit { get; init; }

    [UcliDescription("Opaque cursor returned by the previous scene tree window.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.Cursor)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cursor { get; init; }
}
