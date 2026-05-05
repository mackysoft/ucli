using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Projects application read-index metadata into the public CLI JSON payload shape. </summary>
internal static class ReadIndexInfoPayloadProjector
{
    /// <summary> Creates the public <c>payload.readIndex</c> object. </summary>
    /// <param name="readIndex"> The application read-index metadata. </param>
    /// <returns> The JSON-serializable read-index payload. </returns>
    public static object Create (ReadIndexInfo readIndex)
    {
        ArgumentNullException.ThrowIfNull(readIndex);

        return new
        {
            used = readIndex.Used,
            hit = readIndex.Hit,
            source = ToSourceText(readIndex.Source),
            freshness = ToFreshnessText(readIndex.Freshness),
            generatedAtUtc = readIndex.GeneratedAtUtc,
            fallbackReason = readIndex.FallbackReason,
        };
    }

    private static string ToSourceText (ReadIndexInfoSource source)
    {
        return source switch
        {
            ReadIndexInfoSource.Index => "index",
            ReadIndexInfoSource.Unity => "unity",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unsupported read-index source."),
        };
    }

    private static string ToFreshnessText (IndexFreshness freshness)
    {
        return freshness switch
        {
            IndexFreshness.Fresh => "fresh",
            IndexFreshness.Probable => "probable",
            IndexFreshness.Stale => "stale",
            _ => throw new ArgumentOutOfRangeException(nameof(freshness), freshness, "Unsupported read-index freshness."),
        };
    }
}
