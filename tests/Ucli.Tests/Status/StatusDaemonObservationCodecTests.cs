using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Status;

namespace MackySoft.Ucli.Tests.Status;

public sealed class StatusDaemonObservationCodecTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData((int)DaemonStatusKind.Running, "running")]
    [InlineData((int)DaemonStatusKind.NotRunning, "notRunning")]
    [InlineData((int)DaemonStatusKind.Stale, "stale")]
    public void ToDaemonStatusValue_WhenSupportedStatus_ReturnsContractLiteral (
        int daemonStatus,
        string expected)
    {
        var actual = StatusDaemonObservationCodec.ToDaemonStatusValue((DaemonStatusKind)daemonStatus);

        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ToDaemonStatusValue_WhenUnsupportedStatus_ThrowsArgumentOutOfRangeException ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => StatusDaemonObservationCodec.ToDaemonStatusValue(DaemonStatusKind.Failed));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateWithoutPing_ReturnsObservationWithNullPingFields ()
    {
        var actual = StatusDaemonObservationCodec.CreateWithoutPing(DaemonStatusKind.NotRunning);

        Assert.Equal("notRunning", actual.DaemonStatus);
        Assert.Null(actual.ServerVersion);
        Assert.Null(actual.CompileState);
        Assert.Null(actual.Runtime);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("ready", "ready")]
    [InlineData("compiling", "compiling")]
    [InlineData(" Ready ", null)]
    [InlineData("unknown", null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    public void CreateFromPing_NormalizesCompileStateToSupportedLiterals (
        string compileState,
        string? expectedCompileState)
    {
        var pingResponse = new IpcPingResponse(
            ServerVersion: " 0.5.0 ",
            Runtime: " batchmode ",
            UnityVersion: "2022.3.5f1",
            CompileState: compileState);

        var actual = StatusDaemonObservationCodec.CreateFromPing(
            DaemonStatusKind.Running,
            pingResponse);

        Assert.Equal("running", actual.DaemonStatus);
        Assert.Equal("0.5.0", actual.ServerVersion);
        Assert.Equal(expectedCompileState, actual.CompileState);
        Assert.Equal("batchmode", actual.Runtime);
    }
}