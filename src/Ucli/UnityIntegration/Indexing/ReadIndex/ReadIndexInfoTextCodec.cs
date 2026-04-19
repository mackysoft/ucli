using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;

/// <summary> Maps read-index contract literals into command-facing string values. </summary>
internal static class ReadIndexInfoTextCodec
{
    public const string SourceIndex = "index";

    public const string SourceUnity = "unity";

    public const string FreshnessFresh = "fresh";

    public const string FreshnessProbable = "probable";

    public const string FreshnessStale = "stale";

    public static string MapFreshness (IndexFreshness freshness)
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