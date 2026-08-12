using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

namespace MackySoft.Tests;

internal sealed class RecordingPlayExitService : IPlayExitService
{
    private readonly Func<LifecycleExecutionStartInvocation, CancellationToken, ValueTask<PlayExitExecutionResult>> handler;

    public RecordingPlayExitService (Func<LifecycleExecutionStartInvocation, CancellationToken, ValueTask<PlayExitExecutionResult>> handler)
    {
        this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public List<CommandServiceInvocation<LifecycleExecutionStartInvocation>> Invocations { get; } = [];

    public ValueTask<PlayExitExecutionResult> StartAsync (
        LifecycleExecutionStartInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        Invocations.Add(new CommandServiceInvocation<LifecycleExecutionStartInvocation>(invocation, cancellationToken));
        return handler(invocation, cancellationToken);
    }

    public ValueTask<PlayExitExecutionResult> ReconnectAsync (
        LifecycleExecutionReconnectInvocation invocation,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Fixed-context Play exit reconnect was not expected.");

}
