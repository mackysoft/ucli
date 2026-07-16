namespace MackySoft.Ucli.Tests.Daemon;

public sealed class UnityDaemonLaunchResultTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Size", "Small")]
    public void Success_WhenProcessIdIsNotPositive_RejectsInvalidResult (int processId)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => UnityDaemonLaunchResult.Success(processId, DateTimeOffset.UtcNow));

        Assert.Equal("processId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Success_WhenProcessStartTimeIsNotUtc_RejectsInvalidResult ()
    {
        var exception = Assert.Throws<ArgumentException>(() => UnityDaemonLaunchResult.Success(
            processId: 42,
            new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.FromHours(9))));

        Assert.Equal("processStartedAtUtc", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenErrorIsNull_RejectsInvalidResult ()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => UnityDaemonLaunchResult.Failure(null!));

        Assert.Equal("error", exception.ParamName);
    }
}
