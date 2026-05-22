using System;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Persists recoverable IPC operation state across Unity domain reload. </summary>
    internal interface IRecoverableIpcOperationStore
    {
        /// <summary> Reads one recoverable operation record when present. </summary>
        bool TryRead (
            string method,
            string requestId,
            out RecoverableIpcOperationRecord record,
            out string errorMessage);

        /// <summary> Writes one pending recoverable operation record. </summary>
        bool TryWritePending (
            string method,
            string requestId,
            DateTimeOffset startedAtUtc,
            JsonElement recoveryPayload,
            out string errorMessage);

        /// <summary> Writes one completed recoverable operation record. </summary>
        bool TryWriteCompleted (
            string method,
            string requestId,
            DateTimeOffset startedAtUtc,
            DateTimeOffset completedAtUtc,
            JsonElement recoveryPayload,
            IpcResponse response,
            out string errorMessage);

        /// <summary> Removes expired completed operation records. </summary>
        bool TryPurgeExpiredCompletedRecords (
            DateTimeOffset nowUtc,
            out string errorMessage);
    }
}
