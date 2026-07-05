namespace MackySoft.Ucli.Tests.Ipc;

using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;
using static MackySoft.Ucli.Tests.Ipc.UnityIpcRequestBuilderTestSupport;

public sealed class UnityIpcRequestFactoryTimeoutTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void UnityIpcRequestFactory_WithCompileDispatchTimeout_InjectsTimeoutPayload ()
    {
        var dispatchRequest = new UnityIpcRequestBuilder().Build(new UnityRequestPayload.Compile("run-1"));

        var request = UnityIpcRequestFactory.Create(
            "session-token",
            dispatchRequest,
            TimeSpan.FromMilliseconds(1234));

        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcCompileRequest compileRequest, out _));
        Assert.Equal("run-1", compileRequest.RunId);
        Assert.Equal(1234, compileRequest.TimeoutMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityIpcRequestFactory_WithTestRunDispatchTimeout_InjectsTimeoutPayload ()
    {
        var dispatchRequest = new UnityIpcRequestBuilder().Build(new UnityRequestPayload.TestRun(
            TestRunPlatformCodec.EditMode,
            TestFilter: null,
            TestCategories: [],
            AssemblyNames: [],
            TestSettingsPath: null,
            ResultsXmlPath: "/tmp/results.xml",
            EditorLogPath: "/tmp/editor.log",
            FailFast: false,
            RunId: "run-1"));

        var request = UnityIpcRequestFactory.Create(
            "session-token",
            dispatchRequest,
            TimeSpan.FromMilliseconds(1234));

        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcTestRunRequest testRunRequest, out _));
        Assert.Equal("run-1", testRunRequest.RunId);
        Assert.Equal(1234, testRunRequest.TimeoutMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityIpcRequestFactory_WithBuildRunDispatchTimeout_InjectsTimeoutPayload ()
    {
        var dispatchRequest = new UnityIpcRequestBuilder().Build(CreateExplicitBuildRunPayload(
            outputLayout: new IpcBuildOutputLayout(
                Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
                LocationPathName: "/tmp/ucli/output/player/Player"),
            development: false));

        var request = UnityIpcRequestFactory.Create(
            "session-token",
            dispatchRequest,
            TimeSpan.FromMilliseconds(1234));

        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcBuildRunRequest buildRunRequest, out _));
        Assert.Equal("build-run-1", buildRunRequest.RunId);
        Assert.Equal(1234, buildRunRequest.TimeoutMilliseconds);
    }
}
