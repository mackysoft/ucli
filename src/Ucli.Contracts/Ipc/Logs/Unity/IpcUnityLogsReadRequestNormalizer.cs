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
        if (!IpcLogsReadWindowNormalizer.TryNormalize(
                request.Tail,
                request.Since,
                request.Until,
                out sinceTimestamp,
                out untilTimestamp,
                out errorMessage))
        {
            return false;
        }

        var stackTraceMode = request.StackTrace ?? IpcUnityLogStackTraceMode.Error;
        if (!IpcUnityLogsStackTraceOptionsNormalizer.TryNormalize(
                stackTraceMode,
                request.StackTraceMaxFrames,
                request.StackTraceMaxChars,
                out var maxFrames,
                out var maxChars,
                out errorMessage))
        {
            return false;
        }

        normalizedRequest = new IpcUnityLogsReadRequest(
            Tail: request.Tail,
            After: request.After,
            Since: request.Since,
            Until: request.Until,
            Level: request.Level,
            Query: StringValueNormalizer.TrimToNull(request.Query),
            QueryTarget: request.QueryTarget ?? IpcLogQueryTarget.Message,
            Source: request.Source,
            StackTrace: stackTraceMode,
            StackTraceMaxFrames: maxFrames,
            StackTraceMaxChars: maxChars);
        errorMessage = null;
        return true;
    }
}
