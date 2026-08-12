using MackySoft.Ucli.Application.Features.Programs.Persistence;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Application.Features.Programs.Supervision;

/// <summary> Publishes the immutable terminal facts needed to stop an existing Program Run. </summary>
internal sealed class ProgramRunTerminalizer
{
    private const int MaximumTerminalizationAttempts = 8;
    private readonly TimeProvider timeProvider;

    public ProgramRunTerminalizer (TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async ValueTask<ProgramRunRecord> TerminalizeAsync (
        IProgramRunStore store,
        ProgramRunRecord current,
        ProgramRunState terminalState,
        string reasonCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(current);
        for (var attempt = 0; attempt < MaximumTerminalizationAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ProgramRunStateSemantics.IsTerminal(current.State))
            {
                return current;
            }

            var terminalStepIndex = Array.FindIndex(current.Steps.ToArray(), static step => ProgramRunStateSemantics.IsOngoing(step.State) && step.ExecutionPortInvoked);
            if (terminalStepIndex >= 0)
            {
                var stepPublication = await PublishStepTerminalAsync(store, current, terminalStepIndex, terminalState, reasonCode, cancellationToken).ConfigureAwait(false);
                current = stepPublication.Current;
                if (stepPublication.RequiresRetry)
                {
                    continue;
                }
            }
            else if (string.Equals(reasonCode, "PROGRAM_RUN_TIMEOUT", StringComparison.Ordinal)
                && current.Steps.Any(static step => ProgramRunStateSemantics.IsOngoing(step.State) && !step.ExecutionPortInvoked))
            {
                try
                {
                    return await PublishRunTimeoutWithRestoredPlanningAsync(store, current, terminalState, cancellationToken).ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                    var reloaded = await store.ReadAsync(current.RunId, cancellationToken).ConfigureAwait(false);
                    if (reloaded is null)
                    {
                        throw;
                    }
                    current = reloaded;
                    continue;
                }
            }
            else if (current.Steps.Any(static step => ProgramRunStateSemantics.IsOngoing(step.State)))
            {
                current = await RestoreUninvokedPlanningAsync(store, current, reasonCode, cancellationToken).ConfigureAwait(false);
            }

            var completedAtUtc = timeProvider.GetUtcNow();
            var terminal = new ProgramRunTerminalRecord(
            ProgramRunTerminalRecord.CurrentSchemaVersion, current.Project, current.RunId, current.DefinitionDigest,
            current.DefinitionSnapshotRef, current.DeadlineUtc,
            await GetSourceManifestAsync(store, current, cancellationToken).ConfigureAwait(false),
            current.FixedContext, terminalState, current.Verdict, current.ApplicationState, current.Steps,
            current.ChildExecutionRefs, current.Cancellation, current.CurrentEditorGeneration, current.StartedAtUtc, completedAtUtc)
            {
                FinalSupervisorObservation = current.SupervisorObservation,
                FinalHostObservation = current.HostObservation,
                FinalSupervisorSnapshot = CreateFinalSupervisorSnapshot(current),
                ReasonCode = reasonCode,
            };
            try
            {
                var publication = await store.PublishRunTerminalAsync(current, terminal, terminalRef => CreateRunReplacement(current, terminalState, terminalRef, completedAtUtc, terminalReasonCode: reasonCode), cancellationToken).ConfigureAwait(false);
                return publication.Current;
            }
            catch (InvalidOperationException)
            {
                var reloaded = await store.ReadAsync(current.RunId, cancellationToken).ConfigureAwait(false);
                if (reloaded is null)
                {
                    throw;
                }
                if (ProgramRunStateSemantics.IsTerminal(reloaded.State))
                {
                    return reloaded;
                }
                current = reloaded;
            }
        }
        throw new InvalidOperationException("Program Run terminal publication did not converge within the bounded reconciliation attempts.");
    }

    private async ValueTask<ProgramRunRecord> RestoreUninvokedPlanningAsync (IProgramRunStore store, ProgramRunRecord current, string reasonCode, CancellationToken cancellationToken)
    {
        var steps = current.Steps.Select(step => ProgramRunStateSemantics.IsOngoing(step.State) && !step.ExecutionPortInvoked
            ? step with
            {
                State = ProgramStepState.Deferred,
                Verdict = null,
                PlanningStartedAtUtc = null,
                DeadlineUtc = null,
                ApplicationState = ExecutionApplicationState.NotApplied,
                GenerationBefore = null,
                GenerationAfter = null,
                LifecycleExecutionRef = null,
                RequestExecution = null,
                ArtifactRefs = [],
                ResultRef = null,
                StepResultRef = null,
                ErrorCode = null,
                StartedAtUtc = null,
                CompletedAtUtc = null,
                Execution = null,
                ExecutionPortInvoked = false,
            }
            : step).ToArray();
        var replacement = CreateRunReplacement(current, current.State, current.TerminalRecordRef, timeProvider.GetUtcNow(), steps);
        var exchange = await store.CompareExchangeAsync(current, replacement, cancellationToken).ConfigureAwait(false);
        return exchange.Exchanged ? exchange.Current : exchange.Current;
    }

    private async ValueTask<ProgramRunRecord> PublishRunTimeoutWithRestoredPlanningAsync (
        IProgramRunStore store,
        ProgramRunRecord current,
        ProgramRunState terminalState,
        CancellationToken cancellationToken)
    {
        var stepIndex = Array.FindIndex(current.Steps.ToArray(), static step => ProgramRunStateSemantics.IsOngoing(step.State) && !step.ExecutionPortInvoked);
        if (stepIndex < 0)
        {
            throw new InvalidOperationException("Program Run timeout restoration requires an unadmitted planning Step.");
        }
        var steps = current.Steps.Select(step => ProgramRunStateSemantics.IsOngoing(step.State) && !step.ExecutionPortInvoked
            ? step with
            {
                State = ProgramStepState.Deferred,
                Verdict = null,
                ApplicationState = ExecutionApplicationState.NotApplied,
                ResultRef = null,
                StepResultRef = null,
                ErrorCode = null,
                StartedAtUtc = null,
                CompletedAtUtc = null,
                Execution = null,
                ExecutionPortInvoked = false,
            }
            : step).ToArray();
        var completedAtUtc = timeProvider.GetUtcNow();
        var terminal = new ProgramRunTerminalRecord(
            ProgramRunTerminalRecord.CurrentSchemaVersion, current.Project, current.RunId, current.DefinitionDigest,
            current.DefinitionSnapshotRef, current.DeadlineUtc,
            await GetSourceManifestAsync(store, current, cancellationToken).ConfigureAwait(false),
            current.FixedContext, terminalState, current.Verdict, ProgramRunRecord.DeriveApplicationState(steps), steps,
            current.ChildExecutionRefs, current.Cancellation, current.CurrentEditorGeneration, current.StartedAtUtc, completedAtUtc)
        {
            FinalSupervisorObservation = current.SupervisorObservation,
            FinalHostObservation = current.HostObservation,
            FinalSupervisorSnapshot = CreateFinalSupervisorSnapshot(current),
            ReasonCode = "PROGRAM_RUN_TIMEOUT",
        };
        var publication = await store.PublishRunTimeoutTerminalAsync(
            current,
            stepIndex,
            terminal,
            terminalRef => CreateRunReplacement(current, terminalState, terminalRef, completedAtUtc, steps, "PROGRAM_RUN_TIMEOUT"),
            cancellationToken).ConfigureAwait(false);
        return publication.Current;
    }

    internal async ValueTask<ProgramRunRecord> PublishRecoveredTerminalAsync (
        IProgramRunStore store,
        ProgramRunRecord current,
        int stepIndex,
        ProgramStepExecutionRecoveredTerminal recovered,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(current);
        recovered.Validate();
        for (var attempt = 0; attempt < MaximumTerminalizationAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ProgramRunStateSemantics.IsTerminal(current.State))
            {
                return current;
            }
            if (stepIndex < 0 || stepIndex >= current.Steps.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(stepIndex));
            }
            var step = current.Steps[stepIndex];
            if (!ProgramRunStateSemantics.IsOngoing(step.State))
            {
                return current;
            }
            var completedAtUtc = timeProvider.GetUtcNow();
            var terminal = new ProgramStepTerminalRecord(
                ProgramStepTerminalRecord.CurrentSchemaVersion, current.RunId, current.DefinitionDigest, stepIndex,
                step.Command, recovered.State, recovered.Verdict, recovered.ApplicationState, step.GenerationBefore, step.GenerationAfter,
                step.RequestPlanRef, step.OperationDescriptorRefs, step.LifecycleExecutionRef, step.StepResultRef,
                step.ArtifactRefs, recovered.ErrorCode, step.StartedAtUtc, completedAtUtc);
            try
            {
                var publication = await store.PublishStepTerminalAsync(current, stepIndex, terminal, terminalRef =>
                {
                    var steps = current.Steps.Select((candidate, index) => index == stepIndex
                        ? candidate with
                        {
                            State = recovered.State,
                            Verdict = recovered.Verdict,
                            ApplicationState = recovered.ApplicationState,
                            ErrorCode = recovered.ErrorCode,
                            CompletedAtUtc = completedAtUtc,
                            ResultRef = terminalRef,
                        }
                        : candidate).ToArray();
                    return CreateRunReplacement(current, current.State, current.TerminalRecordRef, completedAtUtc, steps);
                }, cancellationToken).ConfigureAwait(false);
                return publication.Current;
            }
            catch (InvalidOperationException)
            {
                var reloaded = await store.ReadAsync(current.RunId, cancellationToken).ConfigureAwait(false);
                if (reloaded is null)
                {
                    throw;
                }
                if (ProgramRunStateSemantics.IsTerminal(reloaded.State)
                    || !ProgramRunStateSemantics.IsOngoing(reloaded.Steps[stepIndex].State))
                {
                    return reloaded;
                }
                if (reloaded.Steps[stepIndex].Execution != step.Execution)
                {
                    return reloaded;
                }
                current = reloaded;
            }
        }
        throw new InvalidOperationException("Recovered Program Step terminal publication did not converge within the bounded reconciliation attempts.");
    }

    /// <summary>
    /// Fails a locally expired Step that was planned but whose execution port
    /// was never admitted. No execution identity or application effect remains.
    /// </summary>
    internal async ValueTask<ProgramRunRecord> TerminalizeUnstartedStepTimeoutAsync (
        IProgramRunStore store,
        ProgramRunRecord current,
        int stepIndex,
        CancellationToken cancellationToken)
    {
        var step = current.Steps[stepIndex];
        if (step.ExecutionPortInvoked)
        {
            throw new InvalidOperationException("An invoked Program Step must be recovered instead of being treated as unstarted.");
        }
        var completedAtUtc = timeProvider.GetUtcNow();
        var terminal = new ProgramStepTerminalRecord(
            ProgramStepTerminalRecord.CurrentSchemaVersion, current.RunId, current.DefinitionDigest, stepIndex,
            step.Command, ProgramStepState.Failed, null, ExecutionApplicationState.NotApplied, step.GenerationBefore,
            step.GenerationAfter, step.RequestPlanRef, step.OperationDescriptorRefs, step.LifecycleExecutionRef,
            step.StepResultRef, step.ArtifactRefs, "PROGRAM_STEP_TIMEOUT", null, completedAtUtc);
        var publication = await store.PublishStepTerminalAsync(current, stepIndex, terminal, terminalRef =>
        {
            var steps = current.Steps.Select((candidate, index) => index == stepIndex
                ? candidate with
                {
                    State = ProgramStepState.Failed,
                    Verdict = null,
                    ApplicationState = ExecutionApplicationState.NotApplied,
                    ErrorCode = "PROGRAM_STEP_TIMEOUT",
                    StartedAtUtc = null,
                    CompletedAtUtc = completedAtUtc,
                    Execution = null,
                    ExecutionPortInvoked = false,
                    ResultRef = terminalRef,
                }
                : candidate).ToArray();
            return CreateRunReplacement(current, current.State, current.TerminalRecordRef, completedAtUtc, steps);
        }, cancellationToken).ConfigureAwait(false);
        return await TerminalizeAsync(store, publication.Current, ProgramRunState.Failed, "PROGRAM_STEP_TIMEOUT", cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<ProgramDefinitionSnapshotManifest> GetSourceManifestAsync (
        IProgramRunStore store,
        ProgramRunRecord run,
        CancellationToken cancellationToken)
    {
        var stored = await store.ReadDefinitionAsync(run.RunId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Program Run definition is unavailable.");
        return ProgramDefinitionSnapshotManifest.FromResolved(stored.Definition.SourceManifest);
    }

    private async ValueTask<ProgramStepTerminalPublicationAttempt> PublishStepTerminalAsync (
        IProgramRunStore store,
        ProgramRunRecord current,
        int stepIndex,
        ProgramRunState runTerminalState,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        var completedAtUtc = timeProvider.GetUtcNow();
        var state = runTerminalState == ProgramRunState.Cancelled
            ? ProgramStepState.Cancelled
            : ProgramStepState.Interrupted;
        var step = current.Steps[stepIndex];
        var terminal = new ProgramStepTerminalRecord(
            ProgramStepTerminalRecord.CurrentSchemaVersion, current.RunId, current.DefinitionDigest, stepIndex,
            step.Command, state, null, ExecutionApplicationState.Unknown, step.GenerationBefore, step.GenerationAfter,
            step.RequestPlanRef, step.OperationDescriptorRefs, step.LifecycleExecutionRef, step.StepResultRef,
            step.ArtifactRefs, reasonCode, step.StartedAtUtc, completedAtUtc);
        try
        {
            var publication = await store.PublishStepTerminalAsync(current, stepIndex, terminal, terminalRef =>
            {
                var steps = current.Steps.Select((candidate, index) => index == stepIndex
                    ? candidate with
                    {
                        State = state,
                        Verdict = null,
                        ApplicationState = ExecutionApplicationState.Unknown,
                        ErrorCode = reasonCode,
                        CompletedAtUtc = completedAtUtc,
                        ResultRef = terminalRef,
                    }
                    : candidate).ToArray();
                return CreateRunReplacement(current, current.State, current.TerminalRecordRef, completedAtUtc, steps);
            }, cancellationToken).ConfigureAwait(false);
            return new ProgramStepTerminalPublicationAttempt(publication.Current, false);
        }
        catch (InvalidOperationException)
        {
            var reloaded = await store.ReadAsync(current.RunId, cancellationToken).ConfigureAwait(false);
            if (reloaded is null)
            {
                throw;
            }
            if (ProgramRunStateSemantics.IsTerminal(reloaded.State)
                || !ProgramRunStateSemantics.IsOngoing(reloaded.Steps[stepIndex].State))
            {
                return new ProgramStepTerminalPublicationAttempt(reloaded, false);
            }
            // The outer bounded terminalization loop re-evaluates the reloaded
            // aggregate; do not recurse while another writer remains active.
            return new ProgramStepTerminalPublicationAttempt(reloaded, true);
        }
    }

    private sealed record ProgramStepTerminalPublicationAttempt (ProgramRunRecord Current, bool RequiresRetry);

    private static ProgramRunRecord CreateRunReplacement (
        ProgramRunRecord current,
        ProgramRunState state,
        ArtifactRef? terminalRef,
        DateTimeOffset updatedAtUtc,
        IReadOnlyList<ProgramRunStepRecord>? steps = null,
        string? terminalReasonCode = null) => new(
        current.SchemaVersion, current.Version + 1, current.RunId, current.DefinitionDigest, current.DefinitionSnapshotRef,
        current.Project, current.FixedContext, current.Host, current.StartedGeneration, current.CurrentEditorGeneration,
        current.DeadlineUtc, current.StartedAtUtc, updatedAtUtc, state, current.Cursor,
        steps ?? current.Steps, current.ChildExecutionRefs, current.Cancellation, terminalRef)
        {
            SupervisorObservation = current.SupervisorObservation,
            HostObservation = current.HostObservation,
            TerminalReasonCode = terminalReasonCode ?? current.TerminalReasonCode,
        };

    private static ProgramAttachedSupervisorSnapshot CreateFinalSupervisorSnapshot (ProgramRunRecord run)
    {
        var initial = run.FixedContext.Supervisor;
        var observation = run.SupervisorObservation;
        var lost = observation?.Status == MackySoft.Ucli.Application.Shared.Execution.Process.ProcessIdentityStatus.ExitedOrReplaced;
        return new ProgramAttachedSupervisorSnapshot(
            initial.SupervisorId,
            initial.HostId,
            initial.OwnerProcess,
            lost ? ProgramSupervisorConnection.Lost : initial.Connection,
            lost ? ProgramSupervisorAvailability.Unavailable : initial.Availability,
            observation?.ObservedAtUtc ?? initial.LastObservedAtUtc).Validate();
    }
}
