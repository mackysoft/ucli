using System;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Provides a shared logger test double that discards every event. </summary>
    internal sealed class NoOpDaemonLogger : IDaemonLogger
    {
        public static NoOpDaemonLogger Instance { get; } = new NoOpDaemonLogger();

        private NoOpDaemonLogger ()
        {
        }

        public void Info (
            string category,
            string message,
            string raw = null)
        {
        }

        public void Warning (
            string category,
            string message,
            string raw = null)
        {
        }

        public void Error (
            string category,
            string message,
            string raw = null)
        {
        }

        public void Exception (
            string category,
            string message,
            Exception exception)
        {
        }
    }
}
