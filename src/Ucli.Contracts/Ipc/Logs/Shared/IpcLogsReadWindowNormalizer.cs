namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Normalizes shared log-read tail and timestamp bounds. </summary>
internal static class IpcLogsReadWindowNormalizer
{
    public static bool TryNormalize (
        int? tail,
        string? since,
        string? until,
        out DateTimeOffset? sinceTimestamp,
        out DateTimeOffset? untilTimestamp,
        out string? errorMessage)
    {
        if (tail.HasValue && tail.Value <= 0)
        {
            sinceTimestamp = null;
            untilTimestamp = null;
            errorMessage = $"tail must be greater than zero. Actual: {tail.Value}.";
            return false;
        }

        return TryNormalizeTimestampRange(since, until, out sinceTimestamp, out untilTimestamp, out errorMessage);
    }

    private static bool TryNormalizeTimestampRange (
        string? since,
        string? until,
        out DateTimeOffset? sinceTimestamp,
        out DateTimeOffset? untilTimestamp,
        out string? errorMessage)
    {
        if (!IpcIso8601TimestampCodec.TryParseOptionalWithTimezoneOffset(since, out sinceTimestamp))
        {
            untilTimestamp = null;
            errorMessage = $"since must be an ISO 8601 timestamp with timezone offset. Actual: {since}.";
            return false;
        }

        return TryNormalizeUpperTimestamp(since, until, sinceTimestamp, out untilTimestamp, out errorMessage);
    }

    private static bool TryNormalizeUpperTimestamp (
        string? since,
        string? until,
        DateTimeOffset? sinceTimestamp,
        out DateTimeOffset? untilTimestamp,
        out string? errorMessage)
    {
        if (!IpcIso8601TimestampCodec.TryParseOptionalWithTimezoneOffset(until, out untilTimestamp))
        {
            errorMessage = $"until must be an ISO 8601 timestamp with timezone offset. Actual: {until}.";
            return false;
        }

        return TryValidateTimestampOrder(since, until, sinceTimestamp, untilTimestamp, out errorMessage);
    }

    private static bool TryValidateTimestampOrder (
        string? since,
        string? until,
        DateTimeOffset? sinceTimestamp,
        DateTimeOffset? untilTimestamp,
        out string? errorMessage)
    {
        if (sinceTimestamp.HasValue && untilTimestamp.HasValue && sinceTimestamp.Value > untilTimestamp.Value)
        {
            errorMessage = $"since must be less than or equal to until. since={since}, until={until}.";
            return false;
        }

        errorMessage = null;
        return true;
    }
}
