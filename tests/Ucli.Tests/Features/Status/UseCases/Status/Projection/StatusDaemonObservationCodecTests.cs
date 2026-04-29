using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Status.UseCases.Status.Projection;

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
            Runtime: $" {IpcEditorRuntimeCodec.Batchmode} ",
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
        Assert.Null(actual.BlockingReason);
        Assert.Equal(expectedCompileState, actual.CompileState);
        Assert.Equal("42", actual.CompileGeneration);
        Assert.Equal("17", actual.DomainReloadGeneration);
        Assert.True(actual.CanAcceptExecutionRequests);
        Assert.Equal(IpcEditorRuntimeCodec.Batchmode, actual.Runtime);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateFromPing_WhenLifecycleStateIsUnsupported_ClearsBlockingAndReadinessFields ()
    {
        var pingResponse = new IpcPingResponse(
            ServerVersion: "0.5.0",
            Runtime: IpcEditorRuntimeCodec.Batchmode,
            UnityVersion: "2022.3.5f1",
            CompileState: "ready",
            LifecycleState: "unsupported",
            BlockingReason: "busy",
            CompileGeneration: "42",
            DomainReloadGeneration: "17",
            CanAcceptExecutionRequests: true);

        var actual = StatusDaemonObservationCodec.CreateFromPing(
            DaemonStatusKind.Running,
            pingResponse);

        Assert.Null(actual.LifecycleState);
        Assert.Null(actual.BlockingReason);
        Assert.False(actual.CanAcceptExecutionRequests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateFromPing_WhenRuntimeIsUnsupported_ClearsRuntime ()
    {
        var pingResponse = new IpcPingResponse(
            ServerVersion: "0.5.0",
            Runtime: "unsupported",
            UnityVersion: "2022.3.5f1",
            CompileState: "ready",
            LifecycleState: "ready",
            BlockingReason: null,
            CompileGeneration: "42",
            DomainReloadGeneration: "17",
            CanAcceptExecutionRequests: true);

        var actual = StatusDaemonObservationCodec.CreateFromPing(
            DaemonStatusKind.Running,
            pingResponse);

        Assert.Null(actual.Runtime);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateFromPing_WhenLifecycleStateIsReady_ClearsBlockingReason ()
    {
        var pingResponse = new IpcPingResponse(
            ServerVersion: "0.5.0",
            Runtime: IpcEditorRuntimeCodec.Batchmode,
            UnityVersion: "2022.3.5f1",
            CompileState: "ready",
            LifecycleState: "ready",
            BlockingReason: "busy",
            CompileGeneration: "42",
            DomainReloadGeneration: "17",
            CanAcceptExecutionRequests: true);

        var actual = StatusDaemonObservationCodec.CreateFromPing(
            DaemonStatusKind.Running,
            pingResponse);

        Assert.Equal("ready", actual.LifecycleState);
        Assert.Null(actual.BlockingReason);
        Assert.True(actual.CanAcceptExecutionRequests);
    }
}
