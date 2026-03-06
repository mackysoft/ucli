namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Captures daemon control-log events and exposes snapshot reads for IPC handlers. </summary>
    internal interface IDaemonLogStream
    {
        /// <summary> Writes one daemon log event into the in-memory stream. </summary>
        /// <param name="category"> The daemon log category. </param>
        /// <param name="level"> The daemon log level. </param>
        /// <param name="message"> The user-facing message value. </param>
        /// <param name="raw"> The optional raw detail payload. </param>
        void Write (
            string category,
            string level,
            string message,
            string raw = null);

        /// <summary> Creates one immutable snapshot of current stream values. </summary>
        /// <returns> The daemon-log stream snapshot. </returns>
        DaemonLogSnapshot Snapshot ();
    }
}
