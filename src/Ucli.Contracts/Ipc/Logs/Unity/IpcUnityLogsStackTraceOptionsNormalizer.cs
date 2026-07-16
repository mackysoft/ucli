namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Normalizes Unity log stack-trace detail limits. </summary>
internal static class IpcUnityLogsStackTraceOptionsNormalizer
{
    public static bool TryNormalize (
        IpcUnityLogStackTraceMode stackTraceMode,
        int? rawMaxFrames,
        int? rawMaxChars,
        out int? stackTraceMaxFrames,
        out int? stackTraceMaxChars,
        out string? errorMessage)
    {
        if (stackTraceMode == IpcUnityLogStackTraceMode.None)
        {
            stackTraceMaxFrames = null;
            stackTraceMaxChars = null;
            errorMessage = null;
            return true;
        }

        stackTraceMaxFrames = rawMaxFrames;
        stackTraceMaxChars = rawMaxChars;
        return TryValidateFrameLimit(stackTraceMaxFrames, out errorMessage)
            && TryValidateCharLimit(stackTraceMaxChars, out errorMessage);
    }

    private static bool TryValidateFrameLimit (
        int? stackTraceMaxFrames,
        out string? errorMessage)
    {
        if (stackTraceMaxFrames.HasValue
            && (stackTraceMaxFrames.Value < IpcUnityLogsReadRequestNormalizer.MinimumStackTraceMaxFrames
                || stackTraceMaxFrames.Value > IpcUnityLogsReadRequestNormalizer.MaximumStackTraceMaxFrames))
        {
            errorMessage =
                $"stackTraceMaxFrames must be between {IpcUnityLogsReadRequestNormalizer.MinimumStackTraceMaxFrames} and {IpcUnityLogsReadRequestNormalizer.MaximumStackTraceMaxFrames}. Actual: {stackTraceMaxFrames.Value}.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    private static bool TryValidateCharLimit (
        int? stackTraceMaxChars,
        out string? errorMessage)
    {
        if (stackTraceMaxChars.HasValue
            && (stackTraceMaxChars.Value < IpcUnityLogsReadRequestNormalizer.MinimumStackTraceMaxChars
                || stackTraceMaxChars.Value > IpcUnityLogsReadRequestNormalizer.MaximumStackTraceMaxChars))
        {
            errorMessage =
                $"stackTraceMaxChars must be between {IpcUnityLogsReadRequestNormalizer.MinimumStackTraceMaxChars} and {IpcUnityLogsReadRequestNormalizer.MaximumStackTraceMaxChars}. Actual: {stackTraceMaxChars.Value}.";
            return false;
        }

        errorMessage = null;
        return true;
    }
}
