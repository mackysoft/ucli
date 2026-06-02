using System.Buffers;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Host;

/// <summary> Owns caller-disconnect cancellation for supervisor IPC handlers. </summary>
internal sealed class SupervisorRequestLifetime : IAsyncDisposable
{
    private readonly CancellationTokenSource disconnectCancellationTokenSource;

    private readonly CancellationTokenSource monitorCancellationTokenSource;

    private readonly CancellationTokenSource requestCancellationTokenSource;

    private readonly Task disconnectMonitorTask;

    private readonly CancellationToken listenerCancellationToken;

    private SupervisorRequestLifetime (
        CancellationTokenSource disconnectCancellationTokenSource,
        CancellationTokenSource monitorCancellationTokenSource,
        CancellationTokenSource requestCancellationTokenSource,
        Task disconnectMonitorTask,
        CancellationToken listenerCancellationToken)
    {
        this.disconnectCancellationTokenSource = disconnectCancellationTokenSource ?? throw new ArgumentNullException(nameof(disconnectCancellationTokenSource));
        this.monitorCancellationTokenSource = monitorCancellationTokenSource ?? throw new ArgumentNullException(nameof(monitorCancellationTokenSource));
        this.requestCancellationTokenSource = requestCancellationTokenSource ?? throw new ArgumentNullException(nameof(requestCancellationTokenSource));
        this.disconnectMonitorTask = disconnectMonitorTask ?? throw new ArgumentNullException(nameof(disconnectMonitorTask));
        this.listenerCancellationToken = listenerCancellationToken;
    }

    /// <summary> Gets the request-scoped cancellation token. </summary>
    public CancellationToken CancellationToken => requestCancellationTokenSource.Token;

    /// <summary> Gets a value indicating whether the request was canceled because the caller disconnected. </summary>
    public bool IsCallerDisconnectCancellation =>
        disconnectCancellationTokenSource.IsCancellationRequested
        && !listenerCancellationToken.IsCancellationRequested;

    /// <summary> Cancels the request because the caller-owned response stream failed. </summary>
    public void CancelForResponseStreamFailure ()
    {
        TryCancel(disconnectCancellationTokenSource);
    }

    /// <summary> Starts one request lifetime bound to the specified client stream. </summary>
    /// <param name="stream"> The request stream to monitor for caller disconnect. </param>
    /// <param name="listenerCancellationToken"> The host listener cancellation token. </param>
    /// <returns> The started request lifetime. </returns>
    public static SupervisorRequestLifetime Start (
        Stream stream,
        CancellationToken listenerCancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var disconnectCancellationTokenSource = new CancellationTokenSource();
        var monitorCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(listenerCancellationToken);
        var requestCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            listenerCancellationToken,
            disconnectCancellationTokenSource.Token);
        var disconnectMonitorTask = MonitorCallerDisconnectAsync(
            stream,
            disconnectCancellationTokenSource,
            monitorCancellationTokenSource.Token);
        return new SupervisorRequestLifetime(
            disconnectCancellationTokenSource,
            monitorCancellationTokenSource,
            requestCancellationTokenSource,
            disconnectMonitorTask,
            listenerCancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync ()
    {
        monitorCancellationTokenSource.Cancel();
        requestCancellationTokenSource.Cancel();

        try
        {
            await disconnectMonitorTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        disconnectCancellationTokenSource.Dispose();
        monitorCancellationTokenSource.Dispose();
        requestCancellationTokenSource.Dispose();
    }

    private static async Task MonitorCallerDisconnectAsync (
        Stream stream,
        CancellationTokenSource disconnectCancellationTokenSource,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(1);
        try
        {
            while (!cancellationToken.IsCancellationRequested
                   && !disconnectCancellationTokenSource.IsCancellationRequested)
            {
                var bytesRead = await stream.ReadAsync(
                        buffer.AsMemory(0, 1),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    TryCancel(disconnectCancellationTokenSource);
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ObjectDisposedException)
        {
            TryCancel(disconnectCancellationTokenSource);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void TryCancel (CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            cancellationTokenSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
