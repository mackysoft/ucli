using System;
using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Emits daemon control logs to Unity console and daemon in-memory stream. </summary>
    internal sealed class DaemonLogger : IDaemonLogger
    {
        private readonly IDaemonLogStream daemonLogStream;

        private readonly UnityMainThreadDaemonConsoleLogSink consoleLogSink;

        /// <summary> Initializes a new instance of the <see cref="DaemonLogger" /> class. </summary>
        /// <param name="daemonLogStream"> The daemon-log stream dependency. </param>
        /// <param name="consoleLogSink"> The Unity main-thread console sink dependency. </param>
        public DaemonLogger (
            IDaemonLogStream daemonLogStream,
            UnityMainThreadDaemonConsoleLogSink consoleLogSink)
        {
            this.daemonLogStream = daemonLogStream ?? throw new ArgumentNullException(nameof(daemonLogStream));
            this.consoleLogSink = consoleLogSink ?? throw new ArgumentNullException(nameof(consoleLogSink));
        }

        /// <inheritdoc />
        public void Info (
            string category,
            string message,
            string raw = null)
        {
            Write(IpcLogLevel.Info, category, message, raw);
            consoleLogSink.Info(FormatMessage(category, message));
        }

        /// <inheritdoc />
        public void Warning (
            string category,
            string message,
            string raw = null)
        {
            Write(IpcLogLevel.Warning, category, message, raw);
            consoleLogSink.Warning(FormatMessage(category, message));
        }

        /// <inheritdoc />
        public void Error (
            string category,
            string message,
            string raw = null)
        {
            Write(IpcLogLevel.Error, category, message, raw);
            consoleLogSink.Error(FormatMessage(category, message));
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

            Write(IpcLogLevel.Error, category, message, exception.ToString());
            consoleLogSink.Error(FormatExceptionMessage(category, message, exception));
        }

        /// <summary> Writes one daemon log event to in-memory stream with normalized values. </summary>
        /// <param name="level"> The normalized level value. </param>
        /// <param name="category"> The event category. </param>
        /// <param name="message"> The user-facing message. </param>
        /// <param name="raw"> The optional raw detail payload. </param>
        private void Write (
            IpcLogLevel level,
            string category,
            string message,
            string raw)
        {
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

    }

    /// <summary> Dispatches Unity Console calls exclusively through the captured Unity main thread. </summary>
    internal sealed class UnityMainThreadDaemonConsoleLogSink
    {
        private readonly SynchronizationContext unitySynchronizationContext;

        private readonly int unityMainThreadId;

        private readonly Action<string> logInfo;

        private readonly Action<string> logWarning;

        private readonly Action<string> logError;

        /// <summary> Initializes a sink with one explicit main-thread dispatch context and console delegates. </summary>
        /// <param name="unitySynchronizationContext"> The synchronization context captured on Unity's main thread. </param>
        /// <param name="unityMainThreadId"> The managed identifier of the same Unity main thread. </param>
        /// <param name="logInfo"> The Unity Console information delegate. </param>
        /// <param name="logWarning"> The Unity Console warning delegate. </param>
        /// <param name="logError"> The Unity Console error delegate. </param>
        public UnityMainThreadDaemonConsoleLogSink (
            SynchronizationContext unitySynchronizationContext,
            int unityMainThreadId,
            Action<string> logInfo,
            Action<string> logWarning,
            Action<string> logError)
        {
            this.unitySynchronizationContext = unitySynchronizationContext
                ?? throw new ArgumentNullException(nameof(unitySynchronizationContext));
            if (unityMainThreadId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(unityMainThreadId),
                    unityMainThreadId,
                    "Unity main-thread identifier must be greater than zero.");
            }

            this.unityMainThreadId = unityMainThreadId;
            this.logInfo = logInfo ?? throw new ArgumentNullException(nameof(logInfo));
            this.logWarning = logWarning ?? throw new ArgumentNullException(nameof(logWarning));
            this.logError = logError ?? throw new ArgumentNullException(nameof(logError));
        }

        /// <summary> Captures the current Unity main-thread context and Unity Console delegates. </summary>
        /// <returns> A sink owned by the current Unity main thread. </returns>
        /// <exception cref="InvalidOperationException"> Thrown when no synchronization context is active. </exception>
        public static UnityMainThreadDaemonConsoleLogSink CaptureCurrent ()
        {
            var unitySynchronizationContext = UnityMainThreadGuard.CaptureSynchronizationContext(
                "Daemon console logging initialization");
            return new UnityMainThreadDaemonConsoleLogSink(
                unitySynchronizationContext,
                Thread.CurrentThread.ManagedThreadId,
                static message => Debug.Log(message),
                static message => Debug.LogWarning(message),
                static message => Debug.LogError(message));
        }

        public void Info (string message)
        {
            Emit(logInfo, message);
        }

        public void Warning (string message)
        {
            Emit(logWarning, message);
        }

        public void Error (string message)
        {
            Emit(logError, message);
        }

        private void Emit (Action<string> emit, string message)
        {
            if (Thread.CurrentThread.ManagedThreadId == unityMainThreadId)
            {
                TryEmit(emit, message);
                return;
            }

            try
            {
                unitySynchronizationContext.Post(
                    static state =>
                    {
                        var emission = (ConsoleEmission)state;
                        if (Thread.CurrentThread.ManagedThreadId == emission.UnityMainThreadId)
                        {
                            TryEmit(emission.Emit, emission.Message);
                        }
                    },
                    new ConsoleEmission(unityMainThreadId, emit, message));
            }
            catch
            {
                // The daemon log event is already retained in memory. Console delivery is best-effort
                // when Unity is tearing down its synchronization context.
            }
        }

        private static void TryEmit (Action<string> emit, string message)
        {
            try
            {
                emit(message);
            }
            catch
            {
                // Console logging must not affect IPC request or listener lifetime.
            }
        }

        private sealed class ConsoleEmission
        {
            public ConsoleEmission (
                int unityMainThreadId,
                Action<string> emit,
                string message)
            {
                UnityMainThreadId = unityMainThreadId;
                Emit = emit;
                Message = message;
            }

            public int UnityMainThreadId { get; }

            public Action<string> Emit { get; }

            public string Message { get; }
        }
    }
}
