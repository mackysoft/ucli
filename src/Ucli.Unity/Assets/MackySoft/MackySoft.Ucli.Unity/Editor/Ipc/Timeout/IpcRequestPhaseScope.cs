using System;
using System.Diagnostics;
using System.Threading;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Owns the monotonic phase cutoffs and cancellation state for one IPC exchange. </summary>
    internal sealed class IpcRequestPhaseScope : IDisposable
    {
        private static readonly TimeSpan MaximumTimerInterval = TimeSpan.FromDays(1);

        private readonly object lifetimeSyncRoot = new object();

        private readonly CancellationTokenSource executionDeadlineCancellationTokenSource;

        private readonly CancellationTokenSource persistenceCutoffCancellationTokenSource;

        private readonly CancellationTokenSource writeCutoffCancellationTokenSource;

        private readonly CancellationTokenSource executionCancellationTokenSource;

        private readonly Stopwatch elapsedTime;

        private readonly Timer phaseTimer;

        private CancellationTokenRegistration additionalUpstreamCancellationRegistration;

        private int nextCutoff;

        private int activeTimerCallbackCount;

        private int disposeRequested;

        private bool additionalUpstreamAttached;

        private bool timerDisposed;

        /// <summary> Initializes a phase scope from one immutable plan. </summary>
        /// <param name="plan"> The ordered monotonic phase plan. </param>
        /// <param name="elapsedTime"> The running monotonic clock started no later than the UTC deadline observation used to create <paramref name="plan" />. </param>
        /// <param name="upstreamCancellationToken"> The method execution lifetime selected by connection policy. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="plan" /> or <paramref name="elapsedTime" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="elapsedTime" /> is not running. </exception>
        public IpcRequestPhaseScope (
            IpcRequestPhasePlan plan,
            Stopwatch elapsedTime,
            CancellationToken upstreamCancellationToken)
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            this.elapsedTime = elapsedTime ?? throw new ArgumentNullException(nameof(elapsedTime));
            if (!elapsedTime.IsRunning)
            {
                throw new ArgumentException(
                    "The request phase clock must already be running.",
                    nameof(elapsedTime));
            }

            executionDeadlineCancellationTokenSource = new CancellationTokenSource();
            persistenceCutoffCancellationTokenSource = new CancellationTokenSource();
            writeCutoffCancellationTokenSource = new CancellationTokenSource();
            executionCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                upstreamCancellationToken,
                executionDeadlineCancellationTokenSource.Token);
            ExecutionCancellation = new IpcRequestCancellation(
                executionCancellationTokenSource.Token,
                executionDeadlineCancellationTokenSource.Token,
                upstreamCancellationToken);
            phaseTimer = new Timer(
                static state => ((IpcRequestPhaseScope)state).AdvanceCutoffs(),
                this,
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);
            ScheduleNextCutoff();
        }

        /// <summary> Gets the immutable phase plan used by this scope. </summary>
        public IpcRequestPhasePlan Plan { get; }

        /// <summary> Gets cancellation state shared by authorization, main-thread queueing, and method execution. </summary>
        public IpcRequestCancellation ExecutionCancellation { get; }

        /// <summary> Gets the token canceled at the recoverable completion-persistence cutoff. </summary>
        public CancellationToken PersistenceCutoffToken =>
            persistenceCutoffCancellationTokenSource.Token;

        /// <summary> Gets the token canceled at the progress and terminal frame-write cutoff. </summary>
        public CancellationToken WriteCutoffToken => writeCutoffCancellationTokenSource.Token;

        /// <summary> Adds one execution upstream after endpoint validation has selected its connection-lifetime policy. </summary>
        /// <param name="upstreamCancellationToken"> The additional upstream token. </param>
        /// <exception cref="InvalidOperationException"> Thrown when an additional upstream was already attached or disposal has started. </exception>
        public void AttachExecutionUpstream (CancellationToken upstreamCancellationToken)
        {
            lock (lifetimeSyncRoot)
            {
                if (disposeRequested != 0)
                {
                    throw new InvalidOperationException("The request phase scope is being disposed.");
                }

                if (additionalUpstreamAttached)
                {
                    throw new InvalidOperationException("An additional execution upstream is already attached.");
                }

                additionalUpstreamAttached = true;
            }

            var registration = upstreamCancellationToken.Register(
                static state => ((IpcRequestPhaseScope)state).CancelFromAdditionalUpstream(),
                this);
            var disposeRegistration = false;
            lock (lifetimeSyncRoot)
            {
                if (disposeRequested != 0)
                {
                    disposeRegistration = true;
                }
                else
                {
                    additionalUpstreamCancellationRegistration = registration;
                }
            }

            if (disposeRegistration)
            {
                registration.Dispose();
            }
        }

        /// <summary> Stops future timer callbacks and releases callback-shared resources after active callbacks return. </summary>
        public void Dispose ()
        {
            lock (lifetimeSyncRoot)
            {
                if (disposeRequested != 0)
                {
                    return;
                }

                Volatile.Write(ref disposeRequested, 1);
            }

            // Timer.Dispose does not wait for a running callback. Keep callback-shared resources alive
            // until every callback that entered before this disposal request has returned.
            try
            {
                phaseTimer.Dispose();
            }
            finally
            {
                bool disposeOwnedResources;
                lock (lifetimeSyncRoot)
                {
                    timerDisposed = true;
                    disposeOwnedResources = activeTimerCallbackCount == 0;
                }

                if (disposeOwnedResources)
                {
                    DisposeOwnedResources();
                }
            }
        }

        private void AdvanceCutoffs ()
        {
            if (!TryBeginTimerCallback())
            {
                return;
            }

            try
            {
                while (Volatile.Read(ref disposeRequested) == 0)
                {
                    var cutoffIndex = Volatile.Read(ref nextCutoff);
                    if (cutoffIndex >= 3)
                    {
                        return;
                    }

                    var remaining = GetCutoff(cutoffIndex) - elapsedTime.Elapsed;
                    if (remaining > TimeSpan.Zero)
                    {
                        ChangeTimer(remaining);
                        return;
                    }

                    if (Interlocked.CompareExchange(
                            ref nextCutoff,
                            cutoffIndex + 1,
                            cutoffIndex) != cutoffIndex)
                    {
                        continue;
                    }

                    ScheduleCutoff(cutoffIndex + 1);
                    CancelCutoff(cutoffIndex);
                    return;
                }
            }
            finally
            {
                CompleteTimerCallback();
            }
        }

        private void ScheduleNextCutoff ()
        {
            if (Plan.ExecutionCutoff - elapsedTime.Elapsed <= TimeSpan.Zero)
            {
                AdvanceCutoffs();
                return;
            }

            ScheduleCutoff(0);
        }

        private void ScheduleCutoff (int cutoffIndex)
        {
            if (cutoffIndex >= 3)
            {
                return;
            }

            var remaining = GetCutoff(cutoffIndex) - elapsedTime.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                ChangeTimer(TimeSpan.Zero);
                return;
            }

            ChangeTimer(remaining);
        }

        private void ChangeTimer (TimeSpan remaining)
        {
            var dueTime = remaining <= MaximumTimerInterval
                ? remaining
                : MaximumTimerInterval;
            try
            {
                phaseTimer.Change(dueTime, Timeout.InfiniteTimeSpan);
            }
            catch (ObjectDisposedException) when (Volatile.Read(ref disposeRequested) != 0)
            {
            }
        }

        private TimeSpan GetCutoff (int cutoffIndex)
        {
            return cutoffIndex switch
            {
                0 => Plan.ExecutionCutoff,
                1 => Plan.PersistenceCutoff,
                2 => Plan.WriteCutoff,
                _ => throw new ArgumentOutOfRangeException(nameof(cutoffIndex)),
            };
        }

        private void CancelCutoff (int cutoffIndex)
        {
            var cancellationTokenSource = cutoffIndex switch
            {
                0 => executionDeadlineCancellationTokenSource,
                1 => persistenceCutoffCancellationTokenSource,
                2 => writeCutoffCancellationTokenSource,
                _ => throw new ArgumentOutOfRangeException(nameof(cutoffIndex)),
            };

            try
            {
                cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException) when (Volatile.Read(ref disposeRequested) != 0)
            {
            }
            catch (AggregateException)
            {
                // Cancellation callbacks are external to the phase scheduler. A faulty callback must not
                // escape the timer thread or prevent the later persistence and write cutoffs from advancing.
            }
        }

        private bool TryBeginTimerCallback ()
        {
            lock (lifetimeSyncRoot)
            {
                if (disposeRequested != 0)
                {
                    return false;
                }

                activeTimerCallbackCount++;
                return true;
            }
        }

        private void CompleteTimerCallback ()
        {
            bool disposeOwnedResources;
            lock (lifetimeSyncRoot)
            {
                activeTimerCallbackCount--;
                disposeOwnedResources = disposeRequested != 0
                    && timerDisposed
                    && activeTimerCallbackCount == 0;
            }

            if (disposeOwnedResources)
            {
                DisposeOwnedResources();
            }
        }

        private void DisposeOwnedResources ()
        {
            elapsedTime.Stop();
            additionalUpstreamCancellationRegistration.Dispose();
            ExecutionCancellation.Dispose();
            executionCancellationTokenSource.Dispose();
            writeCutoffCancellationTokenSource.Dispose();
            persistenceCutoffCancellationTokenSource.Dispose();
            executionDeadlineCancellationTokenSource.Dispose();
        }

        private void CancelFromAdditionalUpstream ()
        {
            ExecutionCancellation.RecordAdditionalUpstreamCancellation();
            try
            {
                executionCancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException) when (Volatile.Read(ref disposeRequested) != 0)
            {
            }
            catch (AggregateException)
            {
                // External cancellation callbacks cannot change phase-scope lifetime accounting.
            }
        }
    }
}
