using System.IO.Pipes;
using System.Net.Sockets;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Transport;

/// <summary> Opens named-pipe and Unix-domain-socket IPC streams. </summary>
internal sealed class IpcTransportConnector : IIpcTransportConnector
{
    /// <inheritdoc />
    public async ValueTask<Stream> ConnectAsync (
        IpcTransportEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return endpoint.Contract.TransportKind switch
        {
            IpcTransportKind.NamedPipe => await ConnectNamedPipeAsync(endpoint.Contract.Address, cancellationToken).ConfigureAwait(false),
            IpcTransportKind.UnixDomainSocket => await ConnectUnixDomainSocketAsync(
                endpoint.UnixSocketPath
                    ?? throw new InvalidOperationException("A Unix-domain-socket transport endpoint must retain its guarded path."),
                cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported IPC transport kind: {endpoint.Contract.TransportKind}."),
        };
    }

    private static async ValueTask<Stream> ConnectNamedPipeAsync (
        string pipeName,
        CancellationToken cancellationToken)
    {
        var stream = new NamedPipeClientStream(
            serverName: ".",
            pipeName: pipeName,
            direction: PipeDirection.InOut,
            options: PipeOptions.Asynchronous);

        try
        {
            await stream.ConnectAsync(cancellationToken).ConfigureAwait(false);
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static async ValueTask<Stream> ConnectUnixDomainSocketAsync (
        AbsolutePath socketPath,
        CancellationToken cancellationToken)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            var endPoint = new UnixDomainSocketEndPoint(socketPath.Value);
            await socket.ConnectAsync(endPoint, cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
