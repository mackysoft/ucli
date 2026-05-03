using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Single path operation arguments.")]
public sealed record PathArgs
{
    [JsonConstructor]
    public PathArgs (string path)
    {
        Path = path;
    }

    [UcliRequired]
    [UcliDescription("Unity project relative asset path.")]
    public string Path { get; init; }
}
