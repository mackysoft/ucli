namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines machine-readable error kinds for IPC payload reads. </summary>
public enum IpcPayloadReadErrorKind
{
    /// <summary> No error. </summary>
    None = 0,

    /// <summary> Payload was deserialized to <see langword="null" />. </summary>
    NullPayload,

    /// <summary> Payload could not be deserialized to the target model. </summary>
    DeserializeFailed,
}
