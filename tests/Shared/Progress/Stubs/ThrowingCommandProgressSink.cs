using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Ucli.TestSupport;

internal sealed class ThrowingCommandProgressSink : ICommandProgressSink
{
    private readonly Exception exception;

    public ThrowingCommandProgressSink (Exception exception)
    {
        this.exception = exception;
    }

    public ValueTask OnEntryAsync<TPayload> (
        string eventName,
        TPayload payload,
        CancellationToken cancellationToken = default)
        where TPayload : notnull
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw exception;
    }
}
