using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Status;

namespace MackySoft.Ucli.Tests.Status;

public sealed class StatusDaemonObservationCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CreateWithoutPing_ReturnsObservationWithNullPingFields ()
    {
        var actual = StatusDaemonObservationCodec.CreateWithoutPing(DaemonStatusKind.NotRunning);

        Assert.Equal("notRunning", actual.DaemonStatus);
        Assert.Null(actual.ServerVersion);
        Assert.Null(actual.LifecycleState);
        Assert.Null(actual.BlockingReason);
        Assert.Null(actual.CompileState);
        Assert.Null(actual.CompileGeneration);
        Assert.Null(actual.DomainReloadGeneration);
        Assert.False(actual.CanAcceptExecutionRequests);
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
            CompileState: compileState,
            LifecycleState: " ready ",
            BlockingReason: " busy ",
            CompileGeneration: " 42 ",
            DomainReloadGeneration: " 17 ",
            CanAcceptExecutionRequests: true);

        var actual = StatusDaemonObservationCodec.CreateFromPing(
            DaemonStatusKind.Running,
            pingResponse);

        Assert.Equal("running", actual.DaemonStatus);
        Assert.Equal("0.5.0", actual.ServerVersion);
        Assert.Equal("ready", actual.LifecycleState);
        Assert.Equal("busy", actual.BlockingReason);
        Assert.Equal(expectedCompileState, actual.CompileState);
        Assert.Equal("42", actual.CompileGeneration);
        Assert.Equal("17", actual.DomainReloadGeneration);
        Assert.True(actual.CanAcceptExecutionRequests);
        Assert.Equal("batchmode", actual.Runtime);
    }
}