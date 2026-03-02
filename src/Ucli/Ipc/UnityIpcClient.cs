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
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The Unity project fingerprint. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="request"> The request envelope to send. Must not be <see langword="null" />. </param>
    /// <param name="timeout"> The timeout for one IPC request. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The response returned by Unity daemon. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> or <paramref name="projectFingerprint" /> is <see langword="null" />, empty, or whitespace. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    /// <exception cref="TimeoutException"> Thrown when one IPC request exceeds <paramref name="timeout" />. </exception>
    public async ValueTask<IpcResponse> SendAsync (
        string storageRoot,
        string projectFingerprint,
        IpcRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var endpoint = endpointResolver.Resolve(storageRoot, projectFingerprint);

        using var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellationTokenSource.CancelAfter(timeout);
        var ipcCancellationToken = timeoutCancellationTokenSource.Token;
        try
        {
            await using var stream = await ConnectAsync(endpoint, ipcCancellationToken).ConfigureAwait(false);
            await IpcFrameCodec.WriteModelAsync(
                    stream,
                    request,
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: ipcCancellationToken)
                .ConfigureAwait(false);

            var readResult = await IpcFrameCodec.TryReadModelAsync<IpcResponse>(
                    stream,
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: ipcCancellationToken)
                .ConfigureAwait(false);
            if (!readResult.IsSuccess)
            {
                throw CreateFrameReadException(readResult.ErrorKind, readResult.ErrorMessage);
            }

            return readResult.Value;
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested && timeoutCancellationTokenSource.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds.",
                exception);
        }
    }

    /// <summary> Maps one frame read error kind to legacy exception categories for caller compatibility. </summary>
    /// <param name="errorKind"> The frame read error kind. </param>
    /// <param name="errorMessage"> The diagnostic frame read error message. </param>
    /// <returns> The mapped exception value. </returns>
    private static Exception CreateFrameReadException (
        IpcFrameReadErrorKind errorKind,
        string errorMessage)
    {
        return errorKind switch
        {
            IpcFrameReadErrorKind.HeaderTruncated => new EndOfStreamException(errorMessage),
            IpcFrameReadErrorKind.PayloadTruncated => new EndOfStreamException(errorMessage),
            _ => new InvalidDataException(errorMessage),
        };
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