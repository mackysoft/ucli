using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Tests.Ipc;

using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;

public sealed class UnityIpcRequestBuilderBasicPayloadTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(999)]
    public void UnityIpcDispatchRequest_WhenMethodIsUndefined_ThrowsArgumentOutOfRangeException (int value)
    {
        var method = (UnityIpcMethod)value;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UnityIpcDispatchRequest(
                method,
                IpcPayloadCodec.SerializeToElement(new { }),
                UnityBatchmodeLaunchOptions.Default));

        Assert.Equal("method", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityIpcDispatchRequest_WhenNonBuildMethodHasActiveBuildProfile_ThrowsArgumentException ()
    {
        var launchOptions = new UnityBatchmodeLaunchOptions(
            new UnityBuildProfileAssetPath("Assets/BuildProfiles/LinuxPlayer.asset"));

        var exception = Assert.Throws<ArgumentException>(() => new UnityIpcDispatchRequest(
            UnityIpcMethod.OpsRead,
            IpcPayloadCodec.SerializeToElement(new { }),
            launchOptions));

        Assert.Equal("launchOptions", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithOpsRead_CreatesOpsReadPayload ()
    {
        var builder = new UnityIpcRequestBuilder();

        var request = builder.Build(new UnityRequestPayload.OpsRead(
            FailFast: true,
            RequireReadinessGate: true,
            IncludeEditLoweringOnly: true));

        Assert.Equal(UnityIpcMethod.OpsRead, request.Method);
        Assert.Equal(
            UnityIpcResponseReplayPolicy.StatelessAnyHostSuccessor,
            request.ResponseReplayPolicy);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcOpsReadRequest payload, out _));
        Assert.True(payload.FailFast);
        Assert.True(payload.RequireReadinessGate);
        Assert.True(payload.IncludeEditLoweringOnly);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithPing_CreatesPingPayload ()
    {
        var builder = new UnityIpcRequestBuilder();

        var request = builder.Build(new UnityRequestPayload.Ping(
            "test-client",
            FailFast: true));

        Assert.Equal(UnityIpcMethod.Ping, request.Method);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcPingRequest payload, out _));
        Assert.Equal("test-client", payload.ClientVersion);
        Assert.True(payload.FailFast);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithCompile_CreatesCompileLifecycleRegistration ()
    {
        var builder = new UnityIpcRequestBuilder();
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Compile);
        var registration = new LifecycleExecutionRegistration(
            definition,
            RunIdTestValues.Compile,
            DateTimeOffset.UnixEpoch.AddSeconds(30),
            DateTimeOffset.UnixEpoch);

        var request = builder.Build(
            new UnityRequestPayload.Compile(
                registration,
                requiredStart: null));

        Assert.Equal(UnityIpcMethod.Compile, request.Method);
        Assert.Same(registration, request.Registration);
        Assert.Equal(
            UnityIpcResponseReplayPolicy.LifecycleExecutionSameHostSuccessor,
            request.ResponseReplayPolicy);
        var startRequest = request.CreateLifecycleStartRequest();
        Assert.Equal(LifecycleExecutionKind.Compile, startRequest.Kind);
        Assert.Equal(RunIdTestValues.Compile, startRequest.ExecutionId);
        Assert.Equal(
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            startRequest.DefinitionDigest);
        Assert.Throws<InvalidOperationException>(() => request.Payload);
        Assert.True(UnityIpcMethodCapabilities.AllowsStartupLifecycleState(
            request.Method,
            UnityEditorLifecycleState.CompileFailed));
        Assert.True(UnityIpcMethodCapabilities.AllowsStartupLifecycleState(
            request.Method,
            UnityEditorLifecycleState.SafeMode));
        Assert.False(UnityIpcMethodCapabilities.AllowsStartupLifecycleState(
            request.Method,
            UnityEditorLifecycleState.Ready));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithPlayStatus_CreatesPlayStatusPayload ()
    {
        var builder = new UnityIpcRequestBuilder();

        var request = builder.Build(new UnityRequestPayload.PlayStatus());

        Assert.Equal(UnityIpcMethod.PlayStatus, request.Method);
        Assert.Equal(
            UnityIpcResponseReplayPolicy.StatelessAnyHostSuccessor,
            request.ResponseReplayPolicy);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcPlayStatusRequest _, out _));
        Assert.False(UnityIpcMethodCapabilities.AllowsStartupLifecycleState(
            request.Method,
            UnityEditorLifecycleState.SafeMode));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithScreenshotCapture_CreatesScreenshotCapturePayload ()
    {
        var captureId = Guid.Parse("ab66cdfa-d4bd-49bd-b727-a1201d4426f4");
        var builder = new UnityIpcRequestBuilder();

        var request = builder.Build(new UnityRequestPayload.ScreenshotCapture(
            new IpcScreenshotCaptureRequest(
                CaptureId: captureId,
                Target: IpcScreenshotTarget.Game,
                RequestedWidth: 1920,
                RequestedHeight: 1080)));

        Assert.Equal(UnityIpcMethod.ScreenshotCapture, request.Method);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcScreenshotCaptureRequest payload, out _));
        Assert.Equal(captureId, payload.CaptureId);
        Assert.Equal(IpcScreenshotTarget.Game, payload.Target);
        Assert.Equal(1920, payload.RequestedWidth);
        Assert.Equal(1080, payload.RequestedHeight);
        Assert.False(request.Payload.TryGetProperty("stagingPath", out _));
        Assert.False(request.Payload.TryGetProperty("timeoutMilliseconds", out _));
        Assert.Equal(UnityIpcResponseReplayPolicy.None, request.ResponseReplayPolicy);
        Assert.False(UnityIpcMethodCapabilities.AllowsStartupLifecycleState(
            request.Method,
            UnityEditorLifecycleState.SafeMode));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithPlayEnter_CreatesPlayEnterPayload ()
    {
        var builder = new UnityIpcRequestBuilder();
        var registration = UnityIpcRequestBuilderTestSupport.CreateLifecycleRegistration(
            LifecycleExecutionKind.PlayEnter);

        var request = builder.Build(
            new UnityRequestPayload.PlayEnter(
                registration,
                requiredStart: null));

        Assert.Equal(UnityIpcMethod.PlayEnter, request.Method);
        Assert.Same(registration, request.Registration);
        Assert.Equal(
            UnityIpcResponseReplayPolicy.LifecycleExecutionSameHostSuccessor,
            request.ResponseReplayPolicy);
        var start = LifecycleExecutionIpcTestResponseFactory.CreateStartBinding(
            request.CreateLifecycleStartRequest());
        var actionPayload = request.CreateLifecycleActionPayload(start);
        Assert.True(IpcPayloadCodec.TryDeserialize(actionPayload, out IpcPlayEnterRequest _, out _));
        Assert.False(actionPayload.TryGetProperty("timeoutMilliseconds", out _));
        Assert.False(UnityIpcMethodCapabilities.AllowsStartupLifecycleState(
            request.Method,
            UnityEditorLifecycleState.SafeMode));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithPlayExit_CreatesPlayExitPayload ()
    {
        var builder = new UnityIpcRequestBuilder();
        var registration = UnityIpcRequestBuilderTestSupport.CreateLifecycleRegistration(
            LifecycleExecutionKind.PlayExit);

        var request = builder.Build(
            new UnityRequestPayload.PlayExit(
                registration,
                requiredStart: null));

        Assert.Equal(UnityIpcMethod.PlayExit, request.Method);
        Assert.Same(registration, request.Registration);
        Assert.Equal(
            UnityIpcResponseReplayPolicy.LifecycleExecutionSameHostSuccessor,
            request.ResponseReplayPolicy);
        var start = LifecycleExecutionIpcTestResponseFactory.CreateStartBinding(
            request.CreateLifecycleStartRequest());
        var actionPayload = request.CreateLifecycleActionPayload(start);
        Assert.True(IpcPayloadCodec.TryDeserialize(actionPayload, out IpcPlayExitRequest _, out _));
        Assert.False(actionPayload.TryGetProperty("timeoutMilliseconds", out _));
        Assert.False(UnityIpcMethodCapabilities.AllowsStartupLifecycleState(
            request.Method,
            UnityEditorLifecycleState.SafeMode));
    }
}
