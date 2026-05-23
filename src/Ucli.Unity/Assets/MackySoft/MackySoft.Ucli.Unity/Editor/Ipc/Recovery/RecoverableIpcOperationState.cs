namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Defines recoverable IPC operation states used by daemon replay logic. </summary>
    internal enum RecoverableIpcOperationState
    {
        /// <summary> The operation has started and may be resumed after a domain reload. </summary>
        Pending,

        /// <summary> The operation response has been durably recorded and can be replayed. </summary>
        Completed,
    }
}
