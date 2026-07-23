using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Transport;

/// <summary> Owns the supervisor IPC listener transport loop for one host instance. </summary>
internal sealed class SupervisorTransportServer
{
    private readonly object syncRoot = new();

    private readonly SupervisorTransportConnectionGroup activeConnections;

    private NamedPipeServerStream? activePipeStream;

    private Socket? activeSocket;

    private ExceptionDispatchInfo? fatalConnectionException;

    /// <summary> Initializes a new instance of the <see cref="SupervisorTransportServer" /> class. </summary>
    /// <param name="timeProvider"> The time provider used for bounded connection draining. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="timeProvider" /> is <see langword="null" />. </exception>
    public SupervisorTransportServer (TimeProvider timeProvider)
    {
        activeConnections = new SupervisorTransportConnectionGroup(
            stream => TryDisposeTransportHandle(stream),
            RecordFatalConnectionException,
            timeProvider ?? throw new ArgumentNullException(nameof(timeProvider)));
    }

    /// <summary> Runs the listener loop for the specified endpoint until cancellation is requested. </summary>
    /// <param name="endpoint"> The listener endpoint. </param>
    /// <param name="connectionHandler"> The per-connection request handler. </param>
    /// <param name="onStarted"> The callback invoked after the listener becomes ready. </param>
    /// <param name="maximumActiveConnections"> The maximum number of accepted connections handled concurrently. </param>
    /// <param name="connectionDrainTimeout"> The upper bound for draining accepted connections during shutdown. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the host. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when one transport limit is not positive. </exception>
    public async Task RunAsync (
        SupervisorTransportEndpoint endpoint,
        Func<Stream, CancellationToken, Task> connectionHandler,
        Func<CancellationToken, Task> onStarted,
        int maximumActiveConnections,
        TimeSpan connectionDrainTimeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(connectionHandler);
        ArgumentNullException.ThrowIfNull(onStarted);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumActiveConnections, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(connectionDrainTimeout, TimeSpan.Zero);

        if (endpoint.Contract.TransportKind == IpcTransportKind.NamedPipe)
        {
            try
            {
                await RunNamedPipeAsync(
                        endpoint.Contract.Address,
                        connectionHandler,
                        onStarted,
                        maximumActiveConnections,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                activeConnections.Release();
                await AwaitActiveConnectionsAsync(connectionDrainTimeout).ConfigureAwait(false);
            }

            return;
        }

        if (endpoint.Contract.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            try
            {
                await RunUnixSocketAsync(
                        endpoint.UnixSocketPath!,
                        connectionHandler,
                        onStarted,
                        maximumActiveConnections,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                activeConnections.Release();
                await AwaitActiveConnectionsAsync(connectionDrainTimeout).ConfigureAwait(false);
            }

            return;
        }

        throw new InvalidOperationException($"Unsupported supervisor IPC transport kind: {endpoint.Contract.TransportKind}.");
    }

    /// <summary> Releases listener handles and starts non-blocking cleanup for accepted connection handles. </summary>
    public void Release ()
    {
        NamedPipeServerStream? pipeStream;
        Socket? socket;
        lock (syncRoot)
        {
            pipeStream = activePipeStream;
            activePipeStream = null;
            socket = activeSocket;
            activeSocket = null;
        }

        TryDisposeTransportHandle(pipeStream);
        TryDisposeTransportHandle(socket);
        activeConnections.Release();
    }

    private async Task RunNamedPipeAsync (
        string address,
        Func<Stream, CancellationToken, Task> connectionHandler,
        Func<CancellationToken, Task> onStarted,
        int maximumActiveConnections,
        CancellationToken cancellationToken)
    {
        var startupCompleted = false;
        while (!cancellationToken.IsCancellationRequested)
        {
            ThrowIfFatalConnectionException();
            NamedPipeServerStream? serverStream = null;
            try
            {
                serverStream = new NamedPipeServerStream(
                    address,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                lock (syncRoot)
                {
                    activePipeStream = serverStream;
                }

                if (!startupCompleted)
                {
                    await onStarted(cancellationToken).ConfigureAwait(false);
                    startupCompleted = true;
                }

                await serverStream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                lock (syncRoot)
                {
                    if (ReferenceEquals(activePipeStream, serverStream))
                    {
                        activePipeStream = null;
                    }
                }

                if (activeConnections.TryStart(
                        serverStream,
                        connectionHandler,
                        maximumActiveConnections,
                        cancellationToken))
                {
                    serverStream = null;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            // Startup failures must escape so the host releases ownership instead of remaining undiscoverable.
            catch (Exception exception) when (
                startupCompleted
                && !cancellationToken.IsCancellationRequested
                && exception is IOException or InvalidDataException or ObjectDisposedException)
            {
            }
            finally
            {
                lock (syncRoot)
                {
                    if (serverStream != null && ReferenceEquals(activePipeStream, serverStream))
                    {
                        activePipeStream = null;
                    }
                }

                TryDisposeTransportHandle(serverStream);
            }
        }
    }

    private async Task RunUnixSocketAsync (
        AbsolutePath address,
        Func<Stream, CancellationToken, Task> connectionHandler,
        Func<CancellationToken, Task> onStarted,
        int maximumActiveConnections,
        CancellationToken cancellationToken)
    {
        var endpointOwnership = new SupervisorUnixSocketEndpointOwnership(address);
        Socket? listener = null;

        try
        {
            endpointOwnership.PrepareForBind();
            listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            listener.Bind(new UnixDomainSocketEndPoint(endpointOwnership.BoundAddress.Value));
            endpointOwnership.HardenBoundSocket();
            listener.Listen(maximumActiveConnections);
            endpointOwnership.PublishBoundEndpoint();

            lock (syncRoot)
            {
                activeSocket = listener;
            }

            await onStarted(cancellationToken).ConfigureAwait(false);
            endpointOwnership.CommitPublication();

            while (!cancellationToken.IsCancellationRequested)
            {
                ThrowIfFatalConnectionException();
                Socket? acceptedSocket = null;
                NetworkStream? networkStream = null;
                try
                {
                    acceptedSocket = await listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
                    networkStream = new NetworkStream(acceptedSocket, ownsSocket: true);
                    acceptedSocket = null;
                    if (activeConnections.TryStart(
                            networkStream,
                            connectionHandler,
                            maximumActiveConnections,
                            cancellationToken))
                    {
                        networkStream = null;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested && exception is IOException or InvalidDataException or SocketException or ObjectDisposedException)
                {
                }
                finally
                {
                    TryDisposeTransportHandle(networkStream);
                    TryDisposeTransportHandle(acceptedSocket);
                }
            }
        }
        finally
        {
            lock (syncRoot)
            {
                if (listener != null && ReferenceEquals(activeSocket, listener))
                {
                    activeSocket = null;
                }
            }

            TryDisposeTransportHandle(listener);
            endpointOwnership.Cleanup();
        }
    }

    private async Task AwaitActiveConnectionsAsync (TimeSpan connectionDrainTimeout)
    {
        await activeConnections.DrainAsync(connectionDrainTimeout).ConfigureAwait(false);
        ThrowIfFatalConnectionException();
    }

    private void RecordFatalConnectionException (Exception exception)
    {
        lock (syncRoot)
        {
            fatalConnectionException ??= ExceptionDispatchInfo.Capture(exception);
        }

        Release();
    }

    private void ThrowIfFatalConnectionException ()
    {
        ExceptionDispatchInfo? capturedException;
        lock (syncRoot)
        {
            capturedException = fatalConnectionException;
        }

        capturedException?.Throw();
    }

    private void TryDisposeTransportHandle (IDisposable? transportHandle)
    {
        if (transportHandle is null)
        {
            return;
        }

        try
        {
            transportHandle.Dispose();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or SocketException or ObjectDisposedException or InvalidOperationException)
        {
        }
        catch (Exception exception)
        {
            RecordFatalConnectionException(exception);
        }
    }
}
