using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;

/// <summary> Parses user-facing log filter spellings into typed IPC contract values. </summary>
internal static class LogsRequestContractLiteralParser
{
    private const string AllFilterLiteral = "all";

    public static bool TryParseLevel (
        string? value,
        out IpcLogLevel? level,
        out string? errorMessage)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(value, out var normalizedValue)
            || string.Equals(normalizedValue, AllFilterLiteral, StringComparison.OrdinalIgnoreCase))
        {
            level = null;
            errorMessage = null;
            return true;
        }

        if (VocabularyInputParser.TryParseIgnoreCase(normalizedValue, out IpcLogLevel parsedLevel))
        {
            level = parsedLevel;
            errorMessage = null;
            return true;
        }

        level = null;
        errorMessage =
            $"level must be one of: {AllFilterLiteral}, {TextVocabulary.GetText(IpcLogLevel.Error)}, "
            + $"{TextVocabulary.GetText(IpcLogLevel.Warning)}, {TextVocabulary.GetText(IpcLogLevel.Info)}. Actual: {value}.";
        return false;
    }

    public static bool TryParseQueryTarget (
        string? value,
        out IpcLogQueryTarget? queryTarget,
        out string? errorMessage)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(value, out var normalizedValue))
        {
            queryTarget = null;
            errorMessage = null;
            return true;
        }

        if (VocabularyInputParser.TryParseIgnoreCase(normalizedValue, out IpcLogQueryTarget parsedQueryTarget))
        {
            queryTarget = parsedQueryTarget;
            errorMessage = null;
            return true;
        }

        queryTarget = null;
        errorMessage =
            $"queryTarget must be one of: {TextVocabulary.GetText(IpcLogQueryTarget.Message)}, "
            + $"{TextVocabulary.GetText(IpcLogQueryTarget.Stack)}, {TextVocabulary.GetText(IpcLogQueryTarget.Both)}. Actual: {value}.";
        return false;
    }

    public static bool TryParseSource (
        string? value,
        out IpcUnityLogSource? source,
        out string? errorMessage)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(value, out var normalizedValue)
            || string.Equals(normalizedValue, AllFilterLiteral, StringComparison.OrdinalIgnoreCase))
        {
            source = null;
            errorMessage = null;
            return true;
        }

        if (VocabularyInputParser.TryParseIgnoreCase(normalizedValue, out IpcUnityLogSource parsedSource))
        {
            source = parsedSource;
            errorMessage = null;
            return true;
        }

        source = null;
        errorMessage =
            $"source must be one of: {TextVocabulary.GetText(IpcUnityLogSource.Compile)}, "
            + $"{TextVocabulary.GetText(IpcUnityLogSource.Runtime)}, {AllFilterLiteral}. Actual: {value}.";
        return false;
    }

    public static string? NormalizeCategory (string? value)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(value, out var normalizedValue)
            || string.Equals(normalizedValue, AllFilterLiteral, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return normalizedValue;
    }

    public static bool TryParseStackTraceMode (
        string? value,
        out IpcUnityLogStackTraceMode? stackTraceMode,
        out string? errorMessage)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(value, out var normalizedValue))
        {
            stackTraceMode = null;
            errorMessage = null;
            return true;
        }

        if (VocabularyInputParser.TryParseIgnoreCase(normalizedValue, out IpcUnityLogStackTraceMode parsedMode))
        {
            stackTraceMode = parsedMode;
            errorMessage = null;
            return true;
        }

        stackTraceMode = null;
        errorMessage =
            $"stackTrace must be one of: {TextVocabulary.GetText(IpcUnityLogStackTraceMode.None)}, "
            + $"{TextVocabulary.GetText(IpcUnityLogStackTraceMode.Error)}, {TextVocabulary.GetText(IpcUnityLogStackTraceMode.All)}. Actual: {value}.";
        return false;
    }
}
