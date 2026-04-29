using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Validates and normalizes common filter values used by <c>daemon.logs.read</c>. </summary>
public static class IpcDaemonLogsReadRequestNormalizer
{
    /// <summary> Tries to normalize one daemon-log read request payload. </summary>
    /// <param name="request"> The raw request payload. </param>
    /// <param name="normalizedRequest"> The normalized payload when validation succeeds. </param>
    /// <param name="sinceTimestamp"> The parsed inclusive lower timestamp bound. </param>
    /// <param name="untilTimestamp"> The parsed inclusive upper timestamp bound. </param>
    /// <param name="errorMessage"> The validation error message when validation fails. </param>
    /// <returns> <see langword="true" /> when validation succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryNormalize (
        IpcDaemonLogsReadRequest request,
        out IpcDaemonLogsReadRequest? normalizedRequest,
        out DateTimeOffset? sinceTimestamp,
        out DateTimeOffset? untilTimestamp,
        out string? errorMessage)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        normalizedRequest = null;
        if (request.Tail.HasValue && request.Tail.Value <= 0)
        {
            sinceTimestamp = null;
            untilTimestamp = null;
            errorMessage = $"tail must be greater than zero. Actual: {request.Tail.Value}.";
            return false;
        }

        if (!IpcIso8601TimestampCodec.TryParseOptionalWithTimezoneOffset(request.Since, out sinceTimestamp))
        {
            untilTimestamp = null;
            errorMessage = $"since must be an ISO 8601 timestamp with timezone offset. Actual: {request.Since}.";
            return false;
        }

        if (!IpcIso8601TimestampCodec.TryParseOptionalWithTimezoneOffset(request.Until, out untilTimestamp))
        {
            errorMessage = $"until must be an ISO 8601 timestamp with timezone offset. Actual: {request.Until}.";
            return false;
        }

        if (sinceTimestamp.HasValue
            && untilTimestamp.HasValue
            && sinceTimestamp.Value > untilTimestamp.Value)
        {
            errorMessage = $"since must be less than or equal to until. since={request.Since}, until={request.Until}.";
            return false;
        }

        if (!TryResolveLevel(request.Level, out var level, out errorMessage))
        {
            return false;
        }

        if (!IpcDaemonLogsQueryTargetCodec.TryParseForDaemonLogs(
                request.QueryTarget,
                out var queryTarget,
                out errorMessage))
        {
            return false;
        }

        normalizedRequest = new IpcDaemonLogsReadRequest(
            Tail: request.Tail,
            After: request.After,
            Since: request.Since,
            Until: request.Until,
            Level: level,
            Query: StringValueNormalizer.TrimToNull(request.Query),
            QueryTarget: queryTarget,
            Category: NormalizeCategory(request.Category));
        errorMessage = null;
        return true;
    }

    private static string? NormalizeCategory (string? value)
    {
        if (IpcDaemonLogsCategoryCodec.IsAll(value))
        {
            return IpcDaemonLogsCategoryCodec.All;
        }

        return StringValueNormalizer.TrimToNull(value);
    }

    private static bool TryResolveLevel (
        string? value,
        out string level,
        out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            level = IpcDaemonLogsLevelCodec.All;
            errorMessage = null;
            return true;
        }

        if (IpcDaemonLogsLevelCodec.TryParse(value, out var normalizedLevel))
        {
            level = normalizedLevel!;
            errorMessage = null;
            return true;
        }

        level = string.Empty;
        errorMessage =
            $"level must be one of: {IpcDaemonLogsLevelCodec.All}, {IpcDaemonLogsLevelCodec.Error}, {IpcDaemonLogsLevelCodec.Warning}, {IpcDaemonLogsLevelCodec.Info}. Actual: {value}.";
        return false;
    }
}
