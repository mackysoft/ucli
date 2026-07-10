using System.Net.Sockets;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Transport;

/// <summary> Owns admission, handler lifetime, and transport cleanup for accepted supervisor connections. </summary>
internal sealed class SupervisorTransportConnectionGroup
{
    private readonly object syncRoot = new();

    private readonly Action<Stream> releaseTransportHandle;

    private readonly Action<Exception> recordFatalConnectionException;

    private readonly Dictionary<int, ActiveConnection> activeConnections = new();

    private bool isReleased;

    private int nextConnectionId;

    /// <summary> Initializes a new instance of the <see cref="SupervisorTransportConnectionGroup" /> class. </summary>
    /// <param name="releaseTransportHandle"> The non-throwing transport-handle cleanup dependency. </param>
    /// <param name="recordFatalConnectionException"> The callback that records a connection failure that must stop the listener. </param>
    public SupervisorTransportConnectionGroup (
        Action<Stream> releaseTransportHandle,
        Action<Exception> recordFatalConnectionException)
    {
        this.releaseTransportHandle = releaseTransportHandle ?? throw new ArgumentNullException(nameof(releaseTransportHandle));
        this.recordFatalConnectionException = recordFatalConnectionException ?? throw new ArgumentNullException(nameof(recordFatalConnectionException));
    }

    /// <summary> Attempts to admit and start one accepted connection without running its handler on the listener loop. </summary>
    /// <param name="stream"> The accepted transport stream. </param>
    /// <param name="connectionHandler"> The per-connection request handler. </param>
    /// <param name="maximumActiveConnections"> The maximum number of accepted connections whose handler or transport cleanup has not completed. </param>
    /// <param name="cancellationToken"> The listener lifecycle cancellation token. </param>
    /// <returns> <see langword="true" /> when ownership was accepted; otherwise, <see langword="false" /> and the caller retains ownership. </returns>
    public bool TryStart (
        Stream stream,
        Func<Stream, CancellationToken, Task> connectionHandler,
        int maximumActiveConnections,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(connectionHandler);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumActiveConnections, 0);

        ActiveConnection activeConnection;
        int connectionId;
        lock (syncRoot)
        {
            if (isReleased
                || cancellationToken.IsCancellationRequested
                || activeConnections.Count >= maximumActiveConnections)
            {
                return false;
            }

            connectionId = ++nextConnectionId;
            activeConnection = new ActiveConnection(stream, releaseTransportHandle);
            activeConnections.Add(connectionId, activeConnection);
        }

        var connectionTask = Task.Run(
            () => HandleConnectionAsync(
                connectionId,
                activeConnection,
                connectionHandler,
                cancellationToken),
            CancellationToken.None);
        ObserveFault(connectionTask);
        return true;
    }

    /// <summary> Prevents further admission and starts asynchronous cleanup for every tracked transport handle. </summary>
    public void Release ()
    {
        ActiveConnection[] connections;
        lock (syncRoot)
        {
            isReleased = true;
            connections = activeConnections.Values.ToArray();
        }

        foreach (var connection in connections)
        {
            _ = connection.BeginTransportReleaseAsync();
        }
    }

    /// <summary> Waits up to the specified duration for currently tracked handlers and transport cleanup to finish. </summary>
    /// <param name="drainTimeout"> The upper bound for waiting for connection quiescence. </param>
    public async Task DrainAsync (TimeSpan drainTimeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(drainTimeout, TimeSpan.Zero);

        Task[] connectionTasks;
        lock (syncRoot)
        {
            connectionTasks = activeConnections.Values
                .Select(static connection => connection.WaitForRemovalAsync())
                .ToArray();
        }

        if (connectionTasks.Length == 0)
        {
            return;
        }

        var completionTask = Task.WhenAll(connectionTasks);
        using var timeoutCancellationTokenSource = new CancellationTokenSource();
        var timeoutTask = Task.Delay(drainTimeout, timeoutCancellationTokenSource.Token);
        var completedTask = await Task.WhenAny(completionTask, timeoutTask).ConfigureAwait(false);
        if (!ReferenceEquals(completedTask, completionTask) && !completionTask.IsCompleted)
        {
            ObserveFault(completionTask);
            return;
        }

        timeoutCancellationTokenSource.Cancel();
        await completionTask.ConfigureAwait(false);
    }

    private async Task HandleConnectionAsync (
        int connectionId,
        ActiveConnection activeConnection,
        Func<Stream, CancellationToken, Task> connectionHandler,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await connectionHandler(activeConnection.Stream, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or SocketException or ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            recordFatalConnectionException(exception);
            throw;
        }
        finally
        {
            lock (syncRoot)
            {
                activeConnection.CompleteHandler();
            }

            var quiescenceTask = activeConnection.BeginQuiescenceAsync();
            ObserveFault(RemoveConnectionAfterQuiescenceAsync(
                connectionId,
                activeConnection,
                quiescenceTask));
        }
    }

    private async Task RemoveConnectionAfterQuiescenceAsync (
        int connectionId,
        ActiveConnection activeConnection,
        Task quiescenceTask)
    {
        try
        {
            await quiescenceTask.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            recordFatalConnectionException(exception);
        }
        finally
        {
            lock (syncRoot)
            {
                if (activeConnections.TryGetValue(connectionId, out var trackedConnection)
                    && ReferenceEquals(trackedConnection, activeConnection))
                {
                    activeConnections.Remove(connectionId);
                }
            }

            activeConnection.CompleteRemoval();
        }
    }

    private static void ObserveFault (Task task)
    {
        _ = task.ContinueWith(
            static completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private sealed class ActiveConnection
    {
        private readonly object syncRoot = new();

        private readonly Action<Stream> releaseTransportHandle;

        private readonly TaskCompletionSource handlerCompletionSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource removalCompletionSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private Task? transportReleaseTask;

        private Task? quiescenceTask;

        public ActiveConnection (
            Stream stream,
            Action<Stream> releaseTransportHandle)
        {
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
            this.releaseTransportHandle = releaseTransportHandle ?? throw new ArgumentNullException(nameof(releaseTransportHandle));
        }

        public Stream Stream { get; }

        public Task BeginTransportReleaseAsync ()
        {
            lock (syncRoot)
            {
                transportReleaseTask ??= Task.Run(
                    () => releaseTransportHandle(Stream),
                    CancellationToken.None);
                return transportReleaseTask;
            }
        }

        public Task BeginQuiescenceAsync ()
        {
            var releaseTask = BeginTransportReleaseAsync();
            lock (syncRoot)
            {
                quiescenceTask ??= Task.WhenAll(
                    handlerCompletionSource.Task,
                    releaseTask);
                return quiescenceTask;
            }
        }

        public void CompleteHandler ()
        {
            handlerCompletionSource.TrySetResult();
        }

        public Task WaitForRemovalAsync ()
        {
            return removalCompletionSource.Task;
        }

        public void CompleteRemoval ()
        {
            removalCompletionSource.TrySetResult();
        }
    }
}
