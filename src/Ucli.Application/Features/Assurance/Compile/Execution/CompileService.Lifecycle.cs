using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Execution;

internal sealed partial class CompileService
{
    /// <inheritdoc />
    public async ValueTask<CompileExecutionResult> StartAsync (
        LifecycleExecutionStartInvocation invocation,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        cancellationToken.ThrowIfCancellationRequested();

        var context = invocation.Context.Project;
        var project = ProjectIdentityInfo.From(context.UnityProject);
        if (invocation.ExecutionDeadline.IsExpired
            || !registrationIssuer.TryIssueBeforeDeadline(
                Definition,
                invocation.ExecutionDeadline.UtcDeadline,
                out var registration))
        {
            return CompileExecutionResult.Failed(
                CreateTimeoutFailure(invocation.ExecutionDeadline.Timeout),
                project,
                lifecycleExecutionRef: null,
                ExecutionApplicationState.NotApplied);
        }

        var resolvedProgressSink = progressSink ?? NullCommandProgressSink.Instance;
        await EmitStartedAsync(
                resolvedProgressSink,
                registration.ExecutionId,
                project,
                invocation.Context.RequestedMode,
                invocation.Context.HostBinding.Target,
                invocation.ExecutionDeadline.Timeout,
                cancellationToken)
            .ConfigureAwait(false);
        return await ExecuteRegisteredAsync(
                context,
                project,
                registration,
                resolvedProgressSink,
                reconnectedExecutionRef: null,
                requiredStart: null,
                invocation.Context.FailFast,
                cancellationToken,
                (payload, token) => invocation.Context.HostBinding.StartAsync(
                    UcliCommandIds.Compile,
                    payload,
                    invocation,
                    token))
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<CompileExecutionResult> ReconnectAsync (
        LifecycleExecutionReconnectInvocation invocation,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        cancellationToken.ThrowIfCancellationRequested();

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
            return CompileExecutionResult.Failed(
                publicationFailed.Failure,
                project,
                publicationFailed.CurrentReference,
                ExecutionApplicationState.Indeterminate);
        }
        if (reconnectResult is LifecycleExecutionReconnectResolution.Rejected rejected)
        {
            return CompileExecutionResult.Failed(
                rejected.Failure,
                project,
                lifecycleExecutionRef: null,
                ExecutionApplicationState.NotApplied);
        }
        if (reconnectResult is LifecycleExecutionReconnectResolution.Terminal terminal)
        {
            return await CreateResultFromTerminalRecordAsync(
                    project,
                    terminal.ExecutionReference,
                    terminal.TerminalRecord,
                    progressSink ?? NullCommandProgressSink.Instance)
                .ConfigureAwait(false);
        }

        var open = (LifecycleExecutionReconnectResolution.Open)reconnectResult;
        return await ExecuteRegisteredAsync(
                context,
                project,
                open.Registration,
                progressSink ?? NullCommandProgressSink.Instance,
                open.CurrentReference,
                open.RequiredStart,
                failFast: false,
                cancellationToken,
                (payload, token) => invocation.Context.HostBinding.ReconnectAsync(
                    UcliCommandIds.Compile,
                    payload,
                    invocation,
                    token))
            .ConfigureAwait(false);
    }
}
