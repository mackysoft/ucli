using MackySoft.Ucli.Application.Features.Programs.Persistence;
using MackySoft.Ucli.Application.Shared.Execution.Process;
using MackySoft.Ucli.Application.Shared.Identifiers;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Application.Features.Programs.Supervision;

/// <summary>
/// Advances only the Run owned by this live CLI process. It never transfers
/// ownership or resolves a later Program frontier.
/// </summary>
internal sealed class ProgramAttachedSupervisor
{
    private const int MaximumTerminationRecoveryAttempts = 8;
    private static readonly TimeSpan TerminationRecoveryGrace = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RecoveryBurstYield = TimeSpan.FromMilliseconds(1);
    private readonly IProgramRunStoreFactory storeFactory;
    private readonly IProgramStepExecutionPort executionPort;
    private readonly IProcessIdentityObserver processIdentityObserver;
    private readonly IGuidGenerator guidGenerator;
    private readonly TimeProvider timeProvider;
    private readonly ProgramRunTerminalizer terminalizer;
    private readonly ProcessIdentity owner;
    private readonly Dictionary<Guid, ExecutionDeadline> liveStepDeadlines = [];

    public ProgramAttachedSupervisor (
        IProgramRunStoreFactory storeFactory,
        IProgramStepExecutionPort executionPort,
        IProcessIdentityObserver processIdentityObserver,
        IGuidGenerator guidGenerator,
        TimeProvider timeProvider,
        ProcessIdentity owner)
    {
        this.storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
        this.executionPort = executionPort ?? throw new ArgumentNullException(nameof(executionPort));
        this.processIdentityObserver = processIdentityObserver ?? throw new ArgumentNullException(nameof(processIdentityObserver));
        this.guidGenerator = guidGenerator ?? throw new ArgumentNullException(nameof(guidGenerator));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        terminalizer = new ProgramRunTerminalizer(timeProvider);
    }

    /// <summary>
    /// Persists the next Step's logical execution identity before invoking the
    /// closed port. The supplied deadline is owned by this live Supervisor's
    /// monotonic clock and must not be reconstructed from persisted UTC data.
    /// </summary>
    public async ValueTask<ProgramRunRecord?> StartNextAsync (
        ResolvedUnityProjectContext project,
        Guid runId,
        ExecutionDeadline runDeadline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(runDeadline);
        var store = storeFactory.ForProject(project);
        var current = await store.ReadAsync(runId, cancellationToken).ConfigureAwait(false);
        if (current is null || ProgramRunStateSemantics.IsTerminal(current.State) || current.FixedContext.Supervisor.OwnerProcess != owner)
        {
            return current;
        }

        current = await ReconcileLivenessAsync(store, current, cancellationToken).ConfigureAwait(false);
        if (ProgramRunStateSemantics.IsTerminal(current.State))
        {
            return current;
        }
        if (current.Cancellation.Requested)
        {
            // A cancellation fence may race with a status/cancel caller after
            // this Supervisor admitted a Step.  Do not publish a terminal Run
            // until the exact persisted execution is recovered: its final
            // application state is part of the terminal contract.
            var active = current.Cursor < current.Steps.Count ? current.Steps[current.Cursor] : null;
            if (active?.Execution is not null && ProgramRunStateSemantics.IsOngoing(active.State))
            {
                return await TerminateAndRecoverAsync(
                    store,
                    current,
                    current.Cursor,
                    runDeadline,
                    "PROGRAM_RUN_CANCELLED",
                    cancellationToken).ConfigureAwait(false);
            }

            return await terminalizer.TerminalizeAsync(store, current, ProgramRunState.Cancelled, "PROGRAM_RUN_CANCELLED", cancellationToken).ConfigureAwait(false);
        }
        if (runDeadline.IsExpired)
        {
            return await terminalizer.TerminalizeAsync(store, current, ProgramRunState.Failed, "PROGRAM_RUN_TIMEOUT", cancellationToken).ConfigureAwait(false);
        }
        if (current.Cursor >= current.Steps.Count || current.Steps[current.Cursor].State != ProgramStepState.Deferred)
        {
            return current;
        }

        // A Step deadline begins only when its planning transition is fixed.
        // The Run deadline was checked above, before any new planning begins.
        var stepDeadline = runDeadline.CreateCappedDeadline(TimeSpan.FromMilliseconds(current.Steps[current.Cursor].TimeoutMilliseconds));

        var started = PersistStepStart(current, stepDeadline);
        var exchange = await store.CompareExchangeAsync(current, started, cancellationToken).ConfigureAwait(false);
        if (!exchange.Exchanged)
        {
            return exchange.Current;
        }

        // A Run deadline wins the same instant as a Step deadline. This keeps
        // the unadmitted Step deferred and preserves only the run-timeout
        // planning audit facts during terminalization.
        if (runDeadline.IsExpired)
        {
            return await terminalizer.TerminalizeAsync(store, exchange.Current, ProgramRunState.Failed, "PROGRAM_RUN_TIMEOUT", cancellationToken).ConfigureAwait(false);
        }
        if (stepDeadline.IsExpired)
        {
            return await terminalizer.TerminalizeUnstartedStepTimeoutAsync(store, exchange.Current, exchange.Current.Cursor, cancellationToken).ConfigureAwait(false);
        }

        // This admission records the only port invocation before any local
        // preflight starts. For call, it is deliberately distinct from the
        // Request boundary: a rejected preflight has not entered Request Call,
        // but must still be terminalized without replaying this port invocation.
        var admitted = await MarkPortInvocationAsync(store, exchange.Current, cancellationToken).ConfigureAwait(false);
        if (admitted is null)
        {
            return await store.ReadAsync(runId, cancellationToken).ConfigureAwait(false);
        }
        var step = admitted.Steps[admitted.Cursor];
        var execution = step.Execution ?? throw new InvalidOperationException("Persisted Program Step start requires its execution identity.");
        liveStepDeadlines[execution.ExecutionId] = stepDeadline;
        var result = await executionPort.StartAsync(new ProgramStepExecutionStart(admitted, admitted.Cursor, execution), cancellationToken).ConfigureAwait(false);
        if (result.Terminal is not null)
        {
            return await HandleRecoveredTerminalAsync(
                project,
                store,
                admitted,
                admitted.Cursor,
                result.Terminal,
                runDeadline,
                cancellationToken).ConfigureAwait(false);
        }
        if (result.Disposition == ProgramStepExecutionStartDisposition.CommunicationLost)
        {
            return await RecoverAsync(project, admitted.RunId, runDeadline, cancellationToken).ConfigureAwait(false);
        }
        return admitted;
    }

    /// <summary> Attempts recovery only for the already persisted logical execution. </summary>
    public async ValueTask<ProgramRunRecord?> RecoverAsync (
        ResolvedUnityProjectContext project,
        Guid runId,
        ExecutionDeadline runDeadline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(runDeadline);
        var store = storeFactory.ForProject(project);
        var current = await store.ReadAsync(runId, cancellationToken).ConfigureAwait(false);
        if (current is null || ProgramRunStateSemantics.IsTerminal(current.State) || current.FixedContext.Supervisor.OwnerProcess != owner)
        {
            return current;
        }
        current = await ReconcileLivenessAsync(store, current, cancellationToken).ConfigureAwait(false);
        if (ProgramRunStateSemantics.IsTerminal(current.State))
        {
            return current;
        }
        var step = current.Cursor < current.Steps.Count ? current.Steps[current.Cursor] : null;
        if (step?.Execution is null || !ProgramRunStateSemantics.IsOngoing(step.State))
        {
            return current;
        }
        if (current.Cancellation.Requested)
        {
            return await TerminateAndRecoverAsync(
                store,
                current,
                current.Cursor,
                runDeadline,
                "PROGRAM_RUN_CANCELLED",
                cancellationToken).ConfigureAwait(false);
        }
        if (runDeadline.IsExpired)
        {
            return await TerminateAndRecoverAsync(store, current, current.Cursor, runDeadline, "PROGRAM_RUN_TIMEOUT", cancellationToken).ConfigureAwait(false);
        }
        if (liveStepDeadlines.TryGetValue(step.Execution.ExecutionId, out var stepDeadline) && stepDeadline.IsExpired)
        {
            return await TerminateAndRecoverAsync(store, current, current.Cursor, stepDeadline, "PROGRAM_STEP_TIMEOUT", cancellationToken).ConfigureAwait(false);
        }
        var attemptsSinceYield = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var activeDeadline = SelectRecoveryDeadline(runDeadline, stepDeadline);
            var recovery = await RecoverWithinDeadlineAsync(current, current.Cursor, step.Execution, activeDeadline, cancellationToken).ConfigureAwait(false);
            if (recovery is null)
            {
                return await TerminateAndRecoverAsync(
                    store,
                    current,
                    current.Cursor,
                    activeDeadline,
                    ReferenceEquals(activeDeadline, runDeadline) ? "PROGRAM_RUN_TIMEOUT" : "PROGRAM_STEP_TIMEOUT",
                    cancellationToken).ConfigureAwait(false);
            }
            if (recovery.Terminal is not null)
            {
                return await HandleRecoveredTerminalAsync(
                    project,
                    store,
                    current,
                    current.Cursor,
                    recovery.Terminal,
                    runDeadline,
                    cancellationToken).ConfigureAwait(false);
            }
            if (recovery.Disposition != ProgramStepExecutionRecoveryDisposition.CommunicationLost)
            {
                return current;
            }
            if (runDeadline.IsExpired)
            {
                return await TerminateAndRecoverAsync(store, current, current.Cursor, runDeadline, "PROGRAM_RUN_TIMEOUT", cancellationToken).ConfigureAwait(false);
            }
            if (liveStepDeadlines.TryGetValue(step.Execution.ExecutionId, out stepDeadline) && stepDeadline.IsExpired)
            {
                return await TerminateAndRecoverAsync(store, current, current.Cursor, stepDeadline, "PROGRAM_STEP_TIMEOUT", cancellationToken).ConfigureAwait(false);
            }
            attemptsSinceYield++;
            if (attemptsSinceYield == MaximumTerminationRecoveryAttempts)
            {
                await YieldRecoveryBurstAsync(activeDeadline, cancellationToken).ConfigureAwait(false);
                attemptsSinceYield = 0;
            }
        }
    }

    private async ValueTask<ProgramRunRecord> TerminateAndRecoverAsync (
        IProgramRunStore store,
        ProgramRunRecord current,
        int stepIndex,
        ExecutionDeadline deadline,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deadline);
        var step = current.Steps[stepIndex];
        var execution = step.Execution ?? throw new InvalidOperationException("An admitted Program Step requires its persisted execution identity.");
        // Completion recovery is a separately bounded phase. It never extends
        // the expired execution deadline and is fixed once before the first
        // termination request is issued.
        var recoveryDeadline = ExecutionDeadline.Start(TerminationRecoveryGrace, timeProvider);
        var termination = await RequestTerminationWithinDeadlineAsync(
            current,
            stepIndex,
            execution,
            reasonCode,
            recoveryDeadline,
            cancellationToken).ConfigureAwait(false);
        if (termination is null)
        {
            return await terminalizer.TerminalizeAsync(store, current, ProgramRunState.Interrupted,
                "PROGRAM_COMMUNICATION_RECOVERY_EXPIRED", cancellationToken).ConfigureAwait(false);
        }
        // Each port call owns one bounded attach wait. Repeating that bounded
        // operation preserves the same logical execution without a busy loop.
        var attemptsSinceYield = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var recovery = await RecoverWithinDeadlineAsync(current, stepIndex, execution, recoveryDeadline, cancellationToken).ConfigureAwait(false);
            if (recovery is null)
            {
                return await terminalizer.TerminalizeAsync(store, current, ProgramRunState.Interrupted,
                    "PROGRAM_COMMUNICATION_RECOVERY_EXPIRED", cancellationToken).ConfigureAwait(false);
            }
            if (recovery.Terminal is not null && IsKnownApplicationState(recovery.Terminal.ApplicationState))
            {
                var terminalState = reasonCode == "PROGRAM_RUN_CANCELLED"
                    ? ProgramStepState.Cancelled
                    : ProgramStepState.Failed;
                var timedOut = recovery.Terminal with
                {
                    State = terminalState,
                    Verdict = null,
                    ErrorCode = reasonCode,
                };
                var withStep = await terminalizer.PublishRecoveredTerminalAsync(store, current, stepIndex, timedOut, cancellationToken).ConfigureAwait(false);
                var runState = terminalState == ProgramStepState.Cancelled
                    ? ProgramRunState.Cancelled
                    : ProgramRunState.Failed;
                return await terminalizer.TerminalizeAsync(store, withStep, runState, reasonCode, cancellationToken).ConfigureAwait(false);
            }
            if (recovery.Disposition != ProgramStepExecutionRecoveryDisposition.CommunicationLost)
            {
                return await terminalizer.TerminalizeAsync(store, current, ProgramRunState.Interrupted,
                    "PROGRAM_COMMUNICATION_RECOVERY_EXPIRED", cancellationToken).ConfigureAwait(false);
            }
            attemptsSinceYield++;
            if (attemptsSinceYield == MaximumTerminationRecoveryAttempts)
            {
                await YieldRecoveryBurstAsync(recoveryDeadline, cancellationToken).ConfigureAwait(false);
                attemptsSinceYield = 0;
            }
        }
    }

    /// <summary>
    /// Records the already recovered Step before selecting the only permitted
    /// next transition. A completed verdict is a result of execution, not an
    /// execution failure, so only a non-completed Step stops the Program.
    /// </summary>
    private async ValueTask<ProgramRunRecord> HandleRecoveredTerminalAsync (
        ResolvedUnityProjectContext project,
        IProgramRunStore store,
        ProgramRunRecord current,
        int stepIndex,
        ProgramStepExecutionRecoveredTerminal recovered,
        ExecutionDeadline runDeadline,
        CancellationToken cancellationToken)
    {
        // A Lifecycle action persists its Start Record through its observer
        // while the port is dispatching. `current` can therefore predate that
        // observer CAS even though `recovered` already carries the matching
        // terminal reference. Publish only from the durable aggregate so the
        // terminalizer validates the same persisted identity, rather than
        // treating the stale snapshot as an invalid terminal projection.
        var persisted = await store.ReadAsync(current.RunId, cancellationToken).ConfigureAwait(false);
        if (persisted is null)
        {
            throw new InvalidOperationException("Program Run disappeared while a recovered Step terminal was being recorded.");
        }
        if (ProgramRunStateSemantics.IsTerminal(persisted.State)
            || stepIndex < 0
            || stepIndex >= persisted.Steps.Count
            || stepIndex >= current.Steps.Count)
        {
            return persisted;
        }

        var expectedStep = current.Steps[stepIndex];
        var persistedStep = persisted.Steps[stepIndex];
        if (persisted.Cursor != stepIndex
            || persistedStep.Execution != expectedStep.Execution
            || !ProgramRunStateSemantics.IsOngoing(persistedStep.State)
            || !CanPublishReturnedTerminal(persistedStep, recovered))
        {
            // Another supervisor/status writer has already advanced or
            // terminalized this exact boundary. Do not publish a terminal
            // against a different Step or rerun the returned action result.
            return persisted;
        }

        var published = await terminalizer.PublishRecoveredTerminalAsync(
            store, persisted, stepIndex, recovered, cancellationToken).ConfigureAwait(false);
        if (ProgramRunStateSemantics.IsTerminal(published.State))
        {
            return published;
        }

        if (recovered.State != ProgramStepState.Completed)
        {
            var runState = recovered.State switch
            {
                ProgramStepState.Failed => ProgramRunState.Failed,
                ProgramStepState.Cancelled => ProgramRunState.Cancelled,
                ProgramStepState.Interrupted => ProgramRunState.Interrupted,
                _ => throw new InvalidOperationException("Recovered Program Step must be terminal."),
            };
            var reasonCode = recovered.ErrorCode ?? runState switch
            {
                ProgramRunState.Failed => "PROGRAM_STEP_FAILED",
                ProgramRunState.Cancelled => "PROGRAM_STEP_CANCELLED",
                ProgramRunState.Interrupted => "PROGRAM_STEP_INTERRUPTED",
                _ => throw new InvalidOperationException("Program terminal state is not defined."),
            };
            return await terminalizer.TerminalizeAsync(store, published, runState, reasonCode, cancellationToken).ConfigureAwait(false);
        }

        if (published.Cursor == published.Steps.Count)
        {
            return await terminalizer.TerminalizeAsync(
                store, published, ProgramRunState.Completed, "PROGRAM_RUN_COMPLETED", cancellationToken).ConfigureAwait(false);
        }

        return await StartNextAsync(project, published.RunId, runDeadline, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Program Run disappeared while advancing to its next Step.");
    }

    private static bool CanPublishReturnedTerminal (
        ProgramRunStepRecord step,
        ProgramStepExecutionRecoveredTerminal terminal)
    {
        // The invocation marker is the replay fence for every returned terminal.
        // A local call preflight establishes a terminal without starting the
        // Request entry, so it must additionally prove that no Request boundary
        // or its published plan/descriptors were admitted.
        if (!step.ExecutionPortInvoked)
        {
            return false;
        }

        return terminal.Origin != ProgramStepExecutionTerminalOrigin.LocalPreflight
            || (step.Command == "call"
                && step.RequestExecution is null
                && step.RequestPlanRef is null
                && step.OperationDescriptorRefs.Count == 0
                && terminal.State == ProgramStepState.Failed
                && terminal.ApplicationState == ExecutionApplicationState.NotApplied);
    }

    private async ValueTask<ProgramStepExecutionTerminationResult?> RequestTerminationWithinDeadlineAsync (
        ProgramRunRecord current,
        int stepIndex,
        ProgramStepExecutionReference execution,
        string reasonCode,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var operation = await ExecutionDeadlineOperation.ExecuteAsync(
            deadline,
            cancellationToken,
            "Program Step termination began after its fixed recovery deadline.",
            "Program Step termination exceeded its fixed recovery deadline.",
            token => executionPort.RequestTerminationAsync(CreateTermination(current, stepIndex, execution, reasonCode, deadline), token)).ConfigureAwait(false);
        return operation.IsSuccess ? operation.Value : null;
    }

    private async ValueTask<ProgramStepExecutionRecoveryResult?> RecoverWithinDeadlineAsync (
        ProgramRunRecord current,
        int stepIndex,
        ProgramStepExecutionReference execution,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var operation = await ExecutionDeadlineOperation.ExecuteAsync(
            deadline,
            cancellationToken,
            "Program Step recovery began after its fixed deadline.",
            "Program Step recovery exceeded its fixed deadline.",
            token => executionPort.RecoverAsync(CreateRecovery(current, stepIndex, execution, deadline), token)).ConfigureAwait(false);
        return operation.IsSuccess ? operation.Value : null;
    }

    private static ExecutionDeadline SelectRecoveryDeadline (ExecutionDeadline runDeadline, ExecutionDeadline? stepDeadline)
    {
        if (stepDeadline is null)
        {
            return runDeadline;
        }
        _ = runDeadline.TryGetRemainingTimeout(out var runRemaining);
        _ = stepDeadline.TryGetRemainingTimeout(out var stepRemaining);
        return runRemaining <= stepRemaining ? runDeadline : stepDeadline;
    }

    private static async ValueTask YieldRecoveryBurstAsync (ExecutionDeadline deadline, CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var remaining))
        {
            return;
        }
        await Task.Delay(remaining < RecoveryBurstYield ? remaining : RecoveryBurstYield, deadline.Clock, cancellationToken).ConfigureAwait(false);
    }

    private static ProgramStepExecutionRecovery CreateRecovery (
        ProgramRunRecord current,
        int stepIndex,
        ProgramStepExecutionReference execution,
        ExecutionDeadline deadline)
    {
        _ = deadline.TryGetRemainingTimeout(out var remainingTimeout);
        return new ProgramStepExecutionRecovery(current, stepIndex, execution, deadline, remainingTimeout);
    }

    private static ProgramStepExecutionTermination CreateTermination (
        ProgramRunRecord current,
        int stepIndex,
        ProgramStepExecutionReference execution,
        string reasonCode,
        ExecutionDeadline deadline)
    {
        _ = deadline.TryGetRemainingTimeout(out var remainingTimeout);
        return new ProgramStepExecutionTermination(current, stepIndex, execution, reasonCode, deadline, remainingTimeout);
    }

    private static bool IsKnownApplicationState (ExecutionApplicationState applicationState) => applicationState is
        ExecutionApplicationState.NotApplied or ExecutionApplicationState.Applied or ExecutionApplicationState.PartiallyApplied;

    private ProgramRunRecord PersistStepStart (ProgramRunRecord current, ExecutionDeadline deadline)
    {
        var startedAtUtc = timeProvider.GetUtcNow();
        var executionId = guidGenerator.Generate();
        if (executionId == Guid.Empty)
        {
            throw new InvalidOperationException("Program Step execution identifier generator returned an empty identifier.");
        }
        var execution = new ProgramStepExecutionReference(executionId, startedAtUtc, deadline.UtcDeadline).Validate();
        var steps = current.Steps.Select((step, index) => index == current.Cursor
            ? step with
            {
                State = ProgramStepState.Planning,
                PlanningStartedAtUtc = startedAtUtc,
                DeadlineUtc = deadline.UtcDeadline,
                StartedAtUtc = startedAtUtc,
                Execution = execution,
            }
            : step).ToArray();
        return new ProgramRunRecord(
            current.SchemaVersion, current.Version + 1, current.RunId, current.DefinitionDigest, current.DefinitionSnapshotRef,
            current.Project, current.FixedContext, current.Host, current.StartedGeneration, current.CurrentEditorGeneration,
            current.DeadlineUtc, current.StartedAtUtc, startedAtUtc, ProgramRunState.Running, current.Cursor,
            steps, current.ChildExecutionRefs, current.Cancellation, current.TerminalRecordRef)
        {
            SupervisorObservation = current.SupervisorObservation,
            HostObservation = current.HostObservation,
            TerminalReasonCode = current.TerminalReasonCode,
        };
    }

    private static async ValueTask<ProgramRunRecord?> MarkPortInvocationAsync (
        IProgramRunStore store,
        ProgramRunRecord current,
        CancellationToken cancellationToken)
    {
        var steps = current.Steps.Select((step, index) => index == current.Cursor
            ? step with { ExecutionPortInvoked = true }
            : step).ToArray();
        var replacement = new ProgramRunRecord(
            current.SchemaVersion, current.Version + 1, current.RunId, current.DefinitionDigest, current.DefinitionSnapshotRef,
            current.Project, current.FixedContext, current.Host, current.StartedGeneration, current.CurrentEditorGeneration,
            current.DeadlineUtc, current.StartedAtUtc, current.UpdatedAtUtc, current.State, current.Cursor,
            steps, current.ChildExecutionRefs, current.Cancellation, current.TerminalRecordRef)
        {
            SupervisorObservation = current.SupervisorObservation,
            HostObservation = current.HostObservation,
            TerminalReasonCode = current.TerminalReasonCode,
        };
        var exchange = await store.CompareExchangeAsync(current, replacement, cancellationToken).ConfigureAwait(false);
        return exchange.Exchanged ? exchange.Current : null;
    }

    private async ValueTask<ProgramRunRecord> ReconcileLivenessAsync (IProgramRunStore store, ProgramRunRecord current, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var supervisor = processIdentityObserver.Observe(current.FixedContext.Supervisor.OwnerProcess);
            var host = processIdentityObserver.Observe(current.Host.Process);
            var observed = await PersistObservationsAsync(store, current, supervisor, host, cancellationToken).ConfigureAwait(false);
            current = observed.Run;
            if (!observed.Exchanged)
            {
                if (ProgramRunStateSemantics.IsTerminal(current.State))
                {
                    return current;
                }
                continue;
            }
            if (observed.Supervisor == ProcessIdentityStatus.ExitedOrReplaced)
            {
                return await terminalizer.TerminalizeAsync(store, current, ProgramRunState.Interrupted, "PROGRAM_SUPERVISOR_LOST", cancellationToken).ConfigureAwait(false);
            }
            if (observed.Host == ProcessIdentityStatus.ExitedOrReplaced)
            {
                return await terminalizer.TerminalizeAsync(store, current, ProgramRunState.Interrupted, "PROGRAM_EXECUTION_HOST_LOST", cancellationToken).ConfigureAwait(false);
            }
            return current;
        }
    }

    private async ValueTask<(bool Exchanged, ProgramRunRecord Run, ProcessIdentityStatus Supervisor, ProcessIdentityStatus Host)> PersistObservationsAsync (
        IProgramRunStore store,
        ProgramRunRecord current,
        ProcessIdentityStatus supervisor,
        ProcessIdentityStatus host,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var replacement = new ProgramRunRecord(
            current.SchemaVersion, current.Version + 1, current.RunId, current.DefinitionDigest, current.DefinitionSnapshotRef,
            current.Project, current.FixedContext, current.Host, current.StartedGeneration, current.CurrentEditorGeneration,
            current.DeadlineUtc, current.StartedAtUtc, now, current.State, current.Cursor, current.Steps,
            current.ChildExecutionRefs, current.Cancellation, current.TerminalRecordRef)
        {
            SupervisorObservation = new ProgramProcessLivenessObservation(supervisor, now),
            HostObservation = new ProgramProcessLivenessObservation(host, now),
        };
        var exchange = await store.CompareExchangeAsync(current, replacement, cancellationToken).ConfigureAwait(false);
        return (exchange.Exchanged, exchange.Current, supervisor, host);
    }
}
