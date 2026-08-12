using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Tests;

internal sealed class RecordingCompileService : ICompileService
{
    private readonly Func<LifecycleExecutionStartInvocation, ICommandProgressSink?, CancellationToken, ValueTask<CompileExecutionResult>> handler;

    public RecordingCompileService (
        Func<LifecycleExecutionStartInvocation, ICommandProgressSink?, CancellationToken, ValueTask<CompileExecutionResult>> handler)
    {
        this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public List<ProgressCommandServiceInvocation<LifecycleExecutionStartInvocation>> Invocations { get; } = [];

    public ValueTask<CompileExecutionResult> StartAsync (
        LifecycleExecutionStartInvocation invocation,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default)
    {
        Invocations.Add(new ProgressCommandServiceInvocation<LifecycleExecutionStartInvocation>(invocation, progressSink, cancellationToken));
        return handler(invocation, progressSink, cancellationToken);
    }

    public ValueTask<CompileExecutionResult> ReconnectAsync (
        LifecycleExecutionReconnectInvocation invocation,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Fixed-context compile reconnect was not expected.");

}
