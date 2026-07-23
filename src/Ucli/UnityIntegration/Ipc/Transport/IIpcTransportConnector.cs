using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Transport;

/// <summary> Opens one owned stream for an explicitly resolved IPC endpoint. </summary>
internal interface IIpcTransportConnector
{
    /// <summary> Opens an IPC transport stream. </summary>
    /// <param name="endpoint"> The endpoint to connect. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the transport operation. </param>
    /// <returns> The connected stream. The caller owns the returned stream. </returns>
    ValueTask<Stream> ConnectAsync (
        IpcTransportEndpoint endpoint,
        CancellationToken cancellationToken);
}
