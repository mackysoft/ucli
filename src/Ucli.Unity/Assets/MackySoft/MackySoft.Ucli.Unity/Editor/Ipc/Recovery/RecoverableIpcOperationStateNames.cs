namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Defines persisted recoverable IPC operation state names. </summary>
    internal static class RecoverableIpcOperationStateNames
    {
        /// <summary> Gets the state name used while a recoverable operation is in progress. </summary>
        public const string Pending = "pending";

        /// <summary> Gets the state name used after the response has been durably recorded. </summary>
        public const string Completed = "completed";
    }
}
