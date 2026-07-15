namespace MackySoft.Ucli.UnityIntegration.Ipc.Transport;

/// <summary> Indicates that a streaming transport operation already owns the client admission slot. </summary>
internal sealed class IpcStreamingOperationInProgressException : InvalidOperationException
{
    /// <summary> Initializes the exception for an active or not-yet-converged streaming operation. </summary>
    internal IpcStreamingOperationInProgressException ()
        : base("A previous IPC streaming operation is still active or has not converged after cancellation or timeout.")
    {
    }
}
