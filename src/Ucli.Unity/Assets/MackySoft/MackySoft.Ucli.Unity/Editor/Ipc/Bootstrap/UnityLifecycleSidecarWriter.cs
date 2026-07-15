using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Owns one GUI daemon generation's lifecycle sidecar writes without executing filesystem work on the Unity thread.
    /// </summary>
    internal sealed class UnityLifecycleSidecarWriter
    {
        private const int InvalidationRetryLimit = 3;

        private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(50);

        private static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromSeconds(1);

        private readonly object syncRoot = new object();

        private readonly IUnityLifecycleSidecarPersistence persistence;

        private readonly CancellationTokenSource lifetimeCancellationSource = new CancellationTokenSource();

        private readonly TaskCompletionSource<bool> stopCompletionSource =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private TaskCompletionSource<bool> workAvailableSource = CreateSignalSource();

        private TaskCompletionSource<bool> stateChangedSource = CreateSignalSource();

        private WriterState state;

        private Task initializationTask = Task.CompletedTask;

        private Task workerTask = Task.CompletedTask;

        private WriteRequest pendingRequest;

        private long nextVersion;

        private long completedVersion;

        private DateTimeOffset? lastScheduledAtUtc;

        private DateTimeOffset? lastWriteCompletedAtUtc;

        private int consecutiveFailureCount;

        private bool failureStreakActive;

        private bool failureNotificationPending;

        private string latestFailureMessage;

        private bool stopRequested;

        private bool invalidationRequested;

        private Task invalidationTask;

        /// <summary> Initializes a writer for one persistence owner. </summary>
        public UnityLifecycleSidecarWriter (IUnityLifecycleSidecarPersistence persistence)
        {
            this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        }

        /// <summary> Gets the most recent time at which the Unity thread scheduled a snapshot. </summary>
        public DateTimeOffset? LastScheduledAtUtc
        {
            get
            {
                lock (syncRoot)
                {
                    return lastScheduledAtUtc;
                }
            }
        }

        /// <summary> Gets the most recent time at which a sidecar write completed successfully. </summary>
        public DateTimeOffset? LastWriteCompletedAtUtc
        {
            get
            {
                lock (syncRoot)
                {
                    return lastWriteCompletedAtUtc;
                }
            }
        }

        /// <summary> Persists the initial snapshot before endpoint publication commits. </summary>
        public Task InitializeAsync (
            UnityEditorObservation snapshot,
            DateTimeOffset scheduledAtUtc,
            CancellationToken cancellationToken)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            cancellationToken.ThrowIfCancellationRequested();
            lock (syncRoot)
            {
                if (state != WriterState.Created)
                {
                    throw new InvalidOperationException("The lifecycle sidecar writer has already been initialized.");
                }

                state = WriterState.Initializing;
                lastScheduledAtUtc = scheduledAtUtc;
                initializationTask = Task.Run(
                    () => InitializeCoreAsync(snapshot, cancellationToken),
                    CancellationToken.None);
                return initializationTask;
            }
        }

        /// <summary>
        /// Schedules one snapshot and replaces any older snapshot that has not begun writing.
        /// </summary>
        public bool TryEnqueue (
            UnityEditorObservation snapshot,
            DateTimeOffset scheduledAtUtc,
            out long version)
        {
            return TryEnqueueCore(
                snapshot,
                scheduledAtUtc,
                recoveryLease: null,
                out version);
        }

        /// <summary> Schedules the recovery observation written before one domain reload. </summary>
        public bool TryEnqueueDomainReloadRecovery (
            UnityEditorObservation snapshot,
            DateTimeOffset scheduledAtUtc,
            DaemonLifecycleRecoveryLease recoveryLease,
            out long version)
        {
            if (recoveryLease == null)
            {
                throw new ArgumentNullException(nameof(recoveryLease));
            }

            return TryEnqueueCore(snapshot, scheduledAtUtc, recoveryLease, out version);
        }

        private bool TryEnqueueCore (
            UnityEditorObservation snapshot,
            DateTimeOffset scheduledAtUtc,
            DaemonLifecycleRecoveryLease recoveryLease,
            out long version)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            lock (syncRoot)
            {
                if (state != WriterState.Running || stopRequested)
                {
                    version = 0;
                    return false;
                }

                version = checked(++nextVersion);
                pendingRequest = new WriteRequest(version, snapshot, recoveryLease);
                lastScheduledAtUtc = scheduledAtUtc;
                workAvailableSource.TrySetResult(true);
                return true;
            }
        }

        /// <summary> Waits until the requested snapshot or a newer replacement is durable. </summary>
        public async Task FlushAsync (long version, CancellationToken cancellationToken)
        {
            if (version <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(version));
            }

            while (true)
            {
                Task stateChangedTask;
                lock (syncRoot)
                {
                    if (completedVersion >= version)
                    {
                        return;
                    }

                    if (state != WriterState.Running || stopRequested)
                    {
                        throw new InvalidOperationException(
                            "The lifecycle sidecar writer stopped before the requested snapshot became durable.");
                    }

                    stateChangedTask = stateChangedSource.Task;
                }

                await AwaitWithCancellationAsync(stateChangedTask, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Tries to consume the latest failure for the current consecutive failure streak.
        /// This method is called by a Unity main-thread callback, never by the worker.
        /// </summary>
        public bool TryConsumeFailure (out string failureMessage)
        {
            lock (syncRoot)
            {
                if (!failureNotificationPending)
                {
                    failureMessage = null;
                    return false;
                }

                failureNotificationPending = false;
                failureMessage = latestFailureMessage;
                return true;
            }
        }

        /// <summary> Stops admission, cancels pending work, and waits for the worker to quiesce. </summary>
        public Task StopAsync (CancellationToken cancellationToken)
        {
            var beginStop = false;
            lock (syncRoot)
            {
                if (!stopRequested)
                {
                    stopRequested = true;
                    pendingRequest = null;
                    if (state != WriterState.Stopped)
                    {
                        state = WriterState.Stopping;
                    }

                    SignalStateChangedWithoutLock();
                    workAvailableSource.TrySetResult(true);
                    beginStop = true;
                }
            }

            if (beginStop)
            {
                var stopTask = Task.Run(CompleteStopAsync, CancellationToken.None);
                ObserveFault(stopTask);
            }

            return AwaitWithCancellationAsync(stopCompletionSource.Task, cancellationToken);
        }

        /// <summary>
        /// Stops this generation and deletes its persisted contents without deleting a successor generation.
        /// </summary>
        public async Task InvalidateAndStopAsync (CancellationToken cancellationToken)
        {
            var startInvalidation = false;
            lock (syncRoot)
            {
                invalidationRequested = true;
                startInvalidation = state == WriterState.Stopped;
            }

            if (startInvalidation)
            {
                _ = GetOrStartInvalidationTask();
            }

            await StopAsync(cancellationToken).ConfigureAwait(false);
            var capturedInvalidationTask = GetOrStartInvalidationTask();
            await AwaitWithCancellationAsync(capturedInvalidationTask, cancellationToken)
                .ConfigureAwait(false);
        }

        internal static TimeSpan ResolveRetryDelay (int consecutiveFailures)
        {
            if (consecutiveFailures <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(consecutiveFailures));
            }

            var delayMilliseconds = InitialRetryDelay.TotalMilliseconds;
            for (var failure = 1;
                 failure < consecutiveFailures && delayMilliseconds < MaximumRetryDelay.TotalMilliseconds;
                 failure++)
            {
                delayMilliseconds = Math.Min(
                    delayMilliseconds * 2,
                    MaximumRetryDelay.TotalMilliseconds);
            }

            return TimeSpan.FromMilliseconds(delayMilliseconds);
        }

        private async Task InitializeCoreAsync (
            UnityEditorObservation snapshot,
            CancellationToken callerCancellationToken)
        {
            using var initializationCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
                callerCancellationToken,
                lifetimeCancellationSource.Token);
            try
            {
                await persistence.WriteAsync(
                        snapshot,
                        null,
                        initializationCancellationSource.Token)
                    .ConfigureAwait(false);
                initializationCancellationSource.Token.ThrowIfCancellationRequested();
                lock (syncRoot)
                {
                    if (stopRequested)
                    {
                        throw new OperationCanceledException(lifetimeCancellationSource.Token);
                    }

                    state = WriterState.Running;
                    lastWriteCompletedAtUtc = DateTimeOffset.UtcNow;
                    workerTask = Task.Run(RunWorkerAsync, CancellationToken.None);
                    SignalStateChangedWithoutLock();
                }
            }
            catch
            {
                lock (syncRoot)
                {
                    if (!stopRequested)
                    {
                        state = WriterState.InitializationFailed;
                    }

                    SignalStateChangedWithoutLock();
                }

                throw;
            }
        }

        private async Task RunWorkerAsync ()
        {
            while (true)
            {
                WriteRequest request;
                Task workAvailableTask;
                lock (syncRoot)
                {
                    if (stopRequested)
                    {
                        return;
                    }

                    request = pendingRequest;
                    if (request == null)
                    {
                        workAvailableTask = workAvailableSource.Task;
                    }
                    else
                    {
                        pendingRequest = null;
                        if (workAvailableSource.Task.IsCompleted)
                        {
                            workAvailableSource = CreateSignalSource();
                        }

                        workAvailableTask = null;
                    }
                }

                if (request == null)
                {
                    try
                    {
                        await workAvailableTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (lifetimeCancellationSource.IsCancellationRequested)
                    {
                        return;
                    }

                    continue;
                }

                try
                {
                    await persistence.WriteAsync(
                            request.Snapshot,
                            request.RecoveryLease,
                            lifetimeCancellationSource.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (lifetimeCancellationSource.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    TimeSpan retryDelay;
                    lock (syncRoot)
                    {
                        if (stopRequested)
                        {
                            return;
                        }

                        if (consecutiveFailureCount < int.MaxValue)
                        {
                            consecutiveFailureCount++;
                        }

                        latestFailureMessage = exception.Message;
                        if (!failureStreakActive)
                        {
                            failureStreakActive = true;
                            failureNotificationPending = true;
                        }

                        if (pendingRequest == null)
                        {
                            pendingRequest = request;
                        }

                        workAvailableSource.TrySetResult(true);
                        retryDelay = ResolveRetryDelay(consecutiveFailureCount);
                        SignalStateChangedWithoutLock();
                    }

                    try
                    {
                        await Task.Delay(retryDelay, lifetimeCancellationSource.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (lifetimeCancellationSource.IsCancellationRequested)
                    {
                        return;
                    }

                    continue;
                }

                lock (syncRoot)
                {
                    completedVersion = Math.Max(completedVersion, request.Version);
                    lastWriteCompletedAtUtc = DateTimeOffset.UtcNow;
                    consecutiveFailureCount = 0;
                    failureStreakActive = false;
                    failureNotificationPending = false;
                    latestFailureMessage = null;
                    SignalStateChangedWithoutLock();
                }
            }
        }

        private async Task CompleteStopAsync ()
        {
            Exception stopFailure = null;
            try
            {
                try
                {
                    lifetimeCancellationSource.Cancel();
                }
                catch (Exception exception)
                {
                    stopFailure = exception;
                }

                try
                {
                    await initializationTask.ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Initialization failure is observed by StartAsync. Stop owns only worker quiescence.
                }

                Task capturedWorkerTask;
                lock (syncRoot)
                {
                    capturedWorkerTask = workerTask;
                }

                try
                {
                    await capturedWorkerTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (lifetimeCancellationSource.IsCancellationRequested)
                {
                    // Cancellation is the expected worker stop path.
                }
                catch (Exception exception)
                {
                    stopFailure ??= exception;
                }
            }
            finally
            {
                try
                {
                    lifetimeCancellationSource.Dispose();
                }
                catch (Exception exception)
                {
                    stopFailure ??= exception;
                }

                var startInvalidation = false;
                lock (syncRoot)
                {
                    state = WriterState.Stopped;
                    startInvalidation = invalidationRequested;
                    SignalStateChangedWithoutLock();
                }

                if (startInvalidation)
                {
                    try
                    {
                        _ = GetOrStartInvalidationTask();
                    }
                    catch (Exception exception)
                    {
                        stopFailure ??= exception;
                    }
                }

                if (stopFailure == null)
                {
                    stopCompletionSource.TrySetResult(true);
                }
                else
                {
                    stopCompletionSource.TrySetException(stopFailure);
                }
            }
        }

        private Task GetOrStartInvalidationTask ()
        {
            lock (syncRoot)
            {
                if (state != WriterState.Stopped)
                {
                    throw new InvalidOperationException(
                        "The lifecycle sidecar writer must stop before invalidation starts.");
                }

                if (invalidationTask != null)
                {
                    if (!invalidationTask.IsFaulted)
                    {
                        return invalidationTask;
                    }

                    invalidationTask = null;
                }

                invalidationTask = Task.Run(
                    RunInvalidationAsync,
                    CancellationToken.None);
                ObserveFault(invalidationTask);
                return invalidationTask;
            }
        }

        private async Task RunInvalidationAsync ()
        {
            for (var attempt = 1; attempt <= InvalidationRetryLimit; attempt++)
            {
                try
                {
                    await persistence.DeleteIfOwnedAsync(CancellationToken.None).ConfigureAwait(false);
                    return;
                }
                catch (Exception) when (attempt < InvalidationRetryLimit)
                {
                    await Task.Delay(ResolveRetryDelay(attempt)).ConfigureAwait(false);
                }
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

        private void SignalStateChangedWithoutLock ()
        {
            var completedSource = stateChangedSource;
            stateChangedSource = CreateSignalSource();
            completedSource.TrySetResult(true);
        }

        private static TaskCompletionSource<bool> CreateSignalSource ()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static async Task AwaitWithCancellationAsync (
            Task task,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (task.IsCompleted)
            {
                await task.ConfigureAwait(false);
                return;
            }

            var cancellationSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => cancellationSource.TrySetResult(true)))
            {
                var completedTask = await Task.WhenAny(task, cancellationSource.Task).ConfigureAwait(false);
                if (!ReferenceEquals(completedTask, task))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                await task.ConfigureAwait(false);
            }
        }

        private enum WriterState
        {
            Created,
            Initializing,
            Running,
            InitializationFailed,
            Stopping,
            Stopped,
        }

        private sealed class WriteRequest
        {
            public WriteRequest (
                long version,
                UnityEditorObservation snapshot,
                DaemonLifecycleRecoveryLease recoveryLease)
            {
                Version = version;
                Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
                if (recoveryLease != null
                    && (snapshot.State.LifecycleState != IpcEditorLifecycleState.Recovering
                        || recoveryLease.ExpiresAtUtc <= snapshot.ObservedAtUtc.ToUniversalTime()))
                {
                    throw new ArgumentException(
                        "Recovery lease requires a recovering observation and an expiration after its observation timestamp.",
                        nameof(recoveryLease));
                }

                RecoveryLease = recoveryLease;
            }

            public long Version { get; }

            public UnityEditorObservation Snapshot { get; }

            public DaemonLifecycleRecoveryLease RecoveryLease { get; }
        }
    }
}
