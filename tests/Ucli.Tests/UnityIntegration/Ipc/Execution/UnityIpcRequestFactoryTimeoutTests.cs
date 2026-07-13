namespace MackySoft.Ucli.Tests.Ipc;

using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;
using static MackySoft.Ucli.Tests.Ipc.UnityIpcRequestBuilderTestSupport;

public sealed class UnityIpcRequestFactoryTimeoutTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(999)]
    public void UnityIpcRequestFactory_WhenMethodIsUndefined_ThrowsArgumentOutOfRangeException (int value)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => UnityIpcRequestFactory.Create(
            IpcSessionTokenTestFactory.CreateFromDiscriminator(1),
            (UnityIpcMethod)value,
            IpcPayloadCodec.SerializeToElement(new { }),
            Guid.NewGuid(),
            IpcResponseMode.Single));

        Assert.Equal("method", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityIpcRequestFactory_WithCompileDispatchTimeout_InjectsTimeoutPayload ()
    {
        var dispatchRequest = new UnityIpcRequestBuilder().Build(new UnityRequestPayload.Compile(RunIdTestValues.Compile));

        var request = UnityIpcRequestFactory.Create(
            IpcSessionTokenTestFactory.CreateFromDiscriminator(1),
            dispatchRequest.Method,
            dispatchRequest.CreatePayload(TimeSpan.FromMilliseconds(1234)),
            Guid.NewGuid(),
            dispatchRequest.ResponseMode);

        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcCompileRequest compileRequest, out _));
        Assert.Equal(RunIdTestValues.Compile, compileRequest.RunId);
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
            RunId: RunIdTestValues.Test));

        var request = UnityIpcRequestFactory.Create(
            IpcSessionTokenTestFactory.CreateFromDiscriminator(1),
            dispatchRequest.Method,
            dispatchRequest.CreatePayload(TimeSpan.FromMilliseconds(1234)),
            Guid.NewGuid(),
            dispatchRequest.ResponseMode);

        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcTestRunRequest testRunRequest, out _));
        Assert.Equal(RunIdTestValues.Test, testRunRequest.RunId);
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
            IpcSessionTokenTestFactory.CreateFromDiscriminator(1),
            dispatchRequest.Method,
            dispatchRequest.CreatePayload(TimeSpan.FromMilliseconds(1234)),
            Guid.NewGuid(),
            dispatchRequest.ResponseMode);

        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcBuildRunRequest buildRunRequest, out _));
        Assert.Equal(RunIdTestValues.Build, buildRunRequest.RunId);
        Assert.Equal(1234, buildRunRequest.TimeoutMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityIpcRequestFactory_WithExecuteDispatchTimeout_InjectsTimeoutPayload ()
    {
        var executeArguments = IpcPayloadCodec.SerializeToElement(new
        {
            protocolVersion = IpcProtocol.CurrentVersion,
            steps = Array.Empty<object>(),
        });
        var dispatchRequest = new UnityIpcRequestBuilder().Build(new UnityRequestPayload.ExecuteJson(
            UcliCommandIds.Call,
            executeArguments,
            FailFast: false,
            AllowDangerous: true));

        var request = UnityIpcRequestFactory.Create(
            IpcSessionTokenTestFactory.CreateFromDiscriminator(1),
            dispatchRequest.Method,
            dispatchRequest.CreatePayload(TimeSpan.FromMilliseconds(1234)),
            Guid.NewGuid(),
            dispatchRequest.ResponseMode);

        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcExecuteRequest executeRequest, out _));
        Assert.Equal(UcliCommandIds.Call.Name, executeRequest.Command);
        Assert.True(executeRequest.AllowDangerous);
        Assert.Equal(1234, executeRequest.TimeoutMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityIpcRequestFactory_WithExecuteOperationDispatchTimeout_InjectsTimeoutPayload ()
    {
        var args = IpcPayloadCodec.SerializeToElement(new
        {
            path = "Assets/Test.prefab",
        });
        var dispatchRequest = new UnityIpcRequestBuilder().Build(new UnityRequestPayload.ExecuteOperation(
            UcliCommandIds.Call,
            "op-1",
            UcliPrimitiveOperationNames.Resolve,
            args,
            FailFast: false,
            AllowDangerous: true));

        var request = UnityIpcRequestFactory.Create(
            IpcSessionTokenTestFactory.CreateFromDiscriminator(1),
            dispatchRequest.Method,
            dispatchRequest.CreatePayload(TimeSpan.FromMilliseconds(1234)),
            Guid.NewGuid(),
            dispatchRequest.ResponseMode);

        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcExecuteRequest executeRequest, out _));
        Assert.Equal(UcliCommandIds.Call.Name, executeRequest.Command);
        Assert.True(executeRequest.AllowDangerous);
        Assert.Equal(1234, executeRequest.TimeoutMilliseconds);
    }
}
