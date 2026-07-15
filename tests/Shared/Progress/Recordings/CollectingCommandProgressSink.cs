using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Ucli.TestSupport;

internal sealed class CollectingCommandProgressSink : ICommandProgressSink
{
    private readonly TestEventCollector collector;

    public CollectingCommandProgressSink (Action? onEntry = null)
    {
        collector = new TestEventCollector(onEntry);
    }

    public IReadOnlyList<CollectedEventEntry> Entries => collector.Entries;

    public ValueTask OnEntryAsync<TPayload> (
        string eventName,
        TPayload payload,
        CancellationToken cancellationToken = default)
        where TPayload : notnull
    {
        cancellationToken.ThrowIfCancellationRequested();
        collector.Add(eventName, payload);
        return ValueTask.CompletedTask;
    }
}
