using System;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Emits daemon control logs to Unity console and daemon in-memory stream. </summary>
    internal interface IDaemonLogger
    {
        /// <summary> Emits one informational daemon control log. </summary>
        /// <param name="category"> The log category. </param>
        /// <param name="message"> The user-facing message value. </param>
        /// <param name="raw"> The optional raw detail payload. </param>
        void Info (
            string category,
            string message,
            string raw = null);

        /// <summary> Emits one warning daemon control log. </summary>
        /// <param name="category"> The log category. </param>
        /// <param name="message"> The user-facing message value. </param>
        /// <param name="raw"> The optional raw detail payload. </param>
        void Warning (
            string category,
            string message,
            string raw = null);

        /// <summary> Emits one error daemon control log. </summary>
        /// <param name="category"> The log category. </param>
        /// <param name="message"> The user-facing message value. </param>
        /// <param name="raw"> The optional raw detail payload. </param>
        void Error (
            string category,
            string message,
            string raw = null);

        /// <summary> Emits one exception daemon control log. </summary>
        /// <param name="category"> The log category. </param>
        /// <param name="message"> The user-facing message value. </param>
        /// <param name="exception"> The related exception. </param>
        void Exception (
            string category,
            string message,
            Exception exception);
    }
}
