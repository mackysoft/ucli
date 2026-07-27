using System.Text.Json.Serialization;

using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("Assets find operation result.")]
public sealed record AssetsFindResult
{
    /// <summary> Initializes an asset search result with an owned snapshot of the matches. </summary>
    /// <param name="matches"> The non-null matches, none of which may be null. </param>
    /// <param name="window"> The non-null window metadata for the matches. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="matches" /> or <paramref name="window" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="matches" /> contains a <see langword="null" /> item. </exception>
    [JsonConstructor]
    public AssetsFindResult (
        IReadOnlyList<AssetsFindMatch> matches,
        BoundedWindow window)
    {
        Matches = ContractArgumentGuard.RequireItems(matches, nameof(matches));
        Window = window ?? throw new ArgumentNullException(nameof(window));
    }

    [JsonInclude]
    [JsonRequired]
    [Description("Matched assets in ordinal asset path order.")]
    public IReadOnlyList<AssetsFindMatch> Matches { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("Bounded result window metadata.")]
    public BoundedWindow Window { get; private init; }
}
