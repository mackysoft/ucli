using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Features.Play.UseCases.Exit;

/// <summary> Provides Play Mode exit workflow execution. </summary>
internal interface IPlayExitService
{
    /// <summary> Starts Play Mode exit through the caller-fixed Lifecycle Execution context. </summary>
    ValueTask<PlayExitExecutionResult> StartAsync (
        LifecycleExecutionStartInvocation invocation,
        CancellationToken cancellationToken = default);

    /// <summary> Reconnects Play Mode exit through the caller-fixed Lifecycle Execution context. </summary>
    ValueTask<PlayExitExecutionResult> ReconnectAsync (
        LifecycleExecutionReconnectInvocation invocation,
        CancellationToken cancellationToken = default);

}
