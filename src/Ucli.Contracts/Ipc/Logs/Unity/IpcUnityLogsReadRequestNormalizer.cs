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

        if (!TryResolveFilters(request, out var filters, out errorMessage))
        {
            return false;
        }

        normalizedRequest = new IpcUnityLogsReadRequest(
            Tail: request.Tail,
            After: request.After,
            Since: request.Since,
            Until: request.Until,
            Level: filters.Level,
            Query: StringValueNormalizer.TrimToNull(request.Query),
            QueryTarget: filters.QueryTarget,
            Source: filters.Source,
            StackTrace: filters.StackTraceMode,
            StackTraceMaxFrames: filters.StackTraceMaxFrames,
            StackTraceMaxChars: filters.StackTraceMaxChars);
        errorMessage = null;
        return true;
    }

    private static bool TryResolveFilters (
        IpcUnityLogsReadRequest request,
        out Filters filters,
        out string? errorMessage)
    {
        filters = default;
        if (!TryResolveTextFilters(request, out var textFilters, out errorMessage))
        {
            return false;
        }

        if (!TryResolveStackTrace(request, textFilters.StackTraceMode, out var stackTrace, out errorMessage))
        {
            return false;
        }

        filters = new Filters(textFilters, stackTrace);
        return true;
    }

    private static bool TryResolveTextFilters (
        IpcUnityLogsReadRequest request,
        out TextFilters filters,
        out string? errorMessage)
    {
        filters = default;
        if (!TryResolveLevel(request.Level, out var level, out errorMessage)
            || !IpcDaemonLogsQueryTargetCodec.TryParseForUnityLogs(request.QueryTarget, out var queryTarget, out errorMessage)
            || !TryResolveSource(request.Source, out var source, out errorMessage)
            || !TryResolveStackTraceMode(request.StackTrace, out var stackTraceMode, out errorMessage))
        {
            return false;
        }

        filters = new TextFilters(level, queryTarget, source, stackTraceMode);
        return true;
    }

    private static bool TryResolveStackTrace (
        IpcUnityLogsReadRequest request,
        string stackTraceMode,
        out StackTraceOptions options,
        out string? errorMessage)
    {
        options = default;
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

        options = new StackTraceOptions(maxFrames, maxChars);
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

    private readonly struct TextFilters
    {
        public TextFilters (
            string level,
            string queryTarget,
            string source,
            string stackTraceMode)
        {
            Level = level;
            QueryTarget = queryTarget;
            Source = source;
            StackTraceMode = stackTraceMode;
        }

        public string Level { get; }

        public string QueryTarget { get; }

        public string Source { get; }

        public string StackTraceMode { get; }
    }

    private readonly struct StackTraceOptions
    {
        public StackTraceOptions (
            int? maxFrames,
            int? maxChars)
        {
            MaxFrames = maxFrames;
            MaxChars = maxChars;
        }

        public int? MaxFrames { get; }

        public int? MaxChars { get; }
    }

    private readonly struct Filters
    {
        public Filters (
            TextFilters textFilters,
            StackTraceOptions stackTraceOptions)
        {
            Level = textFilters.Level;
            QueryTarget = textFilters.QueryTarget;
            Source = textFilters.Source;
            StackTraceMode = textFilters.StackTraceMode;
            StackTraceMaxFrames = stackTraceOptions.MaxFrames;
            StackTraceMaxChars = stackTraceOptions.MaxChars;
        }

        public string Level { get; }

        public string QueryTarget { get; }

        public string Source { get; }

        public string StackTraceMode { get; }

        public int? StackTraceMaxFrames { get; }

        public int? StackTraceMaxChars { get; }
    }
}
