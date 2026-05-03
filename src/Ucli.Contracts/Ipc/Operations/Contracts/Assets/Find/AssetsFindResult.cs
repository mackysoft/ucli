using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Assets find operation result.")]
public sealed record AssetsFindResult
{
    [JsonConstructor]
    public AssetsFindResult (IReadOnlyList<AssetsFindMatch> matches)
    {
        Matches = matches;
    }

    [UcliRequired]
    [UcliDescription("Matched assets in ordinal asset path order.")]
    public IReadOnlyList<AssetsFindMatch> Matches { get; init; }
}
