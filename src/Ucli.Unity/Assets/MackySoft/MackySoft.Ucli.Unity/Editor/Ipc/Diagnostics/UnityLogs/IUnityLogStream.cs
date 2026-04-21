namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Captures Unity log events and exposes snapshot reads for IPC handlers. </summary>
    internal interface IUnityLogStream
    {
        /// <summary> Writes one Unity log event into the in-memory stream. </summary>
        /// <param name="source"> The Unity log source. </param>
        /// <param name="level"> The Unity log level. </param>
        /// <param name="message"> The user-facing message value. </param>
        /// <param name="stackTrace"> The optional stack trace. </param>
        void Write (
            string source,
            string level,
            string message,
            string stackTrace = null);

        /// <summary> Creates one immutable snapshot of current stream values. </summary>
        /// <returns> The Unity-log stream snapshot. </returns>
        UnityLogSnapshot Snapshot ();
    }
}