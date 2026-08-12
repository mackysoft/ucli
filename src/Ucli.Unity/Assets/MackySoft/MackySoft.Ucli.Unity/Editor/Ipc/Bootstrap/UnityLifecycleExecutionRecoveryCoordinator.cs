using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;
using MackySoft.Ucli.Unity.Runtime;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Admits persisted executions to the current endpoint and schedules only their owning typed recovery handlers.
    /// </summary>
    internal sealed class UnityLifecycleExecutionRecoveryCoordinator :
        ILifecycleExecutionDeadlineScheduler,
        IDisposable
    {
        private static readonly TimeSpan MaximumTrackingDelay =
            TimeSpan.FromHours(1);
        private static readonly TimeSpan TerminalRecoveryRetryDelay =
            TimeSpan.FromSeconds(1);

        private readonly FileLifecycleExecutionStore executionStore;
        private readonly UnityLifecycleExecutionHostContext hostContext;
        private readonly UnityProjectIdentity projectIdentity;
        private readonly IReadOnlyDictionary<LifecycleExecutionKind, ILifecycleExecutionRecoveryHandler> handlers;
        private readonly IUnityMainThreadRequestExecutor mainThreadRequestExecutor;
        private readonly IDaemonLogger daemonLogger;
        private readonly ILifecycleExecutionTerminalObserver terminalObserver;
        private readonly Func<ProcessIdentity, ProcessIdentityObservation>
            processIdentityObserver;
        private readonly Func<DateTimeOffset> utcNowProvider;
        private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;
        private readonly CancellationTokenSource lifetimeCancellationSource =
            new CancellationTokenSource();
        private readonly CancellationToken lifetimeCancellationToken;
        private readonly object lifecycleGate = new object();
        private readonly HashSet<(LifecycleExecutionKind Kind, Guid ExecutionId)>
            trackedExecutions =
                new HashSet<(LifecycleExecutionKind Kind, Guid ExecutionId)>();
        private readonly TaskCompletionSource<bool> quiescenceSource =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        private CoordinatorState state = CoordinatorState.Accepting;
        private bool started;
        private int activeOperationCount;

        public UnityLifecycleExecutionRecoveryCoordinator (
            FileLifecycleExecutionStore executionStore,
            UnityLifecycleExecutionHostContext hostContext,
            UnityProjectIdentity projectIdentity,
            IEnumerable<ILifecycleExecutionRecoveryHandler> handlers,
            IUnityMainThreadRequestExecutor mainThreadRequestExecutor,
            IDaemonLogger daemonLogger,
            ILifecycleExecutionTerminalObserver terminalObserver)
            : this(
                executionStore,
                hostContext,
                projectIdentity,
                handlers,
                mainThreadRequestExecutor,
                daemonLogger,
                terminalObserver,
                ProcessLivenessProbe.ObserveIdentity,
                static () => DateTimeOffset.UtcNow,
                static (delay, cancellationToken) =>
                    Task.Delay(delay, cancellationToken))
        {
        }

        internal UnityLifecycleExecutionRecoveryCoordinator (
            FileLifecycleExecutionStore executionStore,
            UnityLifecycleExecutionHostContext hostContext,
            UnityProjectIdentity projectIdentity,
            IEnumerable<ILifecycleExecutionRecoveryHandler> handlers,
            IUnityMainThreadRequestExecutor mainThreadRequestExecutor,
            IDaemonLogger daemonLogger,
            ILifecycleExecutionTerminalObserver terminalObserver,
            Func<ProcessIdentity, ProcessIdentityObservation>
                processIdentityObserver)
            : this(
                executionStore,
                hostContext,
                projectIdentity,
                handlers,
                mainThreadRequestExecutor,
                daemonLogger,
                terminalObserver,
                processIdentityObserver,
                static () => DateTimeOffset.UtcNow,
                static (delay, cancellationToken) =>
                    Task.Delay(delay, cancellationToken))
        {
        }

        internal UnityLifecycleExecutionRecoveryCoordinator (
            FileLifecycleExecutionStore executionStore,
            UnityLifecycleExecutionHostContext hostContext,
            UnityProjectIdentity projectIdentity,
            IEnumerable<ILifecycleExecutionRecoveryHandler> handlers,
            IUnityMainThreadRequestExecutor mainThreadRequestExecutor,
            IDaemonLogger daemonLogger,
            ILifecycleExecutionTerminalObserver terminalObserver,
            Func<ProcessIdentity, ProcessIdentityObservation>
                processIdentityObserver,
            Func<DateTimeOffset> utcNowProvider,
            Func<TimeSpan, CancellationToken, Task> delayAsync)
        {
            this.executionStore = executionStore
                ?? throw new ArgumentNullException(nameof(executionStore));
            this.hostContext = hostContext
                ?? throw new ArgumentNullException(nameof(hostContext));
            this.projectIdentity = projectIdentity
                ?? throw new ArgumentNullException(nameof(projectIdentity));
            this.handlers = CreateHandlerMap(
                handlers ?? throw new ArgumentNullException(nameof(handlers)));
            this.mainThreadRequestExecutor = mainThreadRequestExecutor
                ?? throw new ArgumentNullException(nameof(mainThreadRequestExecutor));
            this.daemonLogger = daemonLogger
                ?? throw new ArgumentNullException(nameof(daemonLogger));
            this.terminalObserver = terminalObserver
                ?? throw new ArgumentNullException(nameof(terminalObserver));
            this.processIdentityObserver = processIdentityObserver
                ?? throw new ArgumentNullException(
                    nameof(processIdentityObserver));
            this.utcNowProvider = utcNowProvider
                ?? throw new ArgumentNullException(nameof(utcNowProvider));
            this.delayAsync = delayAsync
                ?? throw new ArgumentNullException(nameof(delayAsync));
            lifetimeCancellationToken = lifetimeCancellationSource.Token;
        }

        public void Start ()
        {
            lock (lifecycleGate)
            {
                if (state != CoordinatorState.Accepting || started)
                {
                    return;
                }

                started = true;
                activeOperationCount++;
            }

            _ = RunOwnedOperationAsync(
                RecoverAllAsync(),
                "Lifecycle Execution bootstrap recovery failed.");
        }

        /// <inheritdoc />
        public void Track (
            LifecycleExecutionKind kind,
            Guid executionId)
        {
            if (!TextVocabulary.IsDefined(kind))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    "Lifecycle Execution kind must be defined.");
            }
            if (executionId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Lifecycle Execution identifier must not be empty.",
                    nameof(executionId));
            }
            if (!handlers.ContainsKey(kind))
            {
                return;
            }

            var key = (Kind: kind, ExecutionId: executionId);
            lock (lifecycleGate)
            {
                if (state != CoordinatorState.Accepting)
                {
                    return;
                }
                if (!trackedExecutions.Add(key))
                {
                    return;
                }

                activeOperationCount++;
            }

            _ = RunOwnedOperationAsync(
                TrackUntilTerminalAsync(key.Kind, key.ExecutionId),
                $"Lifecycle Execution deadline tracking failed for '{kind}/{executionId:D}'.",
                key);
        }

        internal async Task RecoverAllAsync ()
        {
            lifetimeCancellationToken.ThrowIfCancellationRequested();
            var entries = executionStore.ListEntries(lifetimeCancellationToken);
            for (var index = 0; index < entries.Count; index++)
            {
                lifetimeCancellationToken.ThrowIfCancellationRequested();
                var entry = entries[index];
                StoredLifecycleExecution execution = null;
                try
                {
                    execution = await executionStore.ReadAsync(
                        entry.Kind,
                        entry.ExecutionId,
                        lifetimeCancellationToken);
                    if (execution == null
                        || execution.IsTerminal
                        || !handlers.TryGetValue(entry.Kind, out var handler))
                    {
                        continue;
                    }
                    if (execution.IsPublishing)
                    {
                        try
                        {
                            await DispatchRecoveryAsync(
                                handler,
                                execution.Start,
                                rejectionReason: null,
                                canAttributeCurrentProviderObservation: false,
                                dispatchCancellationToken:
                                    lifetimeCancellationToken);
                        }
                        finally
                        {
                            Track(entry.Kind, entry.ExecutionId);
                        }

                        continue;
                    }

                    var advanceOutcome =
                        await executionStore.TryAdvanceEndpointRegistrationAsync(
                            entry.Kind,
                            entry.ExecutionId,
                            projectIdentity,
                            hostContext.Process,
                            hostContext.EditorInstanceId,
                            hostContext.EndpointRegistrationGenerationId,
                            hostContext.RecoveryLease,
                            GetUtcNow(),
                            lifetimeCancellationToken);
                    var processObservation = processIdentityObserver(
                        execution.Start.Host.Process);
                    var rejectionReason = GetRejectionReason(
                        advanceOutcome,
                        processObservation);
                    var current = await executionStore.ReadAsync(
                        entry.Kind,
                        entry.ExecutionId,
                        lifetimeCancellationToken);
                    if (current == null || current.IsTerminal)
                    {
                        continue;
                    }
                    if (current.IsPublishing)
                    {
                        try
                        {
                            await DispatchRecoveryAsync(
                                handler,
                                current.Start,
                                rejectionReason: null,
                                canAttributeCurrentProviderObservation: false,
                                dispatchCancellationToken:
                                    lifetimeCancellationToken);
                        }
                        finally
                        {
                            Track(entry.Kind, entry.ExecutionId);
                        }

                        continue;
                    }

                    var deadlineExceeded =
                        current.Start.DeadlineUtc <= GetUtcNow();
                    if (!deadlineExceeded
                        && ShouldDeferRecovery(
                            advanceOutcome,
                            processObservation))
                    {
                        Track(entry.Kind, entry.ExecutionId);
                        continue;
                    }

                    var recoveryReason = deadlineExceeded
                        ? LifecycleExecutionTerminalReason.DeadlineExceeded
                        : rejectionReason;
                    var canAttributeCurrentProviderObservation =
                        advanceOutcome
                            is LifecycleExecutionEndpointAdvanceOutcome.Advanced
                            or LifecycleExecutionEndpointAdvanceOutcome
                                .AlreadyCurrent;
                    if (recoveryReason.HasValue)
                    {
                        try
                        {
                            await DispatchRecoveryAsync(
                                handler,
                                current.Start,
                                recoveryReason.Value,
                                canAttributeCurrentProviderObservation,
                                lifetimeCancellationToken);
                        }
                        finally
                        {
                            Track(entry.Kind, entry.ExecutionId);
                        }

                        continue;
                    }

                    Track(entry.Kind, entry.ExecutionId);
                    await DispatchRecoveryAsync(
                        handler,
                        current.Start,
                        rejectionReason: null,
                        canAttributeCurrentProviderObservation,
                        dispatchCancellationToken:
                            lifetimeCancellationToken);
                }
                catch (OperationCanceledException) when (
                    lifetimeCancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // One damaged or transiently failing execution must not prevent later durable
                    // executions from being admitted and tracked by this successor endpoint.
                    if (execution != null && !execution.IsTerminal)
                    {
                        Track(entry.Kind, entry.ExecutionId);
                    }
                    daemonLogger.Exception(
                        DaemonLogCategories.Lifecycle,
                        $"Lifecycle Execution recovery failed for '{entry.Kind}/{entry.ExecutionId:D}'.",
                        exception);
                }
            }
        }

        /// <summary>
        /// Stops recovery admission and completes after every admitted recovery and tracking operation terminates.
        /// </summary>
        internal Task StopAsync ()
        {
            var cancelLifetime = false;
            Task stopTask;
            lock (lifecycleGate)
            {
                if (state == CoordinatorState.Accepting)
                {
                    state = CoordinatorState.Stopping;
                    cancelLifetime = true;
                    CompleteQuiescenceIfReady();
                }

                stopTask = quiescenceSource.Task;
            }

            if (cancelLifetime)
            {
                lifetimeCancellationSource.Cancel();
            }

            return stopTask;
        }

        /// <inheritdoc />
        public void Dispose ()
        {
            lock (lifecycleGate)
            {
                if (state == CoordinatorState.Disposed)
                {
                    return;
                }
                if (state == CoordinatorState.Accepting && activeOperationCount == 0)
                {
                    state = CoordinatorState.Stopping;
                    CompleteQuiescenceIfReady();
                }
                if (state != CoordinatorState.Quiescent)
                {
                    throw new InvalidOperationException(
                        "Unity Lifecycle Execution recovery must complete StopAsync before disposal.");
                }

                state = CoordinatorState.Disposed;
            }

            lifetimeCancellationSource.Dispose();
        }

        internal async Task TrackUntilTerminalAsync (
            LifecycleExecutionKind kind,
            Guid executionId)
        {
            var key = (Kind: kind, ExecutionId: executionId);
            while (true)
            {
                lifetimeCancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var execution = await executionStore.ReadAsync(
                            key.Kind,
                            key.ExecutionId,
                            lifetimeCancellationToken)
                        .ConfigureAwait(false);
                    if (execution == null)
                    {
                        return;
                    }
                    if (execution.IsTerminal)
                    {
                        NotifyTerminalIfOwned(
                            execution,
                            key.Kind,
                            key.ExecutionId);
                        return;
                    }

                    var remaining = execution.Start.DeadlineUtc - GetUtcNow();
                    if (remaining > TimeSpan.Zero)
                    {
                        await delayAsync(
                                remaining < MaximumTrackingDelay
                                    ? remaining
                                    : MaximumTrackingDelay,
                                lifetimeCancellationToken)
                            .ConfigureAwait(false);
                        continue;
                    }

                    try
                    {
                        await DispatchRecoveryAsync(
                                handlers[key.Kind],
                                execution.Start,
                                LifecycleExecutionTerminalReason.DeadlineExceeded,
                                CanAttributeCurrentProviderObservation(
                                    execution.Start),
                                lifetimeCancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (
                        lifetimeCancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        daemonLogger.Exception(
                            DaemonLogCategories.Lifecycle,
                            $"Lifecycle Execution deadline recovery failed for '{key.Kind}/{key.ExecutionId:D}' and will be retried.",
                            exception);
                    }

                    lifetimeCancellationToken.ThrowIfCancellationRequested();
                    var recovered = await executionStore.ReadAsync(
                            key.Kind,
                            key.ExecutionId,
                            lifetimeCancellationToken)
                        .ConfigureAwait(false);
                    if (recovered == null)
                    {
                        return;
                    }
                    if (recovered.IsTerminal)
                    {
                        NotifyTerminalIfOwned(
                            recovered,
                            key.Kind,
                            key.ExecutionId);
                        return;
                    }
                    }
                    catch (OperationCanceledException) when (
                        lifetimeCancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        daemonLogger.Exception(
                            DaemonLogCategories.Lifecycle,
                            $"Lifecycle Execution deadline tracking failed for '{key.Kind}/{key.ExecutionId:D}' and will be retried.",
                            exception);
                    }

                    await delayAsync(
                            TerminalRecoveryRetryDelay,
                            lifetimeCancellationToken)
                        .ConfigureAwait(false);
            }
        }

        private async Task DispatchRecoveryAsync (
            ILifecycleExecutionRecoveryHandler handler,
            LifecycleExecutionStartBinding start,
            LifecycleExecutionTerminalReason? rejectionReason,
            bool canAttributeCurrentProviderObservation,
            CancellationToken dispatchCancellationToken)
        {
            dispatchCancellationToken.ThrowIfCancellationRequested();
            var recoveryRequest = new LifecycleExecutionRecoveryRequest(
                start,
                rejectionReason,
                canAttributeCurrentProviderObservation);
            await mainThreadRequestExecutor.ExecuteAsync(
                    async () =>
                    {
                        await handler.RecoverAsync(
                            recoveryRequest,
                            CancellationToken.None);
                        return true;
                    },
                    CancellationToken.None)
                .ConfigureAwait(false);
            var recovered = await executionStore.ReadAsync(
                    handler.Kind,
                    start.LifecycleExecutionRef.Id,
                    dispatchCancellationToken)
                .ConfigureAwait(false);
            if (recovered?.IsTerminal == true)
            {
                NotifyTerminalIfOwned(
                    recovered,
                    handler.Kind,
                    start.LifecycleExecutionRef.Id);
            }
        }

        private void NotifyTerminalIfOwned (
            StoredLifecycleExecution execution,
            LifecycleExecutionKind kind,
            Guid executionId)
        {
            if (execution.Start.Host.Process != hostContext.Process
                || execution.Start.Host.EditorInstanceId
                    != hostContext.EditorInstanceId)
            {
                return;
            }

            terminalObserver.OnTerminal(kind, executionId);
        }

        private DateTimeOffset GetUtcNow ()
        {
            var utcNow = utcNowProvider();
            if (utcNow == default || utcNow.Offset != TimeSpan.Zero)
            {
                throw new InvalidOperationException(
                    "Lifecycle Execution deadline scheduler clock must return a non-default UTC timestamp.");
            }

            return utcNow;
        }

        private async Task RunOwnedOperationAsync (
            Task operation,
            string message,
            (LifecycleExecutionKind Kind, Guid ExecutionId)? trackedKey = null)
        {
            try
            {
                await operation.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (
                lifetimeCancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                daemonLogger.Exception(
                    DaemonLogCategories.Lifecycle,
                    message,
                    exception);
            }
            finally
            {
                lock (lifecycleGate)
                {
                    activeOperationCount--;
                    if (trackedKey.HasValue)
                    {
                        trackedExecutions.Remove(trackedKey.Value);
                    }
                    CompleteQuiescenceIfReady();
                }
            }
        }

        private void CompleteQuiescenceIfReady ()
        {
            if (state == CoordinatorState.Stopping && activeOperationCount == 0)
            {
                state = CoordinatorState.Quiescent;
                quiescenceSource.TrySetResult(true);
            }
        }

        private enum CoordinatorState
        {
            Accepting,
            Stopping,
            Quiescent,
            Disposed,
        }

        private static IReadOnlyDictionary<LifecycleExecutionKind, ILifecycleExecutionRecoveryHandler>
            CreateHandlerMap (IEnumerable<ILifecycleExecutionRecoveryHandler> handlers)
        {
            var result =
                new Dictionary<LifecycleExecutionKind, ILifecycleExecutionRecoveryHandler>();
            foreach (var handler in handlers)
            {
                if (handler == null)
                {
                    throw new ArgumentException(
                        "Lifecycle Execution recovery handlers must not contain null.",
                        nameof(handlers));
                }

                if (!result.TryAdd(handler.Kind, handler))
                {
                    throw new ArgumentException(
                        $"Duplicate Lifecycle Execution recovery handler: {handler.Kind}.",
                        nameof(handlers));
                }
            }

            return result;
        }

        private static LifecycleExecutionTerminalReason? GetRejectionReason (
            LifecycleExecutionEndpointAdvanceOutcome outcome,
            ProcessIdentityObservation processObservation)
        {
            return outcome switch
            {
                LifecycleExecutionEndpointAdvanceOutcome.Advanced
                    or LifecycleExecutionEndpointAdvanceOutcome.AlreadyCurrent => null,
                LifecycleExecutionEndpointAdvanceOutcome
                    .TerminalPublicationFixed => null,
                LifecycleExecutionEndpointAdvanceOutcome.ProjectMismatch =>
                    LifecycleExecutionTerminalReason.ProjectMismatch,
                LifecycleExecutionEndpointAdvanceOutcome.HostMismatch =>
                    ResolveObservedHostRejection(
                        processObservation,
                        LifecycleExecutionTerminalReason.HostMismatch),
                LifecycleExecutionEndpointAdvanceOutcome.GenerationMismatch
                    or LifecycleExecutionEndpointAdvanceOutcome.RecoveryLeaseExpired =>
                    ResolveObservedHostRejection(
                        processObservation,
                        LifecycleExecutionTerminalReason
                            .GenerationMismatch),
                LifecycleExecutionEndpointAdvanceOutcome.AlreadyTerminal => null,
                LifecycleExecutionEndpointAdvanceOutcome.Missing => null,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome,
                    "Unsupported endpoint registration advancement outcome."),
            };
        }

        private static LifecycleExecutionTerminalReason?
            ResolveObservedHostRejection (
            ProcessIdentityObservation processObservation,
            LifecycleExecutionTerminalReason liveHostReason)
        {
            return processObservation switch
            {
                ProcessIdentityObservation.Same => liveHostReason,
                ProcessIdentityObservation.ConfirmedExitedOrReplaced =>
                    LifecycleExecutionTerminalReason.UnityExited,
                ProcessIdentityObservation.Unobservable => null,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(processObservation),
                    processObservation,
                    "Unsupported process identity observation."),
            };
        }

        private bool CanAttributeCurrentProviderObservation (
            LifecycleExecutionStartBinding start)
        {
            return start.Project == projectIdentity
                && start.Host.Process == hostContext.Process
                && start.Host.EditorInstanceId
                    == hostContext.EditorInstanceId
                && start.Host.CurrentEndpointRegistrationGenerationId
                    == hostContext.EndpointRegistrationGenerationId;
        }

        private static bool ShouldDeferRecovery (
            LifecycleExecutionEndpointAdvanceOutcome outcome,
            ProcessIdentityObservation processObservation)
        {
            return processObservation
                    == ProcessIdentityObservation.Unobservable
                && outcome
                    is LifecycleExecutionEndpointAdvanceOutcome.HostMismatch
                    or LifecycleExecutionEndpointAdvanceOutcome
                        .GenerationMismatch
                    or LifecycleExecutionEndpointAdvanceOutcome
                        .RecoveryLeaseExpired;
        }
    }
}
