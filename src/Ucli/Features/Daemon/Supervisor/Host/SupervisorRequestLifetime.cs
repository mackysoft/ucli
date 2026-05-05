using System.Buffers;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Host;

/// <summary> Owns per-request timeout and caller-disconnect cancellation for supervisor IPC handlers. </summary>
internal sealed class SupervisorRequestLifetime : IAsyncDisposable
{
    private readonly CancellationTokenSource timeoutCancellationTokenSource;

    private readonly CancellationTokenSource disconnectCancellationTokenSource;

    private readonly CancellationTokenSource monitorCancellationTokenSource;

    private readonly CancellationTokenSource requestCancellationTokenSource;

    private readonly Task disconnectMonitorTask;

    private readonly CancellationToken listenerCancellationToken;

    private SupervisorRequestLifetime (
        CancellationTokenSource timeoutCancellationTokenSource,
        CancellationTokenSource disconnectCancellationTokenSource,
        CancellationTokenSource monitorCancellationTokenSource,
        CancellationTokenSource requestCancellationTokenSource,
        Task disconnectMonitorTask,
        CancellationToken listenerCancellationToken)
    {
        this.timeoutCancellationTokenSource = timeoutCancellationTokenSource ?? throw new ArgumentNullException(nameof(timeoutCancellationTokenSource));
        this.disconnectCancellationTokenSource = disconnectCancellationTokenSource ?? throw new ArgumentNullException(nameof(disconnectCancellationTokenSource));
        this.monitorCancellationTokenSource = monitorCancellationTokenSource ?? throw new ArgumentNullException(nameof(monitorCancellationTokenSource));
        this.requestCancellationTokenSource = requestCancellationTokenSource ?? throw new ArgumentNullException(nameof(requestCancellationTokenSource));
        this.disconnectMonitorTask = disconnectMonitorTask ?? throw new ArgumentNullException(nameof(disconnectMonitorTask));
        this.listenerCancellationToken = listenerCancellationToken;
    }

    /// <summary> Gets the request-scoped cancellation token. </summary>
    public CancellationToken CancellationToken => requestCancellationTokenSource.Token;

    /// <summary> Gets a value indicating whether the request was canceled by its timeout budget. </summary>
    public bool IsTimeoutCancellation =>
        timeoutCancellationTokenSource.IsCancellationRequested
        && !listenerCancellationToken.IsCancellationRequested
        && !disconnectCancellationTokenSource.IsCancellationRequested;

    /// <summary> Gets a value indicating whether the request was canceled because the caller disconnected. </summary>
    public bool IsCallerDisconnectCancellation =>
        disconnectCancellationTokenSource.IsCancellationRequested
        && !listenerCancellationToken.IsCancellationRequested;

    /// <summary> Starts one request lifetime bound to the specified timeout and client stream. </summary>
    /// <param name="stream"> The request stream to monitor for caller disconnect. </param>
    /// <param name="timeout"> The request timeout. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="listenerCancellationToken"> The host listener cancellation token. </param>
    /// <returns> The started request lifetime. </returns>
    public static SupervisorRequestLifetime Start (
        Stream stream,
        TimeSpan timeout,
        CancellationToken listenerCancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
        var disconnectCancellationTokenSource = new CancellationTokenSource();
        var monitorCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(listenerCancellationToken);
        var requestCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            listenerCancellationToken,
            timeoutCancellationTokenSource.Token,
            disconnectCancellationTokenSource.Token);
        var disconnectMonitorTask = MonitorCallerDisconnect(
            stream,
            disconnectCancellationTokenSource,
            monitorCancellationTokenSource.Token);
        return new SupervisorRequestLifetime(
            timeoutCancellationTokenSource,
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

        timeoutCancellationTokenSource.Dispose();
        disconnectCancellationTokenSource.Dispose();
        monitorCancellationTokenSource.Dispose();
        requestCancellationTokenSource.Dispose();
    }

    private static async Task MonitorCallerDisconnect (
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
