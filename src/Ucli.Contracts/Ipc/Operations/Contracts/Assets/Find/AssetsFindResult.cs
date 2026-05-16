using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Assets find operation result.")]
public sealed record AssetsFindResult
{
    [JsonConstructor]
    public AssetsFindResult (
        IReadOnlyList<AssetsFindMatch> matches,
        BoundedWindow window)
    {
        Matches = matches;
        Window = window;
    }

    [UcliRequired]
    [UcliDescription("Matched assets in ordinal asset path order.")]
    public IReadOnlyList<AssetsFindMatch> Matches { get; init; }

    [UcliRequired]
    [UcliDescription("Bounded result window metadata.")]
    public BoundedWindow Window { get; init; }
}
