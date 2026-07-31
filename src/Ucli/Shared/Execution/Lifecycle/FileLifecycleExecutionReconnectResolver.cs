using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;

namespace MackySoft.Ucli.Shared.Execution.Lifecycle;

/// <summary>
/// Resolves one published Lifecycle Execution reference against its guarded project-local start
/// record without issuing an action-provider request.
/// </summary>
internal sealed class FileLifecycleExecutionReconnectResolver :
    ILifecycleExecutionReconnectResolver
{
    /// <inheritdoc />
    public async ValueTask<LifecycleExecutionReconnectResolution> ResolveAsync (
        ResolvedUnityProjectContext project,
        LifecycleExecutionDefinition expectedDefinition,
        ExecutionRef executionRef,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(expectedDefinition);
        ArgumentNullException.ThrowIfNull(executionRef);
        cancellationToken.ThrowIfCancellationRequested();

        if (executionRef.Kind != expectedDefinition.ExecutionKind)
        {
            return RejectDefinition(
                $"Lifecycle Execution kind '{executionRef.Kind.Value}' cannot reconnect through "
                + $"the '{expectedDefinition.ExecutionKind.Value}' action handler.");
        }

        var expectedDigest =
            LifecycleExecutionDefinitionDigest.Calculate(expectedDefinition);
        if (executionRef.DefinitionDigest != expectedDigest)
        {
            return RejectDefinition(
                "Lifecycle Execution definition digest does not match the fixed action definition.");
        }

        try
        {
            LifecycleExecutionContractGuard.RequireReference(
                executionRef,
                nameof(executionRef),
                expectedDefinition.Kind,
                allowTerminal: true);
        }
        catch (ArgumentException exception)
        {
            return RejectReference(
                $"Lifecycle Execution reference is not a valid published action reference. {exception.Message}");
        }

        var store = FileLifecycleExecutionStore.CreateForProject(
            project.UnityProjectRoot,
            project.ProjectFingerprint);
        if (!store.Paths.HasExpectedStatusLocator(
                expectedDefinition.Kind,
                executionRef.Id,
                executionRef.StatusLocator))
        {
            return new LifecycleExecutionReconnectResolution.Rejected(
                ApplicationFailure.ContractViolation(
                    "Lifecycle Execution reference does not resolve inside the selected project.",
                    LifecycleExecutionErrorCodes.ProjectMismatch));
        }

        StoredLifecycleExecution? stored = null;
        LifecycleExecutionTerminalRecord? terminalRecord = null;
        try
        {
            stored = await store.ReadAsync(
                    expectedDefinition.Kind,
                    executionRef.Id,
                    cancellationToken)
                .ConfigureAwait(false);
            if (stored is not null
                && (stored.IsPublishing || stored.IsTerminal))
            {
                var publication = await store.TryRecoverTerminalPublicationAsync(
                        expectedDefinition.Kind,
                        executionRef.Id,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!publication.IsSuccess)
                {
                    return ResolveTerminalPublicationFailure(
                        "Lifecycle Execution terminal publication could not be completed and reverified.",
                        publication.ReconnectableReference
                            ?? GetReconnectableReference(stored));
                }
                terminalRecord = publication.TerminalRecord
                    ?? throw new IOException(
                        "Lifecycle Execution terminal publication omitted its reverified record.");
                stored = await store.ReadAsync(
                        expectedDefinition.Kind,
                        executionRef.Id,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException)
        {
            return ResolveTerminalPublicationFailure(
                "Lifecycle Execution terminal publication could not be recovered and reverified. "
                    + exception.Message,
                GetReconnectableReference(stored));
        }

        if (stored is null)
        {
            return new LifecycleExecutionReconnectResolution.Rejected(
                ApplicationFailure.InvalidInput(
                    $"Lifecycle Execution '{executionRef.Kind.Value}/{executionRef.Id:D}' was not found.",
                    UcliCoreErrorCodes.InvalidArgument));
        }

        if (!ProjectIdentityInfo.TryFromHost(
                project,
                stored.Start.Project,
                out _,
                out var mismatchKind))
        {
            return new LifecycleExecutionReconnectResolution.Rejected(
                ApplicationFailure.ContractViolation(
                    $"Lifecycle Execution belongs to a different project identity. Component={mismatchKind}.",
                    LifecycleExecutionErrorCodes.ProjectMismatch));
        }

        var authoritativeReference = stored.CurrentReference;
        if (authoritativeReference.Kind != executionRef.Kind
            || authoritativeReference.Id != executionRef.Id)
        {
            return new LifecycleExecutionReconnectResolution.Rejected(
                ApplicationFailure.InternalError(
                    "Lifecycle Execution store resolved a different execution identity."));
        }

        if (authoritativeReference.DefinitionDigest
            != executionRef.DefinitionDigest)
        {
            return RejectDefinition(
                "Lifecycle Execution id is bound to a different definition digest.");
        }

        if (executionRef.Lifecycle == ExecutionLifecycle.Terminal
            && !Equals(stored.TerminalReference, executionRef))
        {
            return RejectReference(
                "Terminal Lifecycle Execution reference is not the authoritative reference published by the selected project.");
        }

        if (terminalRecord is not null)
        {
            return new LifecycleExecutionReconnectResolution.Terminal(
                (TerminalExecutionRef)authoritativeReference,
                terminalRecord);
        }

        return new LifecycleExecutionReconnectResolution.Open(
            new LifecycleExecutionRegistration(
                expectedDefinition,
                executionRef.Id,
                stored.Start.DeadlineUtc,
                stored.Start.StartedAtUtc),
            authoritativeReference,
            stored.Start);
    }

    private static LifecycleExecutionReconnectResolution RejectDefinition (
        string message)
    {
        return new LifecycleExecutionReconnectResolution.Rejected(
            ApplicationFailure.ContractViolation(
                message,
                LifecycleExecutionErrorCodes.DefinitionConflict));
    }

    private static LifecycleExecutionReconnectResolution RejectReference (
        string message)
    {
        return new LifecycleExecutionReconnectResolution.Rejected(
            ApplicationFailure.InvalidInput(
                message,
                UcliCoreErrorCodes.InvalidArgument));
    }

    private static LifecycleExecutionReconnectResolution
        ResolveTerminalPublicationFailure (
            string message,
            ExecutionRef? reconnectableReference)
    {
        var failure = ApplicationFailure.InternalError(
            message,
            LifecycleExecutionErrorCodes.TerminalPublicationFailed);
        return reconnectableReference is IReconnectableExecutionRef
            ? new LifecycleExecutionReconnectResolution.PublicationFailed(
                failure,
                reconnectableReference)
            : new LifecycleExecutionReconnectResolution.Rejected(failure);
    }

    private static ExecutionRef? GetReconnectableReference (
        StoredLifecycleExecution? stored)
    {
        return stored is null
            ? null
            : LifecycleExecutionReferenceFactory
                .CreateTerminalPublicationFailureProjection(stored);
    }

}
