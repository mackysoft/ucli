using System;
using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Monitors the request deadline until response completion and the parent process until oneshot termination. </summary>
    internal sealed class OneshotProcessLifetimeWatchdog : IDisposable
    {
        private const int RequestDeadlineMonitoringState = 0;

        private const int ParentOnlyMonitoringState = 1;

        private const int ExitRequestedState = 2;

        private const int DisposedState = 3;

        private const int FailureExitCode = 1;

        private static readonly TimeSpan ProductionPollInterval = TimeSpan.FromMilliseconds(250);

        private readonly string storageRoot;

        private readonly IpcOneshotBootstrapEnvelope bootstrapEnvelope;

        private readonly Func<int, DateTimeOffset, bool> parentProcessIsSameProcess;

        private readonly TimeSpan monotonicRequestExitDeadline;

        private readonly IMonotonicClock monotonicClock;

        private readonly Func<string, IpcOneshotBootstrapEnvelope, bool> tryDeleteEnvelopeIfOwned;

        private readonly Action<int> processExit;

        private readonly Timer timer;

        private int lifetimeState;

        internal OneshotProcessLifetimeWatchdog (
            string storageRoot,
            IpcOneshotBootstrapEnvelope bootstrapEnvelope,
            TimeSpan pollInterval,
            Func<int, DateTimeOffset, bool> parentProcessIsSameProcess,
            DateTimeOffset observedUtcNow,
            IMonotonicClock monotonicClock,
            Func<string, IpcOneshotBootstrapEnvelope, bool> tryDeleteEnvelopeIfOwned,
            Action<int> processExit)
        {
            if (string.IsNullOrWhiteSpace(storageRoot))
            {
                throw new ArgumentException("Storage root must not be empty.", nameof(storageRoot));
            }

            if (pollInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pollInterval),
                    pollInterval,
                    "Poll interval must be greater than zero.");
            }

            this.storageRoot = storageRoot;
            this.bootstrapEnvelope = bootstrapEnvelope ?? throw new ArgumentNullException(nameof(bootstrapEnvelope));
            this.parentProcessIsSameProcess = parentProcessIsSameProcess ?? throw new ArgumentNullException(nameof(parentProcessIsSameProcess));
            if (observedUtcNow == default || observedUtcNow.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Observed UTC time must be a non-default UTC timestamp.",
                    nameof(observedUtcNow));
            }

            this.monotonicClock = monotonicClock ?? throw new ArgumentNullException(nameof(monotonicClock));
            this.tryDeleteEnvelopeIfOwned = tryDeleteEnvelopeIfOwned ?? throw new ArgumentNullException(nameof(tryDeleteEnvelopeIfOwned));
            this.processExit = processExit ?? throw new ArgumentNullException(nameof(processExit));

            var monotonicNow = monotonicClock.Elapsed;
            if (monotonicNow < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(monotonicClock),
                    monotonicNow,
                    "Monotonic clock elapsed time must not be negative.");
            }

            var remainingTime = bootstrapEnvelope.ExitDeadlineUtc - observedUtcNow;
            monotonicRequestExitDeadline = remainingTime > TimeSpan.Zero
                ? monotonicNow + remainingTime
                : monotonicNow;
            timer = new Timer(
                static state => ((OneshotProcessLifetimeWatchdog)state).InspectLifetime(),
                this,
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);
            try
            {
                timer.Change(TimeSpan.Zero, pollInterval);
            }
            catch
            {
                timer.Dispose();
                throw;
            }
        }

        /// <summary> Starts production lifetime monitoring on a worker-thread timer. </summary>
        /// <param name="storageRoot"> The storage root containing the bootstrap envelope. </param>
        /// <param name="bootstrapEnvelope"> The exact bootstrap generation owned by the oneshot process. </param>
        /// <returns> The active watchdog. </returns>
        internal static OneshotProcessLifetimeWatchdog Start (
            string storageRoot,
            IpcOneshotBootstrapEnvelope bootstrapEnvelope)
        {
            var monotonicClock = new StopwatchMonotonicClock();
            return new OneshotProcessLifetimeWatchdog(
                storageRoot,
                bootstrapEnvelope,
                ProductionPollInterval,
                ProcessLivenessProbe.IsSameProcess,
                DateTimeOffset.UtcNow,
                monotonicClock,
                OneshotBootstrapEnvelopeStore.TryDeleteIfOwned,
                static exitCode => Environment.Exit(exitCode));
        }

        /// <summary> Stops enforcing the request deadline after a terminal response while retaining parent-process monitoring. </summary>
        internal void MarkRequestCompleted ()
        {
            while (true)
            {
                var observedState = Volatile.Read(ref lifetimeState);
                if (observedState != RequestDeadlineMonitoringState)
                {
                    return;
                }

                if (Interlocked.CompareExchange(
                        ref lifetimeState,
                        ParentOnlyMonitoringState,
                        RequestDeadlineMonitoringState) == RequestDeadlineMonitoringState)
                {
                    return;
                }
            }
        }

        /// <inheritdoc />
        public void Dispose ()
        {
            while (true)
            {
                var observedState = Volatile.Read(ref lifetimeState);
                if (observedState == ExitRequestedState
                    || observedState == DisposedState)
                {
                    return;
                }

                if (Interlocked.CompareExchange(
                        ref lifetimeState,
                        DisposedState,
                        observedState) != observedState)
                {
                    continue;
                }

                timer.Dispose();
                return;
            }
        }

        private void InspectLifetime ()
        {
            while (true)
            {
                var observedState = Volatile.Read(ref lifetimeState);
                switch (observedState)
                {
                    case RequestDeadlineMonitoringState:
                        if (monotonicClock.Elapsed < monotonicRequestExitDeadline
                            && parentProcessIsSameProcess(
                                bootstrapEnvelope.ParentProcessId,
                                bootstrapEnvelope.ParentProcessStartedAtUtc))
                        {
                            return;
                        }

                        break;

                    case ParentOnlyMonitoringState:
                        if (parentProcessIsSameProcess(
                            bootstrapEnvelope.ParentProcessId,
                            bootstrapEnvelope.ParentProcessStartedAtUtc))
                        {
                            return;
                        }

                        break;

                    case ExitRequestedState:
                    case DisposedState:
                        return;

                    default:
                        throw new InvalidOperationException($"Unsupported oneshot watchdog state: {observedState}.");
                }

                if (Interlocked.CompareExchange(
                        ref lifetimeState,
                        ExitRequestedState,
                        observedState) != observedState)
                {
                    continue;
                }

                timer.Dispose();
                try
                {
                    tryDeleteEnvelopeIfOwned(storageRoot, bootstrapEnvelope);
                }
                catch (Exception)
                {
                    // NOTE: Process termination remains fail-closed when best-effort envelope cleanup cannot complete.
                }

                processExit(FailureExitCode);
                return;
            }
        }
    }
}
