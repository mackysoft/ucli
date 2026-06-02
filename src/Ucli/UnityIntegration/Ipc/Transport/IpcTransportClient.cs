using System.IO.Pipes;
using System.Net.Sockets;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Transport;

/// <summary> Implements transport-level IPC communication with explicitly resolved endpoints. </summary>
internal sealed class IpcTransportClient : IIpcTransportClient
{
    /// <inheritdoc />
    public async ValueTask<IpcResponse> SendAsync (
        IpcEndpoint endpoint,
        IpcRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureResponseMode(request, IpcResponseMode.Single, nameof(SendAsync));

        using var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellationTokenSource.CancelAfter(timeout);
        var ipcCancellationToken = timeoutCancellationTokenSource.Token;
        var hasConnected = false;
        try
        {
            await using var stream = await ConnectAsync(endpoint, ipcCancellationToken).ConfigureAwait(false);
            hasConnected = true;
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

            ValidateIpcResponse(request, readResult.Value);
            return readResult.Value;
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested && timeoutCancellationTokenSource.IsCancellationRequested)
        {
            if (!hasConnected)
            {
                throw new IpcConnectTimeoutException(
                    $"IPC connection timed out after {timeout.TotalMilliseconds:0} milliseconds.",
                    exception);
            }

            throw new TimeoutException(
                $"IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds.",
                exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IpcResponse> SendStreamingAsync (
        IpcEndpoint endpoint,
        IpcRequest request,
        TimeSpan timeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onProgressFrame);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureResponseMode(request, IpcResponseMode.Stream, nameof(SendStreamingAsync));

        using var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellationTokenSource.CancelAfter(timeout);
        var ipcCancellationToken = timeoutCancellationTokenSource.Token;
        var hasConnected = false;
        try
        {
            await using var stream = await ConnectAsync(endpoint, ipcCancellationToken).ConfigureAwait(false);
            hasConnected = true;
            await IpcFrameCodec.WriteModelAsync(
                    stream,
                    request,
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: ipcCancellationToken)
                .ConfigureAwait(false);

            return await ReadStreamingResponseAsync(
                    stream,
                    request,
                    onProgressFrame,
                    ipcCancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested && timeoutCancellationTokenSource.IsCancellationRequested)
        {
            if (!hasConnected)
            {
                throw new IpcConnectTimeoutException(
                    $"IPC connection timed out after {timeout.TotalMilliseconds:0} milliseconds.",
                    exception);
            }

            throw new TimeoutException(
                $"IPC streaming request timed out after {timeout.TotalMilliseconds:0} milliseconds.",
                exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IpcResponse> SendWithUnboundedResponseWaitAsync (
        IpcEndpoint endpoint,
        IpcRequest request,
        TimeSpan sendTimeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sendTimeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureResponseMode(request, IpcResponseMode.Single, nameof(SendWithUnboundedResponseWaitAsync));

        using var sendTimeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        sendTimeoutCancellationTokenSource.CancelAfter(sendTimeout);
        var sendCancellationToken = sendTimeoutCancellationTokenSource.Token;
        var hasConnected = false;
        try
        {
            await using var stream = await ConnectAsync(endpoint, sendCancellationToken).ConfigureAwait(false);
            hasConnected = true;
            await IpcFrameCodec.WriteModelAsync(
                    stream,
                    request,
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: sendCancellationToken)
                .ConfigureAwait(false);

            var readResult = await IpcFrameCodec.TryReadModelAsync<IpcResponse>(
                    stream,
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (!readResult.IsSuccess)
            {
                throw CreateFrameReadException(readResult.ErrorKind, readResult.ErrorMessage);
            }

            ValidateIpcResponse(request, readResult.Value);
            return readResult.Value;
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested && sendTimeoutCancellationTokenSource.IsCancellationRequested)
        {
            if (!hasConnected)
            {
                throw new IpcConnectTimeoutException(
                    $"IPC connection timed out after {sendTimeout.TotalMilliseconds:0} milliseconds.",
                    exception);
            }

            throw new TimeoutException(
                $"IPC request write timed out after {sendTimeout.TotalMilliseconds:0} milliseconds.",
                exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IpcResponse> SendStreamingWithUnboundedResponseWaitAsync (
        IpcEndpoint endpoint,
        IpcRequest request,
        TimeSpan sendTimeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onProgressFrame);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sendTimeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureResponseMode(request, IpcResponseMode.Stream, nameof(SendStreamingWithUnboundedResponseWaitAsync));

        using var sendTimeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        sendTimeoutCancellationTokenSource.CancelAfter(sendTimeout);
        var sendCancellationToken = sendTimeoutCancellationTokenSource.Token;
        var hasConnected = false;
        try
        {
            await using var stream = await ConnectAsync(endpoint, sendCancellationToken).ConfigureAwait(false);
            hasConnected = true;
            await IpcFrameCodec.WriteModelAsync(
                    stream,
                    request,
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: sendCancellationToken)
                .ConfigureAwait(false);

            return await ReadStreamingResponseAsync(
                    stream,
                    request,
                    onProgressFrame,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested && sendTimeoutCancellationTokenSource.IsCancellationRequested)
        {
            if (!hasConnected)
            {
                throw new IpcConnectTimeoutException(
                    $"IPC connection timed out after {sendTimeout.TotalMilliseconds:0} milliseconds.",
                    exception);
            }

            throw new TimeoutException(
                $"IPC streaming request write timed out after {sendTimeout.TotalMilliseconds:0} milliseconds.",
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

    private static void EnsureResponseMode (
        IpcRequest request,
        IpcResponseMode expectedResponseMode,
        string operationName)
    {
        if (ContractLiteralCodec.TryParse<IpcResponseMode>(request.ResponseMode, out var responseMode)
            && responseMode == expectedResponseMode)
        {
            return;
        }

        var expectedLiteral = ContractLiteralCodec.ToValue(expectedResponseMode);
        throw new InvalidOperationException($"IPC {operationName} requires responseMode='{expectedLiteral}'. Actual: {request.ResponseMode}.");
    }

    private static async ValueTask<IpcResponse> ReadStreamingResponseAsync (
        Stream stream,
        IpcRequest request,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var readResult = await IpcFrameCodec.TryReadModelAsync<IpcStreamFrame>(
                    stream,
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (!readResult.IsSuccess)
            {
                throw CreateFrameReadException(readResult.ErrorKind, readResult.ErrorMessage);
            }

            var frame = readResult.Value;
            ValidateStreamingFrame(request, frame);
            if (string.Equals(frame.Kind, IpcStreamFrameKinds.Progress, StringComparison.Ordinal))
            {
                try
                {
                    await onProgressFrame(frame, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new IpcProgressFrameHandlerException(exception);
                }

                continue;
            }

            return frame.Response!;
        }
    }

    private static void ValidateStreamingFrame (
        IpcRequest request,
        IpcStreamFrame frame)
    {
        if (frame.ProtocolVersion != IpcProtocol.CurrentVersion)
        {
            throw new InvalidDataException(
                $"IPC stream frame protocol version mismatch. Requested={IpcProtocol.CurrentVersion}, Actual={frame.ProtocolVersion}.");
        }

        if (!string.Equals(frame.RequestId, request.RequestId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"IPC stream frame requestId mismatch. Expected={request.RequestId}, Actual={frame.RequestId}.");
        }

        if (string.Equals(frame.Kind, IpcStreamFrameKinds.Progress, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(frame.Event))
            {
                throw new InvalidDataException("IPC progress stream frame must contain an event name.");
            }

            if (frame.Response is not null)
            {
                throw new InvalidDataException("IPC progress stream frame must not contain a terminal response.");
            }

            return;
        }

        if (string.Equals(frame.Kind, IpcStreamFrameKinds.Terminal, StringComparison.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(frame.Event))
            {
                throw new InvalidDataException("IPC terminal stream frame must not contain an event name.");
            }

            if (frame.Response is null)
            {
                throw new InvalidDataException("IPC terminal stream frame must contain a response.");
            }

            if (!string.Equals(frame.Response.RequestId, request.RequestId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"IPC terminal response requestId mismatch. Expected={request.RequestId}, Actual={frame.Response.RequestId}.");
            }

            ValidateIpcResponse(request, frame.Response);
            return;
        }

        throw new InvalidDataException($"Unsupported IPC stream frame kind: {frame.Kind}.");
    }

    private static void ValidateIpcResponse (
        IpcRequest request,
        IpcResponse response)
    {
        if (response.ProtocolVersion != IpcProtocol.CurrentVersion)
        {
            throw new InvalidDataException(
                $"IPC response protocol version mismatch. Requested={IpcProtocol.CurrentVersion}, Actual={response.ProtocolVersion}.");
        }

        if (!string.Equals(response.RequestId, request.RequestId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"IPC response requestId mismatch. Expected={request.RequestId}, Actual={response.RequestId}.");
        }

        if (!string.Equals(response.Status, IpcProtocol.StatusOk, StringComparison.Ordinal)
            && !string.Equals(response.Status, IpcProtocol.StatusError, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported IPC response status: {response.Status}.");
        }

        if (response.Errors is null)
        {
            throw new InvalidDataException("IPC response errors must not be null.");
        }
    }

    /// <summary> Opens a stream connection to the specified endpoint. </summary>
    /// <param name="endpoint"> The endpoint to connect. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The connected stream. </returns>
    private static async ValueTask<Stream> ConnectAsync (
        IpcEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        return endpoint.TransportKind switch
        {
            IpcTransportKind.NamedPipe => await ConnectNamedPipeAsync(endpoint.Address, cancellationToken).ConfigureAwait(false),
            IpcTransportKind.UnixDomainSocket => await ConnectUnixDomainSocketAsync(endpoint.Address, cancellationToken).ConfigureAwait(false),
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
