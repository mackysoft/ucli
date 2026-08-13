using MackySoft.Ucli.Application.Features.Programs.Persistence;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Process;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Features.Programs.Supervision;

/// <summary>
/// Reconciles saved Program Run facts for status and cancellation without
/// starting Steps, calling an execution port, or transferring ownership.
/// </summary>
internal sealed class ProgramRunStatusCancelReconciliationService
{
    private readonly IProgramRunStoreFactory storeFactory;
    private readonly IProcessIdentityObserver processIdentityObserver;
    private readonly TimeProvider timeProvider;
    private readonly ILifecycleExecutionReconnectResolver? lifecycleReconnectResolver;
    private readonly ProgramRunTerminalizer terminalizer;

    public ProgramRunStatusCancelReconciliationService (
        IProgramRunStoreFactory storeFactory,
        IProcessIdentityObserver processIdentityObserver,
        TimeProvider timeProvider,
        ILifecycleExecutionReconnectResolver? lifecycleReconnectResolver = null)
    {
        this.storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
        this.processIdentityObserver = processIdentityObserver ?? throw new ArgumentNullException(nameof(processIdentityObserver));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.lifecycleReconnectResolver = lifecycleReconnectResolver;
        terminalizer = new ProgramRunTerminalizer(timeProvider);
    }

    public ValueTask<ProgramRunRecord?> GetStatusAsync (ResolvedUnityProjectContext project, Guid runId, CancellationToken cancellationToken = default) =>
        ReconcileAsync(project, runId, cancel: false, null, cancellationToken);

    public ValueTask<ProgramRunRecord?> CancelAsync (ResolvedUnityProjectContext project, Guid runId, string? reasonCode, CancellationToken cancellationToken = default) =>
        ReconcileAsync(project, runId, cancel: true, reasonCode, cancellationToken);

    private async ValueTask<ProgramRunRecord?> ReconcileAsync (
        ResolvedUnityProjectContext project,
        Guid runId,
        bool cancel,
        string? reasonCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        var store = storeFactory.ForProject(project);
        while (true)
        {
            var current = await store.ReadAsync(runId, cancellationToken).ConfigureAwait(false);
            if (current is null || ProgramRunStateSemantics.IsTerminal(current.State))
            {
                return current;
            }
            var observed = await RecordLivenessAsync(store, current, cancellationToken).ConfigureAwait(false);
            if (!observed.Exchanged)
            {
                // A status/cancel request must make its decision from the facts it
                // successfully observed and recorded, never from a stale snapshot.
                continue;
            }
            current = observed.Run;
            if (observed.Supervisor == ProcessIdentityStatus.ExitedOrReplaced)
            {
                return await ReconcileLossAsync(
                    project, store, current, "PROGRAM_SUPERVISOR_LOST", cancellationToken).ConfigureAwait(false);
            }
            if (observed.Host == ProcessIdentityStatus.ExitedOrReplaced)
            {
                return await ReconcileLossAsync(
                    project, store, current, "PROGRAM_EXECUTION_HOST_LOST", cancellationToken).ConfigureAwait(false);
            }
            if (!cancel)
            {
                return current;
            }
            if (current.Cancellation.Requested)
            {
                return current;
            }

            var requestedAtUtc = timeProvider.GetUtcNow();
            var replacement = new ProgramRunRecord(
                current.SchemaVersion, current.Version + 1, current.RunId, current.DefinitionDigest, current.DefinitionSnapshotRef,
                current.Project, current.FixedContext, current.Host, current.StartedGeneration, current.CurrentEditorGeneration,
                current.DeadlineUtc, current.StartedAtUtc, requestedAtUtc, ProgramRunState.Cancelling, current.Cursor,
                current.Steps, current.ChildExecutionRefs, current.Cancellation.Request(requestedAtUtc, reasonCode), current.TerminalRecordRef)
            {
                SupervisorObservation = current.SupervisorObservation,
                HostObservation = current.HostObservation,
                TerminalReasonCode = current.TerminalReasonCode,
            };
            var exchange = await store.CompareExchangeAsync(current, replacement, cancellationToken).ConfigureAwait(false);
            if (exchange.Exchanged)
            {
                if (!exchange.Current.Steps.Any(static step => step.ExecutionPortInvoked))
                {
                    return await terminalizer.TerminalizeAsync(
                        store, exchange.Current, ProgramRunState.Cancelled, "PROGRAM_RUN_CANCELLED", cancellationToken).ConfigureAwait(false);
                }
                // This reconciliation path is not the attached Supervisor. It
                // records the cancellation fence but cannot claim that an
                // already-started execution has stopped or that its final
                // application state is known.
                return exchange.Current;
            }
        }
    }

    /// <summary>
    /// Performs the sole recovery permitted after the attached owner or fixed
    /// host is lost: project an already-published terminal record for the
    /// exact Lifecycle Execution recorded by the active Step. This path never
    /// reconnects an open action, starts a replacement action, or advances a
    /// Program frontier.
    /// </summary>
    private async ValueTask<ProgramRunRecord> ReconcileLossAsync (
        ResolvedUnityProjectContext project,
        IProgramRunStore store,
        ProgramRunRecord current,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        var recovered = await TryRecoverLifecycleTerminalAsync(project, current, cancellationToken).ConfigureAwait(false);
        if (recovered is not null)
        {
            current = await terminalizer.PublishRecoveredTerminalAsync(
                store, current, current.Cursor, recovered, cancellationToken).ConfigureAwait(false);
        }
        return await terminalizer.TerminalizeAsync(
            store, current, ProgramRunState.Interrupted, reasonCode, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ProgramStepExecutionRecoveredTerminal?> TryRecoverLifecycleTerminalAsync (
        ResolvedUnityProjectContext project,
        ProgramRunRecord run,
        CancellationToken cancellationToken)
    {
        if (lifecycleReconnectResolver is null
            || run.Cursor < 0
            || run.Cursor >= run.Steps.Count)
        {
            return null;
        }

        var step = run.Steps[run.Cursor];
        if (!ProgramRunStateSemantics.IsOngoing(step.State)
            || step.LifecycleExecutionRef is null
            || !TryGetLifecycleKind(step.Command, out var kind))
        {
            return null;
        }

        var expectedReference = step.LifecycleExecutionRef;
        var resolution = await lifecycleReconnectResolver.ResolveAsync(
                project,
                new LifecycleExecutionDefinition(kind),
                expectedReference,
                cancellationToken)
            .ConfigureAwait(false);
        if (resolution is not LifecycleExecutionReconnectResolution.Terminal terminal
            || !HasSameLifecycleIdentity(expectedReference, terminal.ExecutionReference)
            || !HasExpectedTerminalRecord(step.Command, terminal.TerminalRecord)
            || terminal.TerminalRecord.Project != run.Project
            || !ProgramRunRecord.HasSameProgramFixedHost(run.Host, terminal.TerminalRecord.Host)
            || terminal.TerminalRecord.StartedGeneration != (step.GenerationBefore ?? run.CurrentEditorGeneration ?? run.StartedGeneration)
            || terminal.TerminalRecord.TerminalGeneration is null)
        {
            return null;
        }

        var record = terminal.TerminalRecord;
        return record.TerminalReason == LifecycleExecutionTerminalReason.Completed
            ? new ProgramStepExecutionRecoveredTerminal(
                ProgramStepState.Completed,
                record.Verdict,
                record.ApplicationState,
                null,
                record.TerminalGeneration,
                terminal.ExecutionReference)
            : new ProgramStepExecutionRecoveredTerminal(
                ProgramStepState.Failed,
                null,
                record.ApplicationState,
                GetTerminalReasonCode(record.TerminalReason),
                record.TerminalGeneration,
                terminal.ExecutionReference);
    }

    private static bool HasSameLifecycleIdentity (ExecutionRef expected, ExecutionRef actual) =>
        expected.Kind == actual.Kind
        && expected.Id == actual.Id
        && expected.DefinitionDigest == actual.DefinitionDigest;

    private static bool TryGetLifecycleKind (string command, out LifecycleExecutionKind kind)
    {
        switch (command)
        {
            case "refresh":
                kind = LifecycleExecutionKind.Refresh;
                return true;
            case "compile":
                kind = LifecycleExecutionKind.Compile;
                return true;
            case "play.enter":
                kind = LifecycleExecutionKind.PlayEnter;
                return true;
            case "play.exit":
                kind = LifecycleExecutionKind.PlayExit;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static bool HasExpectedTerminalRecord (string command, LifecycleExecutionTerminalRecord terminalRecord) => command switch
    {
        "refresh" => terminalRecord is RefreshLifecycleExecutionTerminalRecord,
        "compile" => terminalRecord is CompileLifecycleExecutionTerminalRecord,
        "play.enter" => terminalRecord is PlayEnterLifecycleExecutionTerminalRecord,
        "play.exit" => terminalRecord is PlayExitLifecycleExecutionTerminalRecord,
        _ => false,
    };

    private static string GetTerminalReasonCode (LifecycleExecutionTerminalReason reason) => reason switch
    {
        LifecycleExecutionTerminalReason.ActionFailed => "LIFECYCLE_EXECUTION_ACTION_FAILED",
        LifecycleExecutionTerminalReason.DeadlineExceeded => "LIFECYCLE_EXECUTION_DEADLINE_EXCEEDED",
        LifecycleExecutionTerminalReason.ProjectMismatch => "LIFECYCLE_EXECUTION_PROJECT_MISMATCH",
        LifecycleExecutionTerminalReason.HostMismatch => "LIFECYCLE_EXECUTION_HOST_MISMATCH",
        LifecycleExecutionTerminalReason.GenerationMismatch => "LIFECYCLE_EXECUTION_GENERATION_MISMATCH",
        LifecycleExecutionTerminalReason.UnityExited => "LIFECYCLE_EXECUTION_UNITY_EXITED",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    private async ValueTask<(bool Exchanged, ProgramRunRecord Run, ProcessIdentityStatus Supervisor, ProcessIdentityStatus Host)> RecordLivenessAsync (
        IProgramRunStore store,
        ProgramRunRecord current,
        CancellationToken cancellationToken)
    {
        var supervisor = processIdentityObserver.Observe(current.FixedContext.Supervisor.OwnerProcess);
        var host = processIdentityObserver.Observe(current.Host.Process);
        var now = timeProvider.GetUtcNow();
        var replacement = new ProgramRunRecord(
            current.SchemaVersion, current.Version + 1, current.RunId, current.DefinitionDigest, current.DefinitionSnapshotRef,
            current.Project, current.FixedContext, current.Host, current.StartedGeneration, current.CurrentEditorGeneration,
            current.DeadlineUtc, current.StartedAtUtc, now, current.State, current.Cursor, current.Steps,
            current.ChildExecutionRefs, current.Cancellation, current.TerminalRecordRef)
        {
            SupervisorObservation = new ProgramProcessLivenessObservation(supervisor, now).Validate(),
            HostObservation = new ProgramProcessLivenessObservation(host, now).Validate(),
        };
        var exchange = await store.CompareExchangeAsync(current, replacement, cancellationToken).ConfigureAwait(false);
        return (exchange.Exchanged, exchange.Current, supervisor, host);
    }
}
