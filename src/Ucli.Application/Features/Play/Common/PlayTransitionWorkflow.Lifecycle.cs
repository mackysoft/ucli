using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Application.Features.Play.Common;

internal sealed partial class PlayTransitionWorkflow
{
    public async ValueTask<PlayTransitionWorkflowResult<TOutput>> StartAsync<TOutput> (
        LifecycleExecutionStartInvocation invocation,
        IPlayTransitionDirectionPolicy<TOutput> direction,
        CancellationToken cancellationToken)
        where TOutput : class
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(direction);
        cancellationToken.ThrowIfCancellationRequested();

        var project = ProjectIdentityInfo.From(invocation.Context.Project.UnityProject);
        if (invocation.ExecutionDeadline.IsExpired
            || !registrationIssuer.TryIssueBeforeDeadline(
                direction.Definition,
                invocation.ExecutionDeadline.UtcDeadline,
                out var registration))
        {
            return PlayTransitionWorkflowResult<TOutput>.Failure(
                ApplicationFailure.Timeout(
                    "Play transition execution deadline elapsed before Lifecycle Execution registration.",
                    LifecycleExecutionErrorCodes.DeadlineExceeded));
        }

        var context = new PlayCommandExecutionContext(
            invocation.Context.Project,
            project,
            Session: null,
            invocation.ExecutionDeadline.Timeout,
            checked((int)invocation.ExecutionDeadline.Timeout.TotalMilliseconds));
        return await ExecuteRegisteredAsync(
                context,
                registration,
                establishedExecutionReference: null,
                requiredStart: null,
                direction,
                invocation.Context.FailFast,
                cancellationToken,
                (payload, token) => invocation.Context.HostBinding.StartAsync(
                    direction.Command,
                    payload,
                    invocation,
                    token))
            .ConfigureAwait(false);
    }

    public async ValueTask<PlayTransitionWorkflowResult<TOutput>> ReconnectAsync<TOutput> (
        LifecycleExecutionReconnectInvocation invocation,
        IPlayTransitionDirectionPolicy<TOutput> direction,
        CancellationToken cancellationToken)
        where TOutput : class
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(direction);
        cancellationToken.ThrowIfCancellationRequested();

        var projectContext = invocation.Context.Project;
        var project = ProjectIdentityInfo.From(projectContext.UnityProject);
        var context = new PlayCommandExecutionContext(
            projectContext,
            project,
            Session: null,
            invocation.CallerWaitDeadline.Timeout,
            checked((int)invocation.CallerWaitDeadline.Timeout.TotalMilliseconds));
        var resolution = await reconnectResolver.ResolveAsync(
                projectContext.UnityProject,
                direction.Definition,
                invocation.ExecutionReference,
                cancellationToken)
            .ConfigureAwait(false);
        if (resolution is LifecycleExecutionReconnectResolution.PublicationFailed publicationFailed)
        {
            return PlayTransitionWorkflowResult<TOutput>.Failure(
                publicationFailed.Failure,
                CreateFailureContext(context, publicationFailed.CurrentReference, ExecutionApplicationState.Indeterminate));
        }
        if (resolution is LifecycleExecutionReconnectResolution.Rejected rejected)
        {
            return PlayTransitionWorkflowResult<TOutput>.Failure(rejected.Failure);
        }
        if (resolution is LifecycleExecutionReconnectResolution.Terminal terminal)
        {
            return CreateResultFromTerminalRecord(
                context,
                terminal.ExecutionReference,
                terminal.TerminalRecord,
                direction);
        }

        var open = (LifecycleExecutionReconnectResolution.Open)resolution;
        return await ExecuteRegisteredAsync(
                context,
                open.Registration,
                open.CurrentReference,
                open.RequiredStart,
                direction,
                failFast: false,
                cancellationToken,
                (payload, token) => invocation.Context.HostBinding.ReconnectAsync(
                    direction.Command,
                    payload,
                    invocation,
                    token))
            .ConfigureAwait(false);
    }
}
