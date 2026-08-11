using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;

/// <summary> Executes the typed project-refresh application workflow. </summary>
internal interface IRefreshService
{
    /// <summary>
    /// Starts one refresh through a project, host, and deadline fixed by the caller before
    /// Lifecycle Execution registration. Program callers use this entry to durably observe the
    /// provider-confirmed start before Unity receives the refresh action.
    /// </summary>
    ValueTask<RefreshExecutionResult> StartAsync (
        Guid requestId,
        LifecycleExecutionStartInvocation invocation,
        bool failFast,
        CancellationToken cancellationToken = default);

    /// <summary> Reconnects through the caller-fixed binding for an existing refresh execution. </summary>
    ValueTask<RefreshExecutionResult> ReconnectAsync (
        Guid requestId,
        LifecycleExecutionReconnectInvocation invocation,
        CancellationToken cancellationToken = default);

}
