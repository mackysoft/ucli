using MackySoft.Ucli.Infrastructure.Execution;

namespace MackySoft.Ucli.Infrastructure.Tests.Execution;

public sealed class ProcessLivenessProbeTests
{
    private static readonly DateTimeOffset ExpectedStartTimeUtc =
        new(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(-10)]
    [InlineData(0)]
    [InlineData(10)]
    [Trait("Size", "Small")]
    public void AreEquivalentProcessStartTimeMeasurements_WhenDifferenceIsAtMostOneMicrosecond_ReturnsTrue (long differenceTicks)
    {
        Assert.True(ProcessLivenessProbe.AreEquivalentProcessStartTimeMeasurements(
            ExpectedStartTimeUtc,
            ExpectedStartTimeUtc.AddTicks(differenceTicks)));
    }

    [Theory]
    [InlineData(-11)]
    [InlineData(11)]
    [Trait("Size", "Small")]
    public void AreEquivalentProcessStartTimeMeasurements_WhenDifferenceExceedsOneMicrosecond_ReturnsFalse (long differenceTicks)
    {
        Assert.False(ProcessLivenessProbe.AreEquivalentProcessStartTimeMeasurements(
            ExpectedStartTimeUtc,
            ExpectedStartTimeUtc.AddTicks(differenceTicks)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AreEquivalentProcessStartTimeMeasurements_WhenOffsetsDifferButUtcTicksMatch_ReturnsTrue ()
    {
        var sameInstantWithDifferentOffset = ExpectedStartTimeUtc.ToOffset(TimeSpan.FromHours(9));

        Assert.True(ProcessLivenessProbe.AreEquivalentProcessStartTimeMeasurements(
            ExpectedStartTimeUtc,
            sameInstantWithDifferentOffset));
    }
}
