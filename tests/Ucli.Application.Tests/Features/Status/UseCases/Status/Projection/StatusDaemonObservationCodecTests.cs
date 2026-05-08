using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status.Projection;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Status;

public sealed class StatusDaemonObservationCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CreateWithoutPing_ReturnsObservationWithNullPingFields ()
    {
        var actual = StatusDaemonObservationCodec.CreateWithoutPing(DaemonStatusKind.NotRunning);

        Assert.Equal(DaemonStatusKind.NotRunning, actual.DaemonStatus);
        Assert.Null(actual.ServerVersion);
        Assert.Null(actual.LifecycleState);
        Assert.Null(actual.BlockingReason);
        Assert.Null(actual.CompileState);
        Assert.Null(actual.CompileGeneration);
        Assert.Null(actual.DomainReloadGeneration);
        Assert.False(actual.CanAcceptExecutionRequests);
        Assert.Null(actual.EditorMode);
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
            EditorMode: $" {DaemonEditorModeValues.Batchmode} ",
            UnityVersion: "2022.3.5f1",
            ProjectFingerprint: "project-fingerprint",
            CompileState: compileState,
            LifecycleState: " ready ",
            BlockingReason: " busy ",
            CompileGeneration: " 42 ",
            DomainReloadGeneration: " 17 ",
            CanAcceptExecutionRequests: true);

        var actual = StatusDaemonObservationCodec.CreateFromPing(
            DaemonStatusKind.Running,
            pingResponse);

        Assert.Equal(DaemonStatusKind.Running, actual.DaemonStatus);
        Assert.Equal("0.5.0", actual.ServerVersion);
        Assert.Equal("ready", actual.LifecycleState);
        Assert.Null(actual.BlockingReason);
        Assert.Equal(expectedCompileState, actual.CompileState);
        Assert.Equal("42", actual.CompileGeneration);
        Assert.Equal("17", actual.DomainReloadGeneration);
        Assert.True(actual.CanAcceptExecutionRequests);
        Assert.Equal(DaemonEditorModeValues.Batchmode, actual.EditorMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateFromPing_WhenLifecycleStateIsUnsupported_ClearsBlockingAndReadinessFields ()
    {
        var pingResponse = new IpcPingResponse(
            ServerVersion: "0.5.0",
            EditorMode: DaemonEditorModeValues.Batchmode,
            UnityVersion: "2022.3.5f1",
            ProjectFingerprint: "project-fingerprint",
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
    public void CreateFromPing_WhenEditorModeIsUnsupported_ClearsEditorMode ()
    {
        var pingResponse = new IpcPingResponse(
            ServerVersion: "0.5.0",
            EditorMode: "unsupported",
            UnityVersion: "2022.3.5f1",
            ProjectFingerprint: "project-fingerprint",
            CompileState: "ready",
            LifecycleState: "ready",
            BlockingReason: null,
            CompileGeneration: "42",
            DomainReloadGeneration: "17",
            CanAcceptExecutionRequests: true);

        var actual = StatusDaemonObservationCodec.CreateFromPing(
            DaemonStatusKind.Running,
            pingResponse);

        Assert.Null(actual.EditorMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateFromPing_WhenLifecycleStateIsReady_ClearsBlockingReason ()
    {
        var pingResponse = new IpcPingResponse(
            ServerVersion: "0.5.0",
            EditorMode: DaemonEditorModeValues.Batchmode,
            UnityVersion: "2022.3.5f1",
            ProjectFingerprint: "project-fingerprint",
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
