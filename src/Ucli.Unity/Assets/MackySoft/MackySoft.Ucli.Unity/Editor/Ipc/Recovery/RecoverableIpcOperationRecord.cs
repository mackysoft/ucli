using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents one persisted recoverable IPC operation journal record. </summary>
    internal sealed class RecoverableIpcOperationRecord
    {
        private RecoverableIpcOperationState state;
        private string unsupportedPersistedState;
        private bool hasState;
        private bool hasPersistedState;

        /// <summary> Gets or sets the record schema version. </summary>
        public int SchemaVersion { get; set; }

        /// <summary> Gets or sets the project fingerprint served by this operation. </summary>
        public ProjectFingerprint ProjectFingerprint { get; set; }

        /// <summary> Gets or sets the IPC method name. </summary>
        public string Method { get; set; }

        /// <summary> Gets or sets the IPC request id. </summary>
        public Guid RequestId { get; set; }

        /// <summary> Gets or sets the hash of the stable request payload identity. </summary>
        public string RequestPayloadHash { get; set; }

        /// <summary> Gets or sets the Unity host process id that created this record. </summary>
        public int HostProcessId { get; set; }

        /// <summary> Gets or sets the Unity Editor process instance identifier that created this record. </summary>
        public Guid HostEditorInstanceId { get; set; }

        /// <summary> Gets or sets the typed operation state used by recovery logic. </summary>
        [JsonIgnore]
        public RecoverableIpcOperationState State
        {
            get => state;
            set
            {
                state = value;
                hasState = true;
            }
        }

        /// <summary> Gets a value indicating whether a persisted state value was provided. </summary>
        [JsonIgnore]
        public bool HasState => hasState;

        /// <summary> Gets a value indicating whether a persisted state literal was present. </summary>
        [JsonIgnore]
        public bool HasPersistedState => hasPersistedState;

        /// <summary> Gets the unsupported persisted state literal when parsing failed. </summary>
        [JsonIgnore]
        public string UnsupportedPersistedState => unsupportedPersistedState;

        /// <summary> Gets or sets the persisted operation state literal. </summary>
        [JsonPropertyName("state")]
        public string PersistedState
        {
            get
            {
                if (HasState)
                {
                    return ToPersistedState(State);
                }

                if (HasPersistedState)
                {
                    return UnsupportedPersistedState;
                }

                throw new JsonException("Recoverable IPC operation state is missing.");
            }
            set
            {
                hasPersistedState = true;
                if (!TryParsePersistedState(value, out var parsedState))
                {
                    unsupportedPersistedState = value;
                    hasState = false;
                    state = default;
                    return;
                }

                State = parsedState;
                unsupportedPersistedState = null;
            }
        }

        /// <summary> Gets or sets the time when the operation first became pending. </summary>
        public DateTimeOffset StartedAtUtc { get; set; }

        /// <summary> Gets or sets the time when the operation response was completed. </summary>
        public DateTimeOffset? CompletedAtUtc { get; set; }

        /// <summary> Gets or sets method-specific recovery payload. </summary>
        public JsonElement RecoveryPayload { get; set; }

        /// <summary> Gets or sets the completed IPC response. </summary>
        public IpcResponse Response { get; set; }

        private static string ToPersistedState (RecoverableIpcOperationState state)
        {
            // NOTE: The persisted operation journal is a JSON contract, so it keeps
            // stable lowercase literals. Runtime recovery logic uses the enum above
            // to avoid silently treating unknown values as a known branch.
            return state switch
            {
                RecoverableIpcOperationState.Pending => "pending",
                RecoverableIpcOperationState.Completed => "completed",
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Recoverable IPC operation state is unsupported."),
            };
        }

        private static bool TryParsePersistedState (
            string value,
            out RecoverableIpcOperationState state)
        {
            switch (value)
            {
                case "pending":
                    state = RecoverableIpcOperationState.Pending;
                    return true;
                case "completed":
                    state = RecoverableIpcOperationState.Completed;
                    return true;
                default:
                    state = default;
                    return false;
            }
        }
    }
}
