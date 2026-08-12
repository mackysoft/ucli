using MackySoft.Ucli.Application.Features.Programs.Persistence;
using MackySoft.Ucli.Application.Shared.Execution.Process;

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
    private readonly ProgramRunTerminalizer terminalizer;

    public ProgramRunStatusCancelReconciliationService (
        IProgramRunStoreFactory storeFactory,
        IProcessIdentityObserver processIdentityObserver,
        TimeProvider timeProvider)
    {
        this.storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
        this.processIdentityObserver = processIdentityObserver ?? throw new ArgumentNullException(nameof(processIdentityObserver));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
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
                return await terminalizer.TerminalizeAsync(store, current, ProgramRunState.Interrupted, "PROGRAM_SUPERVISOR_LOST", cancellationToken).ConfigureAwait(false);
            }
            if (observed.Host == ProcessIdentityStatus.ExitedOrReplaced)
            {
                return await terminalizer.TerminalizeAsync(store, current, ProgramRunState.Interrupted, "PROGRAM_EXECUTION_HOST_LOST", cancellationToken).ConfigureAwait(false);
            }
            if (!cancel)
            {
                return current;
            }
            if (current.Cancellation.Requested)
            {
                return await terminalizer.TerminalizeAsync(store, current, ProgramRunState.Cancelled, "PROGRAM_RUN_CANCELLED", cancellationToken).ConfigureAwait(false);
            }

            var requestedAtUtc = timeProvider.GetUtcNow();
            var replacement = new ProgramRunRecord(
                current.SchemaVersion, current.Version + 1, current.RunId, current.DefinitionDigest, current.DefinitionSnapshotRef,
                current.Project, current.FixedContext, current.Host, current.StartedGeneration, current.CurrentEditorGeneration,
                current.DeadlineUtc, current.StartedAtUtc, requestedAtUtc, current.State, current.Cursor,
                current.Steps, current.ChildExecutionRefs, current.Cancellation.Request(requestedAtUtc, reasonCode), current.TerminalRecordRef)
            {
                SupervisorObservation = current.SupervisorObservation,
                HostObservation = current.HostObservation,
                TerminalReasonCode = current.TerminalReasonCode,
            };
            var exchange = await store.CompareExchangeAsync(current, replacement, cancellationToken).ConfigureAwait(false);
            if (exchange.Exchanged)
            {
                return await terminalizer.TerminalizeAsync(store, exchange.Current, ProgramRunState.Cancelled, "PROGRAM_RUN_CANCELLED", cancellationToken).ConfigureAwait(false);
            }
        }
    }

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
