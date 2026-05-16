using System.Text.Json.Serialization;

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

    public SceneTreeArgs (
        SceneAssetPath path,
        int? depth)
        : this(path, depth, limit: null, cursor: null)
    {
    }

    public SceneTreeArgs (
        string path,
        int? depth)
        : this(path, depth, limit: null, cursor: null)
    {
    }

    public SceneTreeArgs (
        string path,
        int? depth,
        int? limit,
        string? cursor)
        : this(new SceneAssetPath(path), depth, limit, cursor)
    {
    }

    [UcliRequired]
    [UcliDescription("Scene asset path to inspect.")]
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
