using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Validates and normalizes common filter values used by <c>unity.logs.read</c>. </summary>
public static class IpcUnityLogsReadRequestNormalizer
{
    /// <summary> Gets the minimum allowed stack-trace frame limit. </summary>
    public const int MinimumStackTraceMaxFrames = 1;

    /// <summary> Gets the maximum allowed stack-trace frame limit. </summary>
    public const int MaximumStackTraceMaxFrames = 512;

    /// <summary> Gets the minimum allowed stack-trace character limit. </summary>
    public const int MinimumStackTraceMaxChars = 256;

    /// <summary> Gets the maximum allowed stack-trace character limit. </summary>
    public const int MaximumStackTraceMaxChars = 131072;

    /// <summary> Tries to normalize one Unity-log read request payload. </summary>
    /// <param name="request"> The raw request payload. </param>
    /// <param name="normalizedRequest"> The normalized payload when validation succeeds. </param>
    /// <param name="sinceTimestamp"> The parsed inclusive lower timestamp bound. </param>
    /// <param name="untilTimestamp"> The parsed inclusive upper timestamp bound. </param>
    /// <param name="errorMessage"> The validation error message when validation fails. </param>
    /// <returns> <see langword="true" /> when validation succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryNormalize (
        IpcUnityLogsReadRequest request,
        out IpcUnityLogsReadRequest? normalizedRequest,
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

        if (!IpcDaemonLogsQueryTargetCodec.TryParseForUnityLogs(
                request.QueryTarget,
                out var queryTarget,
                out errorMessage))
        {
            return false;
        }

        if (!TryResolveSource(request.Source, out var source, out errorMessage))
        {
            return false;
        }

        if (!TryResolveStackTraceMode(request.StackTrace, out var stackTraceMode, out errorMessage))
        {
            return false;
        }

        var stackTraceMaxFrames = request.StackTraceMaxFrames;
        var stackTraceMaxChars = request.StackTraceMaxChars;
        if (string.Equals(stackTraceMode, IpcUnityLogsStackTraceModeCodec.None, StringComparison.Ordinal))
        {
            stackTraceMaxFrames = null;
            stackTraceMaxChars = null;
        }
        else
        {
            if (stackTraceMaxFrames.HasValue
                && (stackTraceMaxFrames.Value < MinimumStackTraceMaxFrames
                    || stackTraceMaxFrames.Value > MaximumStackTraceMaxFrames))
            {
                errorMessage =
                    $"stackTraceMaxFrames must be between {MinimumStackTraceMaxFrames} and {MaximumStackTraceMaxFrames}. Actual: {stackTraceMaxFrames.Value}.";
                return false;
            }

            if (stackTraceMaxChars.HasValue
                && (stackTraceMaxChars.Value < MinimumStackTraceMaxChars
                    || stackTraceMaxChars.Value > MaximumStackTraceMaxChars))
            {
                errorMessage =
                    $"stackTraceMaxChars must be between {MinimumStackTraceMaxChars} and {MaximumStackTraceMaxChars}. Actual: {stackTraceMaxChars.Value}.";
                return false;
            }
        }

        normalizedRequest = new IpcUnityLogsReadRequest(
            Tail: request.Tail,
            After: request.After,
            Since: request.Since,
            Until: request.Until,
            Level: level,
            Query: StringValueNormalizer.TrimToNull(request.Query),
            QueryTarget: queryTarget,
            Source: source,
            StackTrace: stackTraceMode,
            StackTraceMaxFrames: stackTraceMaxFrames,
            StackTraceMaxChars: stackTraceMaxChars);
        errorMessage = null;
        return true;
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

    private static bool TryResolveSource (
        string? value,
        out string source,
        out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            source = IpcUnityLogsSourceCodec.All;
            errorMessage = null;
            return true;
        }

        if (IpcUnityLogsSourceCodec.TryParse(value, out var normalizedSource))
        {
            source = normalizedSource!;
            errorMessage = null;
            return true;
        }

        source = string.Empty;
        errorMessage =
            $"source must be one of: {IpcUnityLogsSourceCodec.Compile}, {IpcUnityLogsSourceCodec.Runtime}, {IpcUnityLogsSourceCodec.All}. Actual: {value}.";
        return false;
    }

    private static bool TryResolveStackTraceMode (
        string? value,
        out string stackTraceMode,
        out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            stackTraceMode = IpcUnityLogsStackTraceModeCodec.Error;
            errorMessage = null;
            return true;
        }

        if (IpcUnityLogsStackTraceModeCodec.TryParse(value, out var normalizedStackTraceMode))
        {
            stackTraceMode = normalizedStackTraceMode!;
            errorMessage = null;
            return true;
        }

        stackTraceMode = string.Empty;
        errorMessage =
            $"stackTrace must be one of: {IpcUnityLogsStackTraceModeCodec.None}, {IpcUnityLogsStackTraceModeCodec.Error}, {IpcUnityLogsStackTraceModeCodec.All}. Actual: {value}.";
        return false;
    }
}