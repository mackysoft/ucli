using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Scene tree operation arguments.")]
public sealed record SceneTreeArgs
{
    [JsonConstructor]
    public SceneTreeArgs (
        string path,
        int? depth)
    {
        Path = path;
        Depth = depth;
    }

    [UcliRequired]
    [UcliDescription("Scene asset path to inspect.")]
    [UcliMinLength(1)]
    public string Path { get; init; }

    [UcliDescription("Maximum hierarchy depth to include; null means unbounded.")]
    [UcliMinimum(0)]
    public int? Depth { get; init; }
}
