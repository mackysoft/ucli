using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;

/// <summary> Represents one assets.find-style lookup query executed against persisted asset search entries. </summary>
internal sealed record AssetSearchLookupQuery
{
    /// <summary> Initializes a validated asset-search query containing at least one filter. </summary>
    public AssetSearchLookupQuery (
        UnityTypeId? TypeId,
        UnityAssetPathPrefix? PathPrefix,
        string? NameContains)
    {
        if (NameContains is not null)
        {
            if (string.IsNullOrWhiteSpace(NameContains))
            {
                throw new ArgumentException("Asset name filter must not be empty or whitespace.", nameof(NameContains));
            }

            if (StringValueValidator.HasOuterWhitespace(NameContains))
            {
                throw new ArgumentException("Asset name filter must not contain leading or trailing whitespace.", nameof(NameContains));
            }

            if (!StringValueValidator.IsWellFormedUtf16(NameContains))
            {
                throw new ArgumentException("Asset name filter must contain well-formed UTF-16 text.", nameof(NameContains));
            }
        }

        if (TypeId is null && PathPrefix is null && NameContains is null)
        {
            throw new ArgumentException("At least one asset-search filter must be specified.");
        }

        this.TypeId = TypeId;
        this.PathPrefix = PathPrefix;
        this.NameContains = NameContains;
    }

    /// <summary> Gets the optional Unity type filter. </summary>
    public UnityTypeId? TypeId { get; }

    /// <summary> Gets the optional normalized Unity asset path prefix. </summary>
    public UnityAssetPathPrefix? PathPrefix { get; }

    /// <summary> Gets the optional asset-name substring filter. </summary>
    public string? NameContains { get; }
}
