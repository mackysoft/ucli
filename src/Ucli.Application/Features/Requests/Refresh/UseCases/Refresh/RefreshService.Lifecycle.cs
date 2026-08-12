using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;

internal sealed partial class RefreshService
{
    /// <inheritdoc />
    public async ValueTask<RefreshExecutionResult> StartAsync (
        Guid requestId,
        LifecycleExecutionStartInvocation invocation,
        bool failFast,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request id must not be empty.", nameof(requestId));
        }

        ArgumentNullException.ThrowIfNull(invocation);
        var context = invocation.Context.Project;
        var project = ProjectIdentityInfo.From(context.UnityProject);
        if (invocation.ExecutionDeadline.IsExpired
            || !registrationIssuer.TryIssueBeforeDeadline(
                Definition,
                invocation.ExecutionDeadline.UtcDeadline,
                out var registration))
        {
            return RefreshExecutionResult.Failure(
                ApplicationFailure.Timeout(
                    "Refresh execution deadline elapsed before Lifecycle Execution registration.",
                    LifecycleExecutionErrorCodes.DeadlineExceeded),
                CreatePreStartErrorOutput(project, requestId));
        }

        return await ExecuteRegisteredAsync(
                requestId,
                context,
                project,
                registration,
                new RefreshLifecycleExecutionStartAdmissionPolicy(failFast),
                reconnectedExecutionRef: null,
                requiredStart: null,
                cancellationToken,
                (payload, token) => invocation.Context.HostBinding.StartAsync(
                    UcliCommandIds.Refresh,
                    payload,
                    invocation,
                    token))
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<RefreshExecutionResult> ReconnectAsync (
        Guid requestId,
        LifecycleExecutionReconnectInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request id must not be empty.", nameof(requestId));
        }

        ArgumentNullException.ThrowIfNull(invocation);
        var context = invocation.Context.Project;
        var project = ProjectIdentityInfo.From(context.UnityProject);
        var reconnectResult = await reconnectResolver.ResolveAsync(
                context.UnityProject,
                Definition,
                invocation.ExecutionReference,
                cancellationToken)
            .ConfigureAwait(false);
        if (reconnectResult is LifecycleExecutionReconnectResolution.PublicationFailed publicationFailed)
        {
            return RefreshExecutionResult.Failure(
                publicationFailed.Failure,
                new RefreshExecutionErrorOutput(
                    project,
                    requestId,
                    publicationFailed.CurrentReference,
                    ExecutionApplicationState.Indeterminate,
                    Refresh: null,
                    ObservedLifecycle: null,
                    ReadPostcondition: null));
        }
        if (reconnectResult is LifecycleExecutionReconnectResolution.Rejected rejected)
        {
            return RefreshExecutionResult.Failure(rejected.Failure, CreatePreStartErrorOutput(project, requestId));
        }
        if (reconnectResult is LifecycleExecutionReconnectResolution.Terminal terminal)
        {
            return await CreateResultFromTerminalRecordAsync(
                    requestId,
                    context,
                    project,
                    terminal.ExecutionReference,
                    terminal.TerminalRecord)
                .ConfigureAwait(false);
        }

        var open = (LifecycleExecutionReconnectResolution.Open)reconnectResult;
        return await ExecuteRegisteredAsync(
                requestId,
                context,
                project,
                open.Registration,
                startAdmissionPolicy: null,
                open.CurrentReference,
                open.RequiredStart,
                cancellationToken,
                (payload, token) => invocation.Context.HostBinding.ReconnectAsync(
                    UcliCommandIds.Refresh,
                    payload,
                    invocation,
                    token))
            .ConfigureAwait(false);
    }
}
