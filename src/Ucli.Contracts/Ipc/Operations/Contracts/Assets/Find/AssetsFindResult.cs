using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Assets find operation result.")]
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

    [UcliRequired]
    [UcliDescription("Matched assets in ordinal asset path order.")]
    public IReadOnlyList<AssetsFindMatch> Matches { get; }

    [UcliRequired]
    [UcliDescription("Bounded result window metadata.")]
    public BoundedWindow Window { get; }
}
