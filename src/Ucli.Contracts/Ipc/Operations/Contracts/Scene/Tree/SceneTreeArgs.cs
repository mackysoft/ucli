using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Scene tree operation arguments.")]
public sealed record SceneTreeArgs
{
    [JsonConstructor]
    public SceneTreeArgs (
        SceneAssetPath path,
        int? depth)
    {
        Path = path;
        Depth = depth;
    }

    public SceneTreeArgs (
        string path,
        int? depth)
        : this(new SceneAssetPath(path), depth)
    {
    }

    [UcliRequired]
    [UcliDescription("Scene asset path to inspect.")]
    public SceneAssetPath Path { get; init; }

    [UcliDescription("Maximum hierarchy depth to include; null means unbounded.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.Range, Min = 0)]
    public int? Depth { get; init; }
}
