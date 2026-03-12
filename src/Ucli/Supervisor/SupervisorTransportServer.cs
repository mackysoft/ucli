using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Supervisor;

/// <summary> Owns the supervisor IPC listener transport loop for one host instance. </summary>
internal sealed class SupervisorTransportServer
{
    private readonly object syncRoot = new();

    private readonly ConcurrentDictionary<int, Task> activeConnectionTasks = new();

    private NamedPipeServerStream? activePipeStream;

    private Socket? activeSocket;

    private ExceptionDispatchInfo? fatalConnectionException;

    private int nextConnectionId;

    /// <summary> Runs the listener loop for the specified endpoint until cancellation is requested. </summary>
    /// <param name="endpoint"> The listener endpoint. </param>
    /// <param name="connectionHandler"> The per-connection request handler. </param>
    /// <param name="onStarted"> The callback invoked after the listener becomes ready. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the host. </param>
    public async Task Run (
        IpcEndpoint endpoint,
        Func<Stream, CancellationToken, Task> connectionHandler,
        Func<CancellationToken, Task> onStarted,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(connectionHandler);
        ArgumentNullException.ThrowIfNull(onStarted);

        if (endpoint.TransportKind == IpcTransportKind.NamedPipe)
        {
            try
            {
                await RunNamedPipe(endpoint.Address, connectionHandler, onStarted, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await AwaitActiveConnections().ConfigureAwait(false);
            }

            return;
        }

        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            try
            {
                await RunUnixSocket(endpoint.Address, connectionHandler, onStarted, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await AwaitActiveConnections().ConfigureAwait(false);
            }

            return;
        }

        throw new InvalidOperationException($"Unsupported supervisor IPC transport kind: {endpoint.TransportKind}.");
    }

    /// <summary> Releases any actively bound transport resources. </summary>
    public void Release ()
    {
        lock (syncRoot)
        {
            activePipeStream?.Dispose();
            activePipeStream = null;

            activeSocket?.Dispose();
            activeSocket = null;
        }
    }

    private async Task RunNamedPipe (
        string address,
        Func<Stream, CancellationToken, Task> connectionHandler,
        Func<CancellationToken, Task> onStarted,
        CancellationToken cancellationToken)
    {
        var started = false;
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
                    PipeOptions.Asynchronous);

                lock (syncRoot)
                {
                    activePipeStream = serverStream;
                }

                if (!started)
                {
                    await onStarted(cancellationToken).ConfigureAwait(false);
                    started = true;
                }

                await serverStream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                lock (syncRoot)
                {
                    if (ReferenceEquals(activePipeStream, serverStream))
                    {
                        activePipeStream = null;
                    }
                }

                TrackConnection(serverStream, connectionHandler, cancellationToken);
                serverStream = null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested && exception is IOException or InvalidDataException or ObjectDisposedException)
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

                serverStream?.Dispose();
            }
        }
    }

    private async Task RunUnixSocket (
        string address,
        Func<Stream, CancellationToken, Task> connectionHandler,
        Func<CancellationToken, Task> onStarted,
        CancellationToken cancellationToken)
    {
        var socketDirectoryPath = Path.GetDirectoryName(address);
        if (!string.IsNullOrWhiteSpace(socketDirectoryPath))
        {
            UcliLocalStorageBootstrapper.EnsureInitialized(socketDirectoryPath);
            Directory.CreateDirectory(socketDirectoryPath);
        }

        if (File.Exists(address))
        {
            File.Delete(address);
        }

        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(address));
        listener.Listen(8);

        lock (syncRoot)
        {
            activeSocket = listener;
        }

        await onStarted(cancellationToken).ConfigureAwait(false);

        try
        {
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
                    TrackConnection(networkStream, connectionHandler, cancellationToken);
                    networkStream = null;
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
                    networkStream?.Dispose();
                    acceptedSocket?.Dispose();
                }
            }
        }
        finally
        {
            lock (syncRoot)
            {
                if (ReferenceEquals(activeSocket, listener))
                {
                    activeSocket = null;
                }
            }

            if (File.Exists(address))
            {
                File.Delete(address);
            }
        }
    }

    private void TrackConnection (
        Stream stream,
        Func<Stream, CancellationToken, Task> connectionHandler,
        CancellationToken cancellationToken)
    {
        var connectionId = Interlocked.Increment(ref nextConnectionId);
        var connectionTask = HandleConnection(stream, connectionHandler, cancellationToken);
        activeConnectionTasks.TryAdd(connectionId, connectionTask);
        _ = connectionTask.ContinueWith(
            _ =>
            {
                Task? ignoredTask;
                activeConnectionTasks.TryRemove(connectionId, out ignoredTask);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task HandleConnection (
        Stream stream,
        Func<Stream, CancellationToken, Task> connectionHandler,
        CancellationToken cancellationToken)
    {
        using var ownedStream = stream;
        try
        {
            await connectionHandler(ownedStream, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or SocketException or ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            RecordFatalConnectionException(exception);
            throw;
        }
    }

    private async Task AwaitActiveConnections ()
    {
        var connectionTasks = activeConnectionTasks.Values.ToArray();
        if (connectionTasks.Length == 0)
        {
            ThrowIfFatalConnectionException();
            return;
        }

        await Task.WhenAll(connectionTasks).ConfigureAwait(false);
        ThrowIfFatalConnectionException();
    }

    private void RecordFatalConnectionException (Exception exception)
    {
        lock (syncRoot)
        {
            fatalConnectionException ??= ExceptionDispatchInfo.Capture(exception);

            activePipeStream?.Dispose();
            activePipeStream = null;

            activeSocket?.Dispose();
            activeSocket = null;
        }
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
}