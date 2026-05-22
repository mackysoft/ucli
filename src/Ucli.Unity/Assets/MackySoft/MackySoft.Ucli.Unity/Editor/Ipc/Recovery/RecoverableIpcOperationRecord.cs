using System;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents one persisted recoverable IPC operation journal record. </summary>
    internal sealed class RecoverableIpcOperationRecord
    {
        /// <summary> Gets or sets the record schema version. </summary>
        public int SchemaVersion { get; set; }

        /// <summary> Gets or sets the project fingerprint served by this operation. </summary>
        public string ProjectFingerprint { get; set; }

        /// <summary> Gets or sets the IPC method name. </summary>
        public string Method { get; set; }

        /// <summary> Gets or sets the IPC request id. </summary>
        public string RequestId { get; set; }

        /// <summary> Gets or sets the operation state. </summary>
        public string State { get; set; }

        /// <summary> Gets or sets the time when the operation first became pending. </summary>
        public DateTimeOffset StartedAtUtc { get; set; }

        /// <summary> Gets or sets the time when the operation response was completed. </summary>
        public DateTimeOffset? CompletedAtUtc { get; set; }

        /// <summary> Gets or sets method-specific recovery payload. </summary>
        public JsonElement RecoveryPayload { get; set; }

        /// <summary> Gets or sets the completed IPC response. </summary>
        public IpcResponse Response { get; set; }
    }
}
