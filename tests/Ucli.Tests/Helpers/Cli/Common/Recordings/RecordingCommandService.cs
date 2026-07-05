namespace MackySoft.Tests;

internal abstract class RecordingCommandService<TInput, TResult>
{
    private readonly Func<TInput, CancellationToken, ValueTask<TResult>> handler;

    private readonly List<CommandServiceInvocation<TInput>> invocations = [];

    protected RecordingCommandService (Func<TInput, CancellationToken, ValueTask<TResult>> handler)
    {
        this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public IReadOnlyList<CommandServiceInvocation<TInput>> Invocations => invocations;

    protected ValueTask<TResult> ExecuteRecordedAsync (
        TInput input,
        CancellationToken cancellationToken)
    {
        invocations.Add(new CommandServiceInvocation<TInput>(input, cancellationToken));
        return handler(input, cancellationToken);
    }
}
