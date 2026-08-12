using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

namespace MackySoft.Tests;

internal sealed class RecordingPlayEnterService : IPlayEnterService
{
    private readonly Func<LifecycleExecutionStartInvocation, CancellationToken, ValueTask<PlayEnterExecutionResult>> handler;

    public RecordingPlayEnterService (Func<LifecycleExecutionStartInvocation, CancellationToken, ValueTask<PlayEnterExecutionResult>> handler)
    {
        this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public List<CommandServiceInvocation<LifecycleExecutionStartInvocation>> Invocations { get; } = [];

    public ValueTask<PlayEnterExecutionResult> StartAsync (
        LifecycleExecutionStartInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        Invocations.Add(new CommandServiceInvocation<LifecycleExecutionStartInvocation>(invocation, cancellationToken));
        return handler(invocation, cancellationToken);
    }

    public ValueTask<PlayEnterExecutionResult> ReconnectAsync (
        LifecycleExecutionReconnectInvocation invocation,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Fixed-context Play enter reconnect was not expected.");

}
