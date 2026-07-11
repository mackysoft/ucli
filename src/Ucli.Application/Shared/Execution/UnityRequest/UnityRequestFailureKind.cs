namespace MackySoft.Ucli.Application.Shared.Execution.UnityRequest;

/// <summary> Represents Unity request failure kinds observed at the IPC boundary. </summary>
internal enum UnityRequestFailureKind
{
    /// <summary> Indicates a request failure that is not a transport interruption. </summary>
    General = 0,

    /// <summary> Indicates the IPC transport ended before the complete response was read. </summary>
    TransportInterrupted = 1,
}
