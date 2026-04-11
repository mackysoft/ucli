using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Assets.Access;

/// <summary> Represents internal access metadata produced by asset lookup reads. </summary>
internal sealed record AssetLookupAccessInfo (
    bool Used,
    bool Hit,
    AssetLookupSource Source,
    IndexFreshness Freshness,
    DateTimeOffset? GeneratedAtUtc,
    string? FallbackReason);