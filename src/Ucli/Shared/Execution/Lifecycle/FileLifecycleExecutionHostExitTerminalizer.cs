using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;

namespace MackySoft.Ucli.Shared.Execution.Lifecycle;

/// <summary>
/// Owns durable admission reconciliation, conditional publication, and reverification for an
/// action-owned fixed-host-exit Terminal Record.
/// </summary>
internal sealed class FileLifecycleExecutionHostExitTerminalizer :
    ILifecycleExecutionHostExitTerminalizer
{
    /// <inheritdoc />
    public async ValueTask<LifecycleExecutionHostExitTerminalizationResult>
        TerminalizeAsync (
            ResolvedUnityProjectContext project,
            LifecycleExecutionStartBinding start,
            ExecutionRef currentReference,
            LifecycleExecutionTerminalFacts observedTerminalFacts,
            LifecycleExecutionHostExitTerminalRecordFactory
                terminalRecordFactory)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(currentReference);
        ArgumentNullException.ThrowIfNull(terminalRecordFactory);
        if (observedTerminalFacts.ApplicationState
            is not ExecutionApplicationState.NotApplied
            and not ExecutionApplicationState.Indeterminate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(observedTerminalFacts),
                observedTerminalFacts.ApplicationState,
                "A fixed-host-exit observation must retain an unproven application state.");
        }

        var store = FileLifecycleExecutionStore.CreateForProject(
            project.UnityProjectRoot,
            project.ProjectFingerprint);
        var kind = LifecycleExecutionContractGuard.RequireReference(
            start.LifecycleExecutionRef,
            nameof(start),
            allowTerminal: false);
        var applicationState = observedTerminalFacts.ApplicationState;
        try
        {
            while (true)
            {
                var recovery = await store.TryRecoverTerminalPublicationAsync(
                        kind,
                        start.LifecycleExecutionRef.Id,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                if (recovery.IsSuccess)
                {
                    return new LifecycleExecutionHostExitTerminalizationResult.Published(
                        recovery.TerminalReference!,
                        recovery.TerminalRecord!);
                }
                if (recovery.Outcome
                    != LifecycleExecutionTerminalPublicationOutcome
                        .NotPublishing)
                {
                    applicationState =
                        ResolvePublicationFailureApplicationState(
                            recovery,
                            applicationState);
                    return await CreatePublicationFailureAsync(
                            store,
                            start,
                            currentReference,
                            applicationState,
                            recovery.TerminalRecord,
                            "The existing Terminal Record could not be recovered and reverified.")
                        .ConfigureAwait(false);
                }

                var authoritativeExecution = await store.ReadAsync(
                        kind,
                        start.LifecycleExecutionRef.Id,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                if (authoritativeExecution is null)
                {
                    return await CreatePublicationFailureAsync(
                            store,
                            start,
                            currentReference,
                            applicationState,
                            null,
                            "The fixed-host-exit Lifecycle Execution could not be re-read before terminal publication.")
                        .ConfigureAwait(false);
                }
                if (authoritativeExecution.IsTerminal
                    || authoritativeExecution.IsPublishing)
                {
                    continue;
                }

                applicationState = ResolveApplicationState(
                    observedTerminalFacts.ApplicationState,
                    authoritativeExecution);
                var resolvedTerminalFacts = observedTerminalFacts with
                {
                    ApplicationState = applicationState,
                };
                var terminalRecord = terminalRecordFactory(
                        authoritativeExecution.Start,
                        resolvedTerminalFacts)
                    ?? throw new InvalidOperationException(
                        "The action-owned fixed-host-exit Terminal Record factory returned null.");
                if (terminalRecord.ApplicationState != applicationState)
                {
                    throw new InvalidOperationException(
                        "The action-owned fixed-host-exit Terminal Record did not retain the resolved application state.");
                }

                var publication = await store.TryPublishTerminalAsync(
                        authoritativeExecution,
                        terminalRecord,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                if (publication.IsSuccess)
                {
                    return new LifecycleExecutionHostExitTerminalizationResult.Published(
                        publication.TerminalReference!,
                        publication.TerminalRecord!);
                }
                if (publication.Outcome
                        == LifecycleExecutionTerminalPublicationOutcome
                            .Conflict
                    && publication.AuthoritativeExecution is not null)
                {
                    continue;
                }

                return await RecoverAfterPublicationFailureAsync(
                        store,
                        start,
                        currentReference,
                        applicationState,
                        publication.TerminalRecord,
                        terminalRecord.ExecutionKind,
                        terminalRecord.ExecutionId,
                        "The fixed-host-exit Terminal Record could not be published and reverified.")
                    .ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException)
        {
            return await RecoverAfterPublicationFailureAsync(
                    store,
                    start,
                    currentReference,
                    applicationState,
                    null,
                    kind,
                    start.LifecycleExecutionRef.Id,
                    "The fixed-host-exit Terminal Record could not be published and reverified. "
                        + exception.Message)
                .ConfigureAwait(false);
        }
    }

    private static ExecutionApplicationState ResolveApplicationState (
        ExecutionApplicationState observedApplicationState,
        StoredLifecycleExecution authoritativeExecution)
    {
        var durableApplicationState =
            LifecycleExecutionTerminalFactsPolicy
                .ResolveUnprovenApplicationState(
                    authoritativeExecution.CurrentReference,
                    authoritativeExecution
                        .SideEffectRightOwnerEndpointRegistrationGenerationId
                        .HasValue);
        return observedApplicationState
                == ExecutionApplicationState.NotApplied
            && durableApplicationState
                == ExecutionApplicationState.NotApplied
            ? ExecutionApplicationState.NotApplied
            : ExecutionApplicationState.Indeterminate;
    }

    private static async ValueTask<LifecycleExecutionHostExitTerminalizationResult>
        RecoverAfterPublicationFailureAsync (
            FileLifecycleExecutionStore store,
            LifecycleExecutionStartBinding start,
            ExecutionRef currentReference,
            ExecutionApplicationState applicationState,
            LifecycleExecutionTerminalRecord? fixedTerminalRecord,
            LifecycleExecutionKind kind,
            Guid executionId,
            string failureMessage)
    {
        try
        {
            var recovery = await store.TryRecoverTerminalPublicationAsync(
                    kind,
                    executionId,
                    CancellationToken.None)
                .ConfigureAwait(false);
            if (recovery.IsSuccess)
            {
                return new LifecycleExecutionHostExitTerminalizationResult.Published(
                    recovery.TerminalReference!,
                    recovery.TerminalRecord!);
            }

            applicationState =
                ResolvePublicationFailureApplicationState(
                    recovery,
                    applicationState);
            fixedTerminalRecord =
                recovery.TerminalRecord
                ?? fixedTerminalRecord;
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException)
        {
            failureMessage += " Recovery also failed. " + exception.Message;
        }

        return await CreatePublicationFailureAsync(
                store,
                start,
                currentReference,
                applicationState,
                fixedTerminalRecord,
                failureMessage)
            .ConfigureAwait(false);
    }

    private static ExecutionApplicationState
        ResolvePublicationFailureApplicationState (
            LifecycleExecutionTerminalPublicationResult recovery,
            ExecutionApplicationState fallback)
    {
        return recovery.TerminalRecord?.ApplicationState
            ?? fallback;
    }

    private static async ValueTask<LifecycleExecutionHostExitTerminalizationResult>
        CreatePublicationFailureAsync (
            FileLifecycleExecutionStore store,
            LifecycleExecutionStartBinding start,
            ExecutionRef currentReference,
            ExecutionApplicationState applicationState,
            LifecycleExecutionTerminalRecord? fixedTerminalRecord,
            string message)
    {
        var reconnectableReference =
            currentReference is IReconnectableExecutionRef
                ? currentReference
                : start.LifecycleExecutionRef;
        try
        {
            var stored = await store.ReadAsync(
                    start.LifecycleExecutionRef.Kind == currentReference.Kind
                        ? LifecycleExecutionContractGuard.RequireReference(
                            start.LifecycleExecutionRef,
                            nameof(start),
                            allowTerminal: false)
                        : throw new InvalidOperationException(
                            "Lifecycle Execution identity changed during terminal publication."),
                    start.LifecycleExecutionRef.Id,
                    CancellationToken.None)
                .ConfigureAwait(false);
            if (stored?.CurrentReference is IReconnectableExecutionRef)
            {
                reconnectableReference = stored.CurrentReference;
            }
            else if (stored?.Start.LifecycleExecutionRef
                is IReconnectableExecutionRef)
            {
                reconnectableReference =
                    stored.Start.LifecycleExecutionRef;
            }
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException)
        {
            message += " The reconnectable execution reference could not be re-read. "
                + exception.Message;
        }

        return new LifecycleExecutionHostExitTerminalizationResult.PublicationFailed(
            reconnectableReference,
            applicationState,
            ApplicationFailure.InternalError(
                message,
                LifecycleExecutionErrorCodes.TerminalPublicationFailed),
            fixedTerminalRecord);
    }
}
