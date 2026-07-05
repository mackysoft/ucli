using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Tests;

internal abstract class RecordingProgressCommandService<TInput, TResult>
{
    private readonly Func<TInput, ICommandProgressSink?, CancellationToken, ValueTask<TResult>> handler;

    private readonly List<ProgressCommandServiceInvocation<TInput>> invocations = [];

    protected RecordingProgressCommandService (
        Func<TInput, ICommandProgressSink?, CancellationToken, ValueTask<TResult>> handler)
    {
        this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public IReadOnlyList<ProgressCommandServiceInvocation<TInput>> Invocations => invocations;

    protected ValueTask<TResult> ExecuteRecordedAsync (
        TInput input,
        ICommandProgressSink? progressSink,
        CancellationToken cancellationToken)
    {
        invocations.Add(new ProgressCommandServiceInvocation<TInput>(input, progressSink, cancellationToken));
        return handler(input, progressSink, cancellationToken);
    }
}
