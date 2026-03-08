using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Ops.Access;

namespace MackySoft.Ucli.Ops.Mapping;

/// <summary> Maps internal access metadata into command-facing <c>payload.readIndex</c> output. </summary>
internal sealed class OpsReadIndexInfoMapper
{
    private const string SourceIndex = "index";

    private const string SourceUnity = "unity";

    private const string FreshnessFresh = "fresh";

    private const string FreshnessProbable = "probable";

    private const string FreshnessStale = "stale";

    /// <summary> Maps one internal access-info value into command-facing read-index output. </summary>
    /// <param name="accessInfo"> The internal access metadata. </param>
    /// <returns> The mapped read-index output. </returns>
    public OpsReadIndexInfo Map (OpsCatalogAccessInfo accessInfo)
    {
        ArgumentNullException.ThrowIfNull(accessInfo);

        return new OpsReadIndexInfo(
            Used: accessInfo.Used,
            Hit: accessInfo.Hit,
            Source: MapSource(accessInfo.Source),
            Freshness: MapFreshness(accessInfo.Freshness),
            GeneratedAtUtc: accessInfo.GeneratedAtUtc,
            FallbackReason: accessInfo.FallbackReason);
    }

    private static string MapSource (OpsCatalogSource source)
    {
        return source switch
        {
            OpsCatalogSource.Index => SourceIndex,
            OpsCatalogSource.Source => SourceUnity,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unsupported ops catalog source."),
        };
    }

    private static string MapFreshness (IndexFreshness freshness)
    {
        return freshness switch
        {
            IndexFreshness.Fresh => FreshnessFresh,
            IndexFreshness.Probable => FreshnessProbable,
            IndexFreshness.Stale => FreshnessStale,
            _ => throw new ArgumentOutOfRangeException(nameof(freshness), freshness, "Unsupported index freshness."),
        };
    }
}