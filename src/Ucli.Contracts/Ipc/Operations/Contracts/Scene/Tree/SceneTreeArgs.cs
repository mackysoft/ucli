using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("Scene tree operation arguments.")]
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

    [JsonInclude]
    [JsonRequired]
    [Description("Scene asset path to inspect.")]
    [UcliAssetExists(UcliOperationAssetKind.Scene)]
    public UnityScenePath Path { get; private init; }

    [Description("Maximum hierarchy depth to include; null means unbounded.")]
    [UcliInt32Minimum(0)]
    public int? Depth { get; }

    [Description("Maximum number of hierarchy nodes to include in the response window.")]
    [UcliInt32Range(1, BoundedWindowConstants.MaxLimit)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Limit { get; }

    [Description("Opaque cursor returned by the previous scene tree window.")]
    [UcliCursor]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cursor { get; }
}
