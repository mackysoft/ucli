using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Applies stack-trace suppression and truncation rules for Unity-log reads. </summary>
    internal static class UnityLogsStackTraceFormatter
    {
        /// <summary> Resolves one effective stack trace for response and query evaluation. </summary>
        /// <param name="stackTrace"> The stored stack trace. </param>
        /// <param name="eventLevel"> The event level. </param>
        /// <param name="stackTraceMode"> The requested stack-trace mode. </param>
        /// <param name="maxFrames"> The optional maximum number of frames. </param>
        /// <param name="maxChars"> The optional maximum number of characters. </param>
        /// <returns> The effective stack trace, or <see langword="null" /> when suppressed or empty. </returns>
        public static string? Format (
            string? stackTrace,
            string eventLevel,
            string stackTraceMode,
            int? maxFrames,
            int? maxChars)
        {
            if (!StringValueNormalizer.TryTrimToNonEmpty(stackTrace, out var normalizedStackTrace))
            {
                return null;
            }

            if (string.Equals(stackTraceMode, MackySoft.Ucli.Contracts.Ipc.IpcUnityLogsStackTraceModeCodec.None, System.StringComparison.Ordinal))
            {
                return null;
            }

            if (string.Equals(stackTraceMode, IpcUnityLogsStackTraceModeCodec.Error, System.StringComparison.Ordinal)
                && !string.Equals(eventLevel, IpcDaemonLogsLevelCodec.Error, System.StringComparison.Ordinal))
            {
                return null;
            }

            var formatted = normalizedStackTrace;
            if (maxFrames.HasValue)
            {
                formatted = LimitFrames(formatted, maxFrames.Value);
            }

            if (maxChars.HasValue && formatted.Length > maxChars.Value)
            {
                formatted = formatted.Substring(0, maxChars.Value);
            }

            return formatted;
        }

        private static string LimitFrames (
            string stackTrace,
            int maxFrames)
        {
            var normalized = stackTrace
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');
            var frameCount = 0;
            for (var i = 0; i < normalized.Length; i++)
            {
                if (normalized[i] != '\n')
                {
                    continue;
                }

                frameCount++;
                if (frameCount >= maxFrames)
                {
                    return normalized.Substring(0, i);
                }
            }

            return normalized;
        }
    }
}
