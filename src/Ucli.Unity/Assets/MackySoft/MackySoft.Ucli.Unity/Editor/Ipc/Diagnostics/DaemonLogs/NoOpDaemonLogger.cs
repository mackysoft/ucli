using System;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Provides no-op fallback for daemon daemon logger dependencies. </summary>
    internal sealed class NoOpDaemonLogger : IDaemonLogger
    {
        /// <summary> Gets shared no-op logger instance. </summary>
        public static NoOpDaemonLogger Instance { get; } = new NoOpDaemonLogger();

        private NoOpDaemonLogger ()
        {
        }

        /// <inheritdoc />
        public void Info (
            string category,
            string message,
            string raw = null)
        {
        }

        /// <inheritdoc />
        public void Warning (
            string category,
            string message,
            string raw = null)
        {
        }

        /// <inheritdoc />
        public void Error (
            string category,
            string message,
            string raw = null)
        {
        }

        /// <inheritdoc />
        public void Exception (
            string category,
            string message,
            Exception exception)
        {
        }
    }
}