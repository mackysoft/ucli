namespace MackySoft.Tests;

internal sealed class TestEventCollector
{
    private readonly List<CollectedEventEntry> entries = [];
    private readonly Action? onEntry;

    public TestEventCollector (Action? onEntry = null)
    {
        this.onEntry = onEntry;
    }

    public IReadOnlyList<CollectedEventEntry> Entries => entries;

    public void Add<TPayload> (string eventName, TPayload payload)
        where TPayload : notnull
    {
        entries.Add(new CollectedEventEntry(eventName, payload));
        onEntry?.Invoke();
    }
}
