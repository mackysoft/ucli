namespace MackySoft.Tests;

internal static class EventSequenceAssert
{
    public static void EmittedEventsInOrder (
        IReadOnlyList<CollectedEventEntry> entries,
        params string[] eventNames)
    {
        Assert.Collection(
            entries,
            eventNames
                .Select<string, Action<CollectedEventEntry>>(eventName => entry => Assert.Equal(eventName, entry.EventName))
                .ToArray());
    }
}
