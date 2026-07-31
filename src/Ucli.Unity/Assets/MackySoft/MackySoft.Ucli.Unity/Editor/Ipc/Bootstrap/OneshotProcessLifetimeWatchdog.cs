using System;
using System.Threading;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Monitors the active oneshot process contract until normal completion or hard exit.
    /// </summary>
    internal sealed class OneshotProcessLifetimeWatchdog :
        IDisposable,
        ILifecycleExecutionHostLifetimeObserver
    {
        private const int RequestDeadlineMonitoringState = 0;

        private const int LifecycleExecutionHardExitMonitoringState = 1;

        private const int ParentOnlyMonitoringState = 2;

        private const int ExitRequestedState = 3;

        private const int DisposedState = 4;

        private static readonly TimeSpan ProductionPollInterval = TimeSpan.FromMilliseconds(250);

        private readonly AbsolutePath storageRoot;

        private readonly IpcOneshotBootstrapEnvelope bootstrapEnvelope;

        private readonly Func<ProcessIdentity, bool> parentProcessIsSameProcess;

        private readonly DateTimeOffset observedUtcAtConstruction;

        private readonly TimeSpan monotonicElapsedAtConstruction;

        private readonly TimeSpan monotonicBootstrapExitDeadline;

        private readonly IMonotonicClock monotonicClock;

        private readonly Func<AbsolutePath, IpcOneshotBootstrapEnvelope, bool> tryDeleteEnvelopeIfOwned;

        private readonly Action terminateProcess;

        private readonly Timer timer;

        private int lifetimeState;

        internal OneshotProcessLifetimeWatchdog (
            AbsolutePath storageRoot,
            IpcOneshotBootstrapEnvelope bootstrapEnvelope,
            TimeSpan pollInterval,
            Func<ProcessIdentity, bool> parentProcessIsSameProcess,
            DateTimeOffset observedUtcNow,
            IMonotonicClock monotonicClock,
            Func<AbsolutePath, IpcOneshotBootstrapEnvelope, bool> tryDeleteEnvelopeIfOwned,
            Action terminateProcess)
        {
            if (storageRoot == null)
            {
                throw new ArgumentNullException(nameof(storageRoot));
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

            observedUtcAtConstruction = observedUtcNow;
            this.monotonicClock = monotonicClock ?? throw new ArgumentNullException(nameof(monotonicClock));
            this.tryDeleteEnvelopeIfOwned = tryDeleteEnvelopeIfOwned ?? throw new ArgumentNullException(nameof(tryDeleteEnvelopeIfOwned));
            this.terminateProcess = terminateProcess ?? throw new ArgumentNullException(nameof(terminateProcess));

            var monotonicNow = monotonicClock.Elapsed;
            if (monotonicNow < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(monotonicClock),
                    monotonicNow,
                    "Monotonic clock elapsed time must not be negative.");
            }

            monotonicElapsedAtConstruction = monotonicNow;
            monotonicBootstrapExitDeadline = ResolveMonotonicDeadline(
                bootstrapEnvelope.ExitDeadlineUtc);
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
            AbsolutePath storageRoot,
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
                static () =>
                {
                    using (var process = System.Diagnostics.Process.GetCurrentProcess())
                    {
                        process.Kill();
                    }
                });
        }

        /// <summary>
        /// Stops parent-process monitoring after durable Lifecycle Execution registration and enforces only
        /// the bootstrap hard-exit deadline fixed by the caller.
        /// </summary>
        /// <param name="deadlineUtc"> The immutable Lifecycle Execution deadline. </param>
        internal void MarkLifecycleExecutionStarted (DateTimeOffset deadlineUtc)
        {
            if (deadlineUtc == default || deadlineUtc.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Lifecycle Execution deadline must be a non-default UTC timestamp.",
                    nameof(deadlineUtc));
            }

            if (deadlineUtc > bootstrapEnvelope.ExitDeadlineUtc)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deadlineUtc),
                    deadlineUtc,
                    "Lifecycle Execution deadline must not exceed the oneshot bootstrap hard-exit deadline.");
            }

            _ = Interlocked.CompareExchange(
                ref lifetimeState,
                LifecycleExecutionHardExitMonitoringState,
                RequestDeadlineMonitoringState);
        }

        /// <inheritdoc />
        void ILifecycleExecutionHostLifetimeObserver.OnStartAccepted (
            DateTimeOffset deadlineUtc)
        {
            MarkLifecycleExecutionStarted(deadlineUtc);
        }

        /// <summary> Transitions a terminal request to parent-process monitoring while oneshot cleanup completes. </summary>
        internal void MarkRequestCompleted ()
        {
            while (true)
            {
                var observedState = Volatile.Read(ref lifetimeState);
                if (observedState != RequestDeadlineMonitoringState
                    && observedState != LifecycleExecutionHardExitMonitoringState)
                {
                    return;
                }

                if (Interlocked.CompareExchange(
                        ref lifetimeState,
                        ParentOnlyMonitoringState,
                        observedState) == observedState)
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
                        if (monotonicClock.Elapsed < monotonicBootstrapExitDeadline
                            && parentProcessIsSameProcess(bootstrapEnvelope.ParentProcess))
                        {
                            return;
                        }

                        break;

                    case LifecycleExecutionHardExitMonitoringState:
                        if (monotonicClock.Elapsed
                            < monotonicBootstrapExitDeadline)
                        {
                            return;
                        }

                        break;

                    case ParentOnlyMonitoringState:
                        if (parentProcessIsSameProcess(bootstrapEnvelope.ParentProcess))
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

                terminateProcess();
                return;
            }
        }

        private TimeSpan ResolveMonotonicDeadline (DateTimeOffset deadlineUtc)
        {
            var remainingTime = deadlineUtc - observedUtcAtConstruction;
            return remainingTime > TimeSpan.Zero
                ? monotonicElapsedAtConstruction + remainingTime
                : monotonicElapsedAtConstruction;
        }
    }
}
