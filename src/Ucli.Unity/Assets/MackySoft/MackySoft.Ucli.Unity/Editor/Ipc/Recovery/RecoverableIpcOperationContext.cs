using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Provides one recoverable IPC operation's persisted state to a method handler. </summary>
    internal sealed class RecoverableIpcOperationContext
    {
        private readonly IRecoverableIpcOperationStore store;
        private readonly string method;
        private readonly string requestId;
        private readonly string requestPayloadHash;

        private readonly object completionSyncRoot = new object();

        private DateTimeOffset startedAtUtc;
        private JsonElement recoveryPayload;
        private bool hasRecord;
        private Task<RecoverableIpcOperationStoreResult> completionTask;

        /// <summary> Initializes a new instance of the <see cref="RecoverableIpcOperationContext" /> class. </summary>
        public RecoverableIpcOperationContext (
            IRecoverableIpcOperationStore store,
            string method,
            string requestId,
            string requestPayloadHash,
            RecoverableIpcOperationRecord record)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            if (string.IsNullOrWhiteSpace(method))
            {
                throw new ArgumentException("Method must not be empty.", nameof(method));
            }

            if (string.IsNullOrWhiteSpace(requestId))
            {
                throw new ArgumentException("Request id must not be empty.", nameof(requestId));
            }

            if (string.IsNullOrWhiteSpace(requestPayloadHash))
            {
                throw new ArgumentException("Request payload hash must not be empty.", nameof(requestPayloadHash));
            }

            this.method = method;
            this.requestId = requestId;
            this.requestPayloadHash = requestPayloadHash;
            if (record != null)
            {
                hasRecord = true;
                startedAtUtc = record.StartedAtUtc;
                recoveryPayload = record.RecoveryPayload.Clone();
            }
        }

        /// <summary> Gets whether this context has a pending or completed operation record. </summary>
        public bool HasOperationRecord => hasRecord;

        /// <summary> Gets the UTC time when this operation first became pending, when present. </summary>
        public DateTimeOffset? StartedAtUtc => hasRecord ? startedAtUtc : null;

        /// <summary> Tries to deserialize the pending recovery payload. </summary>
        public bool TryReadPendingPayload<TPayload> (
            out TPayload payload,
            out string errorMessage)
        {
            payload = default;
            if (!hasRecord || recoveryPayload.ValueKind == JsonValueKind.Undefined)
            {
                errorMessage = null;
                return false;
            }

            if (!IpcPayloadCodec.TryDeserialize(recoveryPayload, out TPayload parsedPayload, out var readError))
            {
                errorMessage = readError.Message;
                return false;
            }

            payload = parsedPayload;
            errorMessage = null;
            return true;
        }

        /// <summary> Marks the operation pending before the method performs its state-changing action. </summary>
        public async ValueTask<RecoverableIpcOperationStoreResult> MarkPendingAsync<TPayload> (
            TPayload payload,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var nextStartedAtUtc = hasRecord ? startedAtUtc : DateTimeOffset.UtcNow;

            var nextRecoveryPayload = IpcPayloadCodec.SerializeToElement(payload);
            // NOTE: Pending state is written before the handler performs its Unity
            // state-changing action. Domain reload recovery depends on this payload.
            var result = await store.WritePendingAsync(
                method,
                requestId,
                requestPayloadHash,
                nextStartedAtUtc,
                nextRecoveryPayload,
                cancellationToken);
            if (!result.IsSuccess)
            {
                return result;
            }

            hasRecord = true;
            startedAtUtc = nextStartedAtUtc;
            recoveryPayload = nextRecoveryPayload.Clone();
            return result;
        }

        /// <summary> Marks the operation completed with the response returned by the method handler. </summary>
        public ValueTask<RecoverableIpcOperationStoreResult> MarkCompletedAsync (
            IpcResponse response,
            CancellationToken cancellationToken)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            if (!hasRecord)
            {
                return new ValueTask<RecoverableIpcOperationStoreResult>(
                    RecoverableIpcOperationStoreResult.Success());
            }

            lock (completionSyncRoot)
            {
                completionTask ??= MarkCompletedCoreAsync(response, cancellationToken);
                return new ValueTask<RecoverableIpcOperationStoreResult>(completionTask);
            }
        }

        private async Task<RecoverableIpcOperationStoreResult> MarkCompletedCoreAsync (
            IpcResponse response,
            CancellationToken cancellationToken)
        {
            return await store.WriteCompletedAsync(
                method,
                requestId,
                requestPayloadHash,
                startedAtUtc,
                DateTimeOffset.UtcNow,
                recoveryPayload,
                response,
                cancellationToken);
        }
    }
}
