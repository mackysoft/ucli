namespace MackySoft.Ucli.Tests.Ipc;

using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;

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
            new UnityIpcDispatchRequest(method, IpcPayloadCodec.SerializeToElement(new { })));

        Assert.Equal("method", exception.ParamName);
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
    public void Build_WithCompile_CreatesCompilePayload ()
    {
        var builder = new UnityIpcRequestBuilder();

        var request = builder.Build(new UnityRequestPayload.Compile("run-1"));

        Assert.Equal(UnityIpcMethod.Compile, request.Method);
        Assert.True(request.IsRecoverable);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcCompileRequest payload, out _));
        Assert.Equal("run-1", payload.RunId);
        Assert.Null(payload.TimeoutMilliseconds);
        Assert.Equal(
            [IpcEditorLifecycleStateCodec.CompileFailed, IpcEditorLifecycleStateCodec.SafeMode],
            request.AllowedStartupLifecycleStates);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithPlayStatus_CreatesPlayStatusPayload ()
    {
        var builder = new UnityIpcRequestBuilder();

        var request = builder.Build(new UnityRequestPayload.PlayStatus());

        Assert.Equal(UnityIpcMethod.PlayStatus, request.Method);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcPlayStatusRequest _, out _));
        Assert.Empty(request.AllowedStartupLifecycleStates);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithPlayEnter_CreatesPlayEnterPayload ()
    {
        var builder = new UnityIpcRequestBuilder();

        var request = builder.Build(new UnityRequestPayload.PlayEnter(1500));

        Assert.Equal(UnityIpcMethod.PlayEnter, request.Method);
        Assert.True(request.IsRecoverable);
        Assert.Equal(TimeSpan.FromMilliseconds(1000), request.RecoverableResponseAttemptTimeout);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcPlayEnterRequest payload, out _));
        Assert.Equal(1500, payload.TimeoutMilliseconds);
        Assert.Empty(request.AllowedStartupLifecycleStates);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithPlayExit_CreatesPlayExitPayload ()
    {
        var builder = new UnityIpcRequestBuilder();

        var request = builder.Build(new UnityRequestPayload.PlayExit(2500));

        Assert.Equal(UnityIpcMethod.PlayExit, request.Method);
        Assert.True(request.IsRecoverable);
        Assert.Equal(TimeSpan.FromMilliseconds(1000), request.RecoverableResponseAttemptTimeout);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcPlayExitRequest payload, out _));
        Assert.Equal(2500, payload.TimeoutMilliseconds);
        Assert.Empty(request.AllowedStartupLifecycleStates);
    }
}
