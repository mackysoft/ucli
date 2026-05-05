using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Status.Common.Contracts;

namespace MackySoft.Ucli.Tests.Status;

public sealed class StatusDaemonStateCodecTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData((int)DaemonStatusKind.Running, StatusDaemonStateCodec.Running)]
    [InlineData((int)DaemonStatusKind.NotRunning, StatusDaemonStateCodec.NotRunning)]
    [InlineData((int)DaemonStatusKind.Stale, StatusDaemonStateCodec.Stale)]
    public void ToValue_WhenSupportedStatus_ReturnsContractLiteral (
        int daemonStatus,
        string expected)
    {
        var actual = StatusDaemonStateCodec.ToValue((DaemonStatusKind)daemonStatus);

        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ToValue_WhenUnsupportedStatus_ThrowsArgumentOutOfRangeException ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => StatusDaemonStateCodec.ToValue(DaemonStatusKind.Failed));
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
        var result = StatusDaemonStateCodec.TryParse(value, out var daemonStatus);

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
        var result = StatusDaemonStateCodec.TryParse(value, out var daemonStatus);

        Assert.False(result);
        Assert.Equal(default, daemonStatus);
    }
}
