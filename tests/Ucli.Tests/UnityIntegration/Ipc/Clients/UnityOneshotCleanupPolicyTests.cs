using MackySoft.Ucli.UnityIntegration.Ipc.Clients;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityOneshotCleanupPolicyTests
{
    [Theory]
    [InlineData(0, 1, "timeout")]
    [InlineData(-1, 1, "timeout")]
    [InlineData(1, 0, "retryDelay")]
    [InlineData(1, -1, "retryDelay")]
    [Trait("Size", "Small")]
    public void Constructor_WhenDurationIsNotPositive_ThrowsArgumentOutOfRangeException (
        int timeoutMilliseconds,
        int retryDelayMilliseconds,
        string expectedParameterName)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new UnityOneshotCleanupPolicy(
            TimeSpan.FromMilliseconds(timeoutMilliseconds),
            TimeSpan.FromMilliseconds(retryDelayMilliseconds)));

        Assert.Equal(expectedParameterName, exception.ParamName);
    }
}
