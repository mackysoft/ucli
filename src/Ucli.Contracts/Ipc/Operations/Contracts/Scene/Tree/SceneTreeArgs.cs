using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Scene tree operation arguments.")]
public sealed record SceneTreeArgs
{
    [JsonConstructor]
    public SceneTreeArgs (
        UnityScenePath path,
        int? depth,
        int? limit,
        string? cursor)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        if (depth is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), depth, "Scene tree depth must not be negative.");
        }

        if (limit is < 1 or > BoundedWindowConstants.MaxLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, $"Scene tree limit must be between 1 and {BoundedWindowConstants.MaxLimit}.");
        }

        Depth = depth;
        Limit = limit;
        Cursor = cursor;
    }

    [UcliRequired]
    [UcliDescription("Scene asset path to inspect.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.AssetExists, AssetKind = UcliOperationAssetKind.Scene)]
    public UnityScenePath Path { get; }

    [UcliDescription("Maximum hierarchy depth to include; null means unbounded.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.Range, Min = 0)]
    public int? Depth { get; }

    [UcliDescription("Maximum number of hierarchy nodes to include in the response window.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.Range, Min = 1, Max = BoundedWindowConstants.MaxLimit)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Limit { get; }

    [UcliDescription("Opaque cursor returned by the previous scene tree window.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.Cursor)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cursor { get; }
}
