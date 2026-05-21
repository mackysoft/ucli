using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Hosting.Cli.Common.Projection;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Common.Projection;

public sealed class DaemonStatusPayloadCodecTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData((int)DaemonStatusKind.Running, DaemonStatusPayloadCodec.Running)]
    [InlineData((int)DaemonStatusKind.NotRunning, DaemonStatusPayloadCodec.NotRunning)]
    [InlineData((int)DaemonStatusKind.Stale, DaemonStatusPayloadCodec.Stale)]
    public void ToValue_WhenSupportedStatus_ReturnsContractLiteral (
        int daemonStatus,
        string expected)
    {
        var actual = DaemonStatusPayloadCodec.ToValue((DaemonStatusKind)daemonStatus);

        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ToValue_WhenUnsupportedStatus_ThrowsArgumentOutOfRangeException ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DaemonStatusPayloadCodec.ToValue(DaemonStatusKind.Failed));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("running", (int)DaemonStatusKind.Running)]
    [InlineData(" notRunning ", (int)DaemonStatusKind.NotRunning)]
    [InlineData("stale", (int)DaemonStatusKind.Stale)]
    public void TryParse_WhenValidValue_ReturnsTrueAndParsedEnum (
        string? value,
        int expectedDaemonStatus)
    {
        var result = DaemonStatusPayloadCodec.TryParse(value, out var daemonStatus);

        Assert.True(result);
        Assert.Equal((DaemonStatusKind)expectedDaemonStatus, daemonStatus);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("RUNNING")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void TryParse_WhenInvalidValue_ReturnsFalseAndDefaultEnum (string? value)
    {
        var result = DaemonStatusPayloadCodec.TryParse(value, out var daemonStatus);

        Assert.False(result);
        Assert.Equal(default, daemonStatus);
    }
}
