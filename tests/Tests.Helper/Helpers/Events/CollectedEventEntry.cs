namespace MackySoft.Tests;

internal readonly record struct CollectedEventEntry (
    string EventName,
    object Payload);
