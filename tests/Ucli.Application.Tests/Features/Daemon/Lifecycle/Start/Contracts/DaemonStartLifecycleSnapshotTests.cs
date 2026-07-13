using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStartLifecycleSnapshotTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_WhenLifecycleTupleIsConsistent_ReturnsTypedSnapshot ()
    {
        var pingResponse = CreatePingResponse(
            lifecycleState: "compiling",
            blockingReason: "compile",
            canAcceptExecutionRequests: false);

        var result = DaemonStartLifecycleSnapshot.TryCreate(
            pingResponse,
            out var snapshot,
            out var error);

        Assert.True(result);
        Assert.NotNull(snapshot);
        Assert.Equal(IpcEditorLifecycleState.Compiling, snapshot.LifecycleState);
        Assert.Equal(IpcEditorBlockingReason.Compile, snapshot.BlockingReason);
        Assert.False(snapshot.CanAcceptExecutionRequests);
        Assert.Null(error);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("ready", null, false)]
    [InlineData("ready", "busy", true)]
    [InlineData("compiling", null, false)]
    [InlineData("compiling", "busy", false)]
    [InlineData("compiling", "compile", true)]
    public void TryCreate_WhenLifecycleTupleIsInconsistent_ReturnsInternalError (
        string lifecycleState,
        string? blockingReason,
        bool canAcceptExecutionRequests)
    {
        var pingResponse = CreatePingResponse(
            lifecycleState,
            blockingReason,
            canAcceptExecutionRequests);

        var result = DaemonStartLifecycleSnapshot.TryCreate(
            pingResponse,
            out var snapshot,
            out var error);

        Assert.False(result);
        Assert.Null(snapshot);
        Assert.NotNull(error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("inconsistent lifecycle tuple", error.Message, StringComparison.Ordinal);
    }

    private static IpcPingResponse CreatePingResponse (
        string lifecycleState,
        string? blockingReason,
        bool canAcceptExecutionRequests)
    {
        return new IpcPingResponse(
            ServerVersion: "0.5.0",
            EditorMode: "batchmode",
            UnityVersion: "6000.1.4f1",
            ProjectFingerprint: "project-fingerprint",
            CompileState: "ready",
            LifecycleState: lifecycleState,
            BlockingReason: blockingReason,
            CompileGeneration: "12",
            DomainReloadGeneration: "7",
            CanAcceptExecutionRequests: canAcceptExecutionRequests);
    }
}
