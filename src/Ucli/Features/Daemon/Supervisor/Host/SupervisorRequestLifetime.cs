using System.Buffers;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Host;

/// <summary> Owns caller-disconnect cancellation and deferred cleanup for one supervisor IPC request. </summary>
internal sealed class SupervisorRequestLifetime
{
    private readonly object cancellationSyncRoot = new();

    private readonly CancellationTokenSource monitorCancellationTokenSource = new();

    private readonly CancellationTokenSource requestCancellationTokenSource = new();

    private readonly Task disconnectMonitorTask;

    private readonly CancellationToken listenerCancellationToken;

    private readonly CancellationTokenRegistration listenerCancellationRegistration;

    private Task? cancellationTask;

    private int callerDisconnectCancellationRequested;

    private int releaseRequested;

    private SupervisorRequestLifetime (
        Stream stream,
        CancellationToken listenerCancellationToken)
    {
        this.listenerCancellationToken = listenerCancellationToken;
        listenerCancellationRegistration = listenerCancellationToken.Register(
            static state => ((SupervisorRequestLifetime)state!).RequestCancellation(),
            this);
        disconnectMonitorTask = Task.Run(
            () => MonitorCallerDisconnectAsync(
                stream,
                this,
                monitorCancellationTokenSource.Token),
            CancellationToken.None);
        ObserveFault(disconnectMonitorTask);
    }

    /// <summary> Gets the request-scoped cancellation token. </summary>
    public CancellationToken CancellationToken => requestCancellationTokenSource.Token;

    /// <summary> Gets a value indicating whether the request was canceled because the caller disconnected. </summary>
    public bool IsCallerDisconnectCancellation =>
        Volatile.Read(ref callerDisconnectCancellationRequested) != 0
        && !listenerCancellationToken.IsCancellationRequested;

    /// <summary> Cancels the request because the caller-owned response stream failed without running callbacks inline. </summary>
    public void CancelForResponseStreamFailure ()
    {
        MarkCallerDisconnectAndRequestCancellation();
    }

    /// <summary> Starts one request lifetime bound to the specified client stream. </summary>
    /// <param name="stream"> The request stream to monitor for caller disconnect. </param>
    /// <param name="listenerCancellationToken"> The host listener cancellation token. </param>
    /// <returns> The started request lifetime. </returns>
    public static SupervisorRequestLifetime Start (
        Stream stream,
        CancellationToken listenerCancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new SupervisorRequestLifetime(stream, listenerCancellationToken);
    }

    /// <summary> Requests cancellation and transfers cleanup to a deferred task owned by this lifetime. </summary>
    public void Release ()
    {
        if (Interlocked.Exchange(ref releaseRequested, 1) == 0)
        {
            var cleanupTask = CleanupAfterCancellationAsync(RequestCancellation());
            ObserveFault(cleanupTask);
        }
    }

    private void MarkCallerDisconnectAndRequestCancellation ()
    {
        Interlocked.Exchange(ref callerDisconnectCancellationRequested, 1);
        _ = RequestCancellation();
    }

    private Task RequestCancellation ()
    {
        lock (cancellationSyncRoot)
        {
            if (cancellationTask is null)
            {
                // NOTE: CancelAsync prevents synchronous callbacks registered by the request operation or
                // transport stream from running on the response writer, listener, or disconnect-monitor thread.
                var monitorCancellationTask = TryCancelAsync(monitorCancellationTokenSource);
                var requestCancellationTask = TryCancelAsync(requestCancellationTokenSource);
                cancellationTask = Task.WhenAll(
                    monitorCancellationTask,
                    requestCancellationTask);
                ObserveFault(cancellationTask);
            }

            return cancellationTask;
        }
    }

    private async Task CleanupAfterCancellationAsync (Task requestedCancellationTask)
    {
        try
        {
            await Task.WhenAll(
                    requestedCancellationTask,
                    disconnectMonitorTask)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            listenerCancellationRegistration.Dispose();
            monitorCancellationTokenSource.Dispose();
            requestCancellationTokenSource.Dispose();
        }
    }

    private static async Task MonitorCallerDisconnectAsync (
        Stream stream,
        SupervisorRequestLifetime requestLifetime,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(1);
        try
        {
            while (!cancellationToken.IsCancellationRequested
                   && !requestLifetime.IsCallerDisconnectCancellation)
            {
                var bytesRead = await stream.ReadAsync(
                        buffer.AsMemory(0, 1),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    requestLifetime.MarkCallerDisconnectAndRequestCancellation();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ObjectDisposedException)
        {
            requestLifetime.MarkCallerDisconnectAndRequestCancellation();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task TryCancelAsync (CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            await cancellationTokenSource.CancelAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (AggregateException)
        {
            // NOTE: Cancellation callbacks are request-owned; one callback failure must not block deferred cleanup.
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
}
