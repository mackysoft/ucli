using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Persists recoverable IPC operation state across Unity domain reload. </summary>
    internal interface IRecoverableIpcOperationStore
    {
        /// <summary> Reads one recoverable operation record when present. </summary>
        ValueTask<RecoverableIpcOperationReadResult> ReadAsync (
            UnityIpcMethod method,
            Guid requestId,
            Sha256Digest requestPayloadHash,
            CancellationToken cancellationToken);

        /// <summary> Writes one pending recoverable operation record. </summary>
        ValueTask<RecoverableIpcOperationStoreResult> WritePendingAsync (
            UnityIpcMethod method,
            Guid requestId,
            Sha256Digest requestPayloadHash,
            DateTimeOffset startedAtUtc,
            JsonElement recoveryPayload,
            CancellationToken cancellationToken);

        /// <summary> Writes one completed recoverable operation record. </summary>
        ValueTask<RecoverableIpcOperationStoreResult> WriteCompletedAsync (
            UnityIpcMethod method,
            Guid requestId,
            Sha256Digest requestPayloadHash,
            DateTimeOffset startedAtUtc,
            DateTimeOffset completedAtUtc,
            JsonElement recoveryPayload,
            IpcResponse response,
            CancellationToken cancellationToken);

        /// <summary> Consumes the latest background-maintenance failure for main-thread reporting. </summary>
        string? ConsumeMaintenanceFailure ();
    }

    /// <summary> Represents one recoverable operation store mutation outcome. </summary>
    internal sealed class RecoverableIpcOperationStoreResult
    {
        private RecoverableIpcOperationStoreResult (string errorMessage)
        {
            ErrorMessage = errorMessage;
        }

        /// <summary> Gets whether the store operation completed successfully. </summary>
        public bool IsSuccess => string.IsNullOrWhiteSpace(ErrorMessage);

        /// <summary> Gets the persistence failure message when the operation failed. </summary>
        public string ErrorMessage { get; }

        /// <summary> Creates a successful store result. </summary>
        public static RecoverableIpcOperationStoreResult Success ()
        {
            return new RecoverableIpcOperationStoreResult(null);
        }

        /// <summary> Creates a failed store result. </summary>
        public static RecoverableIpcOperationStoreResult Failure (string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new ArgumentException("Store failure message must not be empty.", nameof(errorMessage));
            }

            return new RecoverableIpcOperationStoreResult(errorMessage);
        }
    }

    /// <summary> Represents one recoverable operation store read outcome. </summary>
    internal sealed class RecoverableIpcOperationReadResult
    {
        private RecoverableIpcOperationReadResult (
            RecoverableIpcOperationRecord record,
            string errorMessage)
        {
            Record = record;
            ErrorMessage = errorMessage;
        }

        /// <summary> Gets whether the read completed without a persistence or validation failure. </summary>
        public bool IsSuccess => string.IsNullOrWhiteSpace(ErrorMessage);

        /// <summary> Gets the matching record, or <see langword="null" /> when no record exists. </summary>
        public RecoverableIpcOperationRecord Record { get; }

        /// <summary> Gets the read failure message. </summary>
        public string ErrorMessage { get; }

        /// <summary> Creates a successful missing-record result. </summary>
        public static RecoverableIpcOperationReadResult Missing ()
        {
            return new RecoverableIpcOperationReadResult(null, null);
        }

        /// <summary> Creates a successful record read result. </summary>
        public static RecoverableIpcOperationReadResult Success (RecoverableIpcOperationRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            return new RecoverableIpcOperationReadResult(record, null);
        }

        /// <summary> Creates a failed read result. </summary>
        public static RecoverableIpcOperationReadResult Failure (string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new ArgumentException("Store read failure message must not be empty.", nameof(errorMessage));
            }

            return new RecoverableIpcOperationReadResult(null, errorMessage);
        }
    }
}
