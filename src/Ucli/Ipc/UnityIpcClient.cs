using System.IO.Pipes;
using System.Net.Sockets;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Ipc;

/// <summary> Implements transport-level IPC communication with Unity daemon endpoints. </summary>
internal sealed class UnityIpcClient : IUnityIpcClient
{
    private readonly IIpcEndpointResolver endpointResolver;

    /// <summary> Initializes a new instance of the <see cref="UnityIpcClient" /> class. </summary>
    /// <param name="endpointResolver"> The endpoint resolver used to locate daemon endpoints. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="endpointResolver" /> is <see langword="null" />. </exception>
    public UnityIpcClient (IIpcEndpointResolver endpointResolver)
    {
        this.endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
    }

    /// <summary> Sends one IPC request to the resolved endpoint and returns its response. </summary>
    /// <param name="projectRoot"> The Unity project root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The Unity project fingerprint. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="request"> The request envelope to send. Must not be <see langword="null" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The response returned by Unity daemon. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="projectRoot" /> or <paramref name="projectFingerprint" /> is <see langword="null" />, empty, or whitespace. </exception>
    public async ValueTask<IpcResponse> SendAsync (
        string projectRoot,
        string projectFingerprint,
        IpcRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var endpoint = endpointResolver.Resolve(projectRoot, projectFingerprint);

        await using var stream = await ConnectAsync(endpoint, cancellationToken);
        await IpcFrameCodec.WriteModelAsync(
            stream,
            request,
            IpcJsonSerializerOptions.Default,
            cancellationToken: cancellationToken);
        return await IpcFrameCodec.ReadModelAsync<IpcResponse>(
            stream,
            IpcJsonSerializerOptions.Default,
            cancellationToken: cancellationToken);
    }

    /// <summary> Opens a stream connection to the specified endpoint. </summary>
    /// <param name="endpoint"> The endpoint to connect. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The connected stream. </returns>
    /// <exception cref="InvalidOperationException"> Thrown when endpoint transport kind is unsupported. </exception>
    private static async ValueTask<Stream> ConnectAsync (
        IpcEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        return endpoint.TransportKind switch
        {
            IpcTransportKind.NamedPipe => await ConnectNamedPipeAsync(endpoint.Address, cancellationToken),
            IpcTransportKind.UnixDomainSocket => await ConnectUnixDomainSocketAsync(endpoint.Address, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported IPC transport kind: {endpoint.TransportKind}."),
        };
    }

    /// <summary> Connects a named pipe client stream to the server pipe. </summary>
    /// <param name="pipeName"> The named pipe name. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The connected named pipe stream. </returns>
    private static async ValueTask<Stream> ConnectNamedPipeAsync (
        string pipeName,
        CancellationToken cancellationToken)
    {
        var stream = new NamedPipeClientStream(
            serverName: ".",
            pipeName: pipeName,
            direction: PipeDirection.InOut,
            options: PipeOptions.Asynchronous);

        await stream.ConnectAsync(cancellationToken);
        return stream;
    }

    /// <summary> Connects a Unix domain socket stream to the server socket. </summary>
    /// <param name="socketPath"> The Unix domain socket file path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The connected network stream. </returns>
    private static async ValueTask<Stream> ConnectUnixDomainSocketAsync (
        string socketPath,
        CancellationToken cancellationToken)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            var endPoint = new UnixDomainSocketEndPoint(socketPath);
            await socket.ConnectAsync(endPoint, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
