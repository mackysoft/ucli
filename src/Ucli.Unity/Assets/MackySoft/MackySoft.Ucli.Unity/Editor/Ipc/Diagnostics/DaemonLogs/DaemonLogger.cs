using System;
using MackySoft.Ucli.Contracts.Ipc;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Emits daemon control logs to Unity console and daemon in-memory stream. </summary>
    internal sealed class DaemonLogger : IDaemonLogger
    {
        private readonly IDaemonLogStream daemonLogStream;

        private readonly Action<string> logInfo;

        private readonly Action<string> logWarning;

        private readonly Action<string> logError;

        /// <summary> Initializes a new instance of the <see cref="DaemonLogger" /> class. </summary>
        /// <param name="daemonLogStream"> The daemon-log stream dependency. </param>
        public DaemonLogger (IDaemonLogStream daemonLogStream)
            : this(daemonLogStream, LogInfoToUnityConsole, LogWarningToUnityConsole, LogErrorToUnityConsole)
        {
        }

        internal DaemonLogger (
            IDaemonLogStream daemonLogStream,
            Action<string> logInfo,
            Action<string> logWarning,
            Action<string> logError)
        {
            this.daemonLogStream = daemonLogStream ?? throw new ArgumentNullException(nameof(daemonLogStream));
            this.logInfo = logInfo ?? throw new ArgumentNullException(nameof(logInfo));
            this.logWarning = logWarning ?? throw new ArgumentNullException(nameof(logWarning));
            this.logError = logError ?? throw new ArgumentNullException(nameof(logError));
        }

        /// <inheritdoc />
        public void Info (
            string category,
            string message,
            string raw = null)
        {
            WriteAndEmit(IpcDaemonLogsLevelCodec.Info, category, message, raw);
            logInfo(FormatMessage(category, message));
        }

        /// <inheritdoc />
        public void Warning (
            string category,
            string message,
            string raw = null)
        {
            WriteAndEmit(IpcDaemonLogsLevelCodec.Warning, category, message, raw);
            logWarning(FormatMessage(category, message));
        }

        /// <inheritdoc />
        public void Error (
            string category,
            string message,
            string raw = null)
        {
            WriteAndEmit(IpcDaemonLogsLevelCodec.Error, category, message, raw);
            logError(FormatMessage(category, message));
        }

        /// <inheritdoc />
        public void Exception (
            string category,
            string message,
            Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            WriteAndEmit(IpcDaemonLogsLevelCodec.Error, category, message, exception.ToString());
            logError(FormatExceptionMessage(category, message, exception));
        }

        /// <summary> Writes one daemon log event to in-memory stream with normalized values. </summary>
        /// <param name="level"> The normalized level value. </param>
        /// <param name="category"> The event category. </param>
        /// <param name="message"> The user-facing message. </param>
        /// <param name="raw"> The optional raw detail payload. </param>
        private void WriteAndEmit (
            string level,
            string category,
            string message,
            string raw)
        {
            if (string.IsNullOrWhiteSpace(level))
            {
                throw new ArgumentException("level must not be empty.", nameof(level));
            }

            if (string.IsNullOrWhiteSpace(category))
            {
                throw new ArgumentException("category must not be empty.", nameof(category));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("message must not be empty.", nameof(message));
            }

            daemonLogStream.Write(
                category,
                level,
                message,
                raw);
        }

        /// <summary> Formats one Unity console output message. </summary>
        /// <param name="category"> The log category value. </param>
        /// <param name="message"> The log message value. </param>
        /// <returns> The formatted log message text. </returns>
        private static string FormatMessage (
            string category,
            string message)
        {
            return string.Concat("[ucli][", category, "] ", message);
        }

        private static string FormatExceptionMessage (
            string category,
            string message,
            Exception exception)
        {
            return string.Concat(FormatMessage(category, message), Environment.NewLine, exception);
        }

        private static void LogInfoToUnityConsole (string message)
        {
            Debug.Log(message);
        }

        private static void LogWarningToUnityConsole (string message)
        {
            Debug.LogWarning(message);
        }

        private static void LogErrorToUnityConsole (string message)
        {
            Debug.LogError(message);
        }
    }
}
