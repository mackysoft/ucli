namespace MackySoft.Tests;

using Xunit.Sdk;

public sealed class EventSequenceAssertTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void EmittedEventsInOrder_Succeeds_WhenEventNamesMatchInOrder ()
    {
        var collector = new TestEventCollector();
        collector.Add("started", new { RunId = "run-1" });
        collector.Add("completed", new { Verdict = "pass" });

        EventSequenceAssert.EmittedEventsInOrder(collector.Entries, "started", "completed");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EmittedEventsInOrder_Throws_WhenEventOrderDiffers ()
    {
        var collector = new TestEventCollector();
        collector.Add("completed", new { Verdict = "pass" });
        collector.Add("started", new { RunId = "run-1" });

        var exception = Assert.ThrowsAny<XunitException>(
            () => EventSequenceAssert.EmittedEventsInOrder(collector.Entries, "started", "completed"));

        Assert.Contains("started", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Add_InvokesEntryCallbackAndPreservesPayload ()
    {
        var callbackCount = 0;
        var collector = new TestEventCollector(() => callbackCount++);
        var payload = new { ProjectFingerprint = "project-fingerprint" };

        collector.Add("observed", payload);

        var entry = Assert.Single(collector.Entries);
        Assert.Equal("observed", entry.EventName);
        Assert.Same(payload, entry.Payload);
        Assert.Equal(1, callbackCount);
    }
}
