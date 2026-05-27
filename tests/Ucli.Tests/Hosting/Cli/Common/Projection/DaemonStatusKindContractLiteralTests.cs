using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Common.Projection;

public sealed class DaemonStatusKindContractLiteralTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData((int)DaemonStatusKind.Running, "running")]
    [InlineData((int)DaemonStatusKind.NotRunning, "notRunning")]
    [InlineData((int)DaemonStatusKind.Stale, "stale")]
    public void ToValue_WhenSupportedStatus_ReturnsContractLiteral (
        int daemonStatus,
        string expected)
    {
        var actual = ContractLiteralCodec.ToValue((DaemonStatusKind)daemonStatus);

        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ToValue_WhenUnsupportedStatus_ThrowsArgumentOutOfRangeException ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ContractLiteralCodec.ToValue(DaemonStatusKind.Failed));
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
        var result = ContractLiteralInputParser.TryParseTrimmed<DaemonStatusKind>(value, out var daemonStatus);

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
        Assert.False(ContractLiteralInputParser.IsDefinedTrimmed<DaemonStatusKind>(value));
    }
}
