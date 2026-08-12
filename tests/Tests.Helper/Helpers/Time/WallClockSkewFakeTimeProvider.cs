using Microsoft.Extensions.Time.Testing;

namespace MackySoft.Tests;

/// <summary>
/// Uses the official fake time provider for monotonic time and timers while allowing tests to
/// move wall-clock UTC independently.
/// </summary>
internal sealed class WallClockSkewFakeTimeProvider : TimeProvider
{
    private readonly FakeTimeProvider fakeTimeProvider;

    private long utcOffsetTicks;

    internal WallClockSkewFakeTimeProvider (DateTimeOffset? startUtc = null)
    {
        fakeTimeProvider = new FakeTimeProvider(startUtc ?? DateTimeOffset.UnixEpoch);
    }

    public override TimeZoneInfo LocalTimeZone => fakeTimeProvider.LocalTimeZone;

    public override long TimestampFrequency => fakeTimeProvider.TimestampFrequency;

    public override DateTimeOffset GetUtcNow ()
    {
        return fakeTimeProvider.GetUtcNow() + TimeSpan.FromTicks(Volatile.Read(ref utcOffsetTicks));
    }

    public override long GetTimestamp ()
    {
        return fakeTimeProvider.GetTimestamp();
    }

    public override ITimer CreateTimer (
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        return fakeTimeProvider.CreateTimer(callback, state, dueTime, period);
    }

    internal void Advance (TimeSpan elapsed)
    {
        fakeTimeProvider.Advance(elapsed);
    }

    internal void ShiftUtc (TimeSpan offset)
    {
        _ = Interlocked.Add(ref utcOffsetTicks, offset.Ticks);
    }
}
