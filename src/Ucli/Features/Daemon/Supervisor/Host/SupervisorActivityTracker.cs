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
using MackySoft.Ucli.Application.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
namespace MackySoft.Ucli.Features.Daemon.Supervisor.Host;

/// <summary> Tracks request activity and idle timing for one supervisor host instance. </summary>
internal sealed class SupervisorActivityTracker
{
    private int activeRequestCount;

    private long lastActivityUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary> Gets a value indicating whether one supervisor request is currently being processed. </summary>
    public bool HasActiveRequests => Volatile.Read(ref activeRequestCount) > 0;

    /// <summary> Records one supervisor request scope and keeps the host active until disposal. </summary>
    /// <returns> One disposable request-activity scope. </returns>
    public IDisposable BeginRequest ()
    {
        Touch();
        Interlocked.Increment(ref activeRequestCount);
        return new RequestScope(this);
    }

    /// <summary> Updates the last observed activity timestamp to the current UTC time. </summary>
    public void Touch ()
    {
        Volatile.Write(ref lastActivityUnixTimeMilliseconds, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    /// <summary> Determines whether the host has been idle for the specified delay. </summary>
    /// <param name="idleDelay"> The idle-delay threshold. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <returns> <see langword="true" /> when the idle delay has elapsed without activity; otherwise <see langword="false" />. </returns>
    public bool IsIdle (TimeSpan idleDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idleDelay, TimeSpan.Zero);

        var lastActivity = DateTimeOffset.FromUnixTimeMilliseconds(
            Volatile.Read(ref lastActivityUnixTimeMilliseconds));
        return DateTimeOffset.UtcNow - lastActivity >= idleDelay;
    }

    private void EndRequest ()
    {
        Interlocked.Decrement(ref activeRequestCount);
    }

    private sealed class RequestScope : IDisposable
    {
        private readonly SupervisorActivityTracker tracker;

        private int disposed;

        public RequestScope (SupervisorActivityTracker tracker)
        {
            this.tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        }

        public void Dispose ()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            tracker.EndRequest();
        }
    }
}
