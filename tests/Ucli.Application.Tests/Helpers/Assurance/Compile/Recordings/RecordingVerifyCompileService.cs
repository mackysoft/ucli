using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingVerifyCompileService : ICompileService
{
    private readonly Func<LifecycleExecutionStartInvocation, CompileExecutionResult> resultFactory;
    private readonly List<Invocation> invocations = [];

    public RecordingVerifyCompileService (Func<LifecycleExecutionStartInvocation, CompileExecutionResult> resultFactory)
    {
        this.resultFactory = resultFactory;
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<CompileExecutionResult> StartAsync (
        LifecycleExecutionStartInvocation invocation,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(invocation, progressSink, cancellationToken));
        return ValueTask.FromResult(resultFactory(invocation));
    }

    public ValueTask<CompileExecutionResult> ReconnectAsync (
        LifecycleExecutionReconnectInvocation invocation,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Compile reconnect was not expected.");
    }

    internal readonly record struct Invocation (
        LifecycleExecutionStartInvocation StartInvocation,
        ICommandProgressSink? ProgressSink,
        CancellationToken CancellationToken);
}
