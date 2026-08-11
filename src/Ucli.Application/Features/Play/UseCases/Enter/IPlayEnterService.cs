using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Features.Play.UseCases.Enter;

/// <summary> Provides Play Mode enter workflow execution. </summary>
internal interface IPlayEnterService
{
    /// <summary> Starts Play Mode enter through the caller-fixed Lifecycle Execution context. </summary>
    ValueTask<PlayEnterExecutionResult> StartAsync (
        LifecycleExecutionStartInvocation invocation,
        CancellationToken cancellationToken = default);

    /// <summary> Reconnects Play Mode enter through the caller-fixed Lifecycle Execution context. </summary>
    ValueTask<PlayEnterExecutionResult> ReconnectAsync (
        LifecycleExecutionReconnectInvocation invocation,
        CancellationToken cancellationToken = default);

}
