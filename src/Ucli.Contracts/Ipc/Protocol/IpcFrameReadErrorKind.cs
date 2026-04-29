namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines machine-readable error kinds for IPC frame reads. </summary>
public enum IpcFrameReadErrorKind
{
    /// <summary> No error. </summary>
    None = 0,

    /// <summary> Frame header bytes are truncated. </summary>
    HeaderTruncated,

    /// <summary> Frame payload length is negative. </summary>
    PayloadLengthNegative,

    /// <summary> Frame payload length exceeds configured maximum size. </summary>
    PayloadTooLarge,

    /// <summary> Frame payload bytes are truncated. </summary>
    PayloadTruncated,

    /// <summary> Frame payload JSON is invalid for target model. </summary>
    PayloadJsonInvalid,

    /// <summary> Frame payload was deserialized to <see langword="null" />. </summary>
    PayloadModelNull,

    /// <summary> Frame read failed for non-protocol reasons. </summary>
    StreamReadFailed,
}
