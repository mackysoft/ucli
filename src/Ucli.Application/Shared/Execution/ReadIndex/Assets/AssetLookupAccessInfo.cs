namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;

/// <summary> Represents internal access metadata produced by asset lookup reads. </summary>
internal sealed record AssetLookupAccessInfo (
    bool Used,
    bool Hit,
    AssetLookupSource Source,
    IndexFreshness Freshness,
    DateTimeOffset? GeneratedAtUtc,
    string? FallbackReason);
