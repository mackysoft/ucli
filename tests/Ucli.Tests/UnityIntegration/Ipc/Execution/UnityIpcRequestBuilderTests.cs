using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityIpcRequestBuilderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithRawPayload_PreservesMethodAndPayload ()
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            value = 42,
        });
        var builder = new UnityIpcRequestBuilder();

        var request = builder.Build(new UnityRequestPayload.Raw("custom.method", payload));

        Assert.Equal("custom.method", request.Method);
        Assert.Equal(payload.GetRawText(), request.Payload.GetRawText());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithPing_CreatesPingPayload ()
    {
        var builder = new UnityIpcRequestBuilder();

        var request = builder.Build(new UnityRequestPayload.Ping(
            "test-client",
            FailFast: true));

        Assert.Equal(IpcMethodNames.Ping, request.Method);
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

        Assert.Equal(IpcMethodNames.Compile, request.Method);
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
    public void Build_WithBuildRun_CreatesBuildRunPayload ()
    {
        var builder = new UnityIpcRequestBuilder();

        var request = builder.Build(new UnityRequestPayload.BuildRun(
            RunId: "build-run-1",
            BuildTarget: "standaloneLinux64",
            UnityBuildTarget: "StandaloneLinux64",
            SceneSource: "explicit",
            ScenePaths: ["Assets/Scenes/Main.unity"],
            Development: true,
            OutputPath: "/tmp/ucli/output",
            OutputLayout: new IpcBuildOutputLayout(
                Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
                LocationPathName: "/tmp/ucli/output/player/Player"),
            BuildReportPath: "/tmp/ucli/build-report.json",
            BuildLogPath: "/tmp/ucli/build.log",
            AllowedEditorModes: ["batchmode"],
            ProjectMutationMode: "forbid",
            RunnerKind: "buildPipeline"));

        Assert.Equal(IpcMethodNames.BuildRun, request.Method);
        Assert.False(request.IsRecoverable);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcBuildRunRequest payload, out _));
        Assert.Equal("build-run-1", payload.RunId);
        Assert.Equal("standaloneLinux64", payload.BuildTarget);
        Assert.Equal("StandaloneLinux64", payload.UnityBuildTarget);
        Assert.Equal("explicit", payload.SceneSource);
        Assert.Equal(["Assets/Scenes/Main.unity"], payload.ScenePaths);
        Assert.True(payload.Development);
        Assert.Equal("/tmp/ucli/output", payload.OutputPath);
        Assert.NotNull(payload.OutputLayout);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File), payload.OutputLayout!.Shape);
        Assert.Equal("/tmp/ucli/output/player/Player", payload.OutputLayout.LocationPathName);
        Assert.Equal("/tmp/ucli/build-report.json", payload.BuildReportPath);
        Assert.Equal("/tmp/ucli/build.log", payload.BuildLogPath);
        Assert.Equal(["batchmode"], payload.AllowedEditorModes);
        Assert.Equal("forbid", payload.ProjectMutationMode);
        Assert.Null(payload.TimeoutMilliseconds);
        Assert.Equal("buildPipeline", payload.RunnerKind);
        Assert.Null(payload.RunnerMethod);
        Assert.Empty(payload.RunnerArguments);
        Assert.Empty(payload.RunnerEnvironmentVariables);
        Assert.Empty(payload.RunnerEnvironmentSecrets);
        Assert.Empty(payload.RunnerEnvironmentVariableValues);
        Assert.Empty(payload.RunnerEnvironmentSecretValues);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithExecuteMethodBuildRun_CreatesRunnerPayload ()
    {
        var builder = new UnityIpcRequestBuilder();

        var request = builder.Build(new UnityRequestPayload.BuildRun(
            RunId: "build-run-1",
            BuildTarget: "standaloneLinux64",
            UnityBuildTarget: "StandaloneLinux64",
            SceneSource: "explicit",
            ScenePaths: ["Assets/Scenes/Main.unity"],
            Development: true,
            OutputPath: "/tmp/ucli/output",
            OutputLayout: null,
            BuildReportPath: "/tmp/ucli/build-report.json",
            BuildLogPath: "/tmp/ucli/build.log",
            AllowedEditorModes: ["batchmode"],
            ProjectMutationMode: "forbid",
            RunnerKind: "executeMethod")
        {
            ProfilePath = "/workspace/build.ucli.json",
            ProfileDigest = new string('a', 64),
            RunnerMethod = "Build.Entry.Run",
            RunnerArguments = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["output"] = "/tmp/ucli/output",
            },
            RunnerEnvironmentVariables = ["UCLI_MODE"],
            RunnerEnvironmentSecrets = ["UCLI_SECRET"],
            RunnerEnvironmentVariableValues = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["UCLI_MODE"] = "release",
            },
            RunnerEnvironmentSecretValues = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["UCLI_SECRET"] = "secret-value",
            },
        });

        Assert.Equal(IpcMethodNames.BuildRun, request.Method);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcBuildRunRequest payload, out _));
        Assert.Equal("executeMethod", payload.RunnerKind);
        Assert.Null(payload.OutputLayout);
        Assert.Equal("/workspace/build.ucli.json", payload.ProfilePath);
        Assert.Equal(new string('a', 64), payload.ProfileDigest);
        Assert.Equal("Build.Entry.Run", payload.RunnerMethod);
        Assert.Equal("/tmp/ucli/output", payload.RunnerArguments["output"]);
        Assert.Equal(["UCLI_MODE"], payload.RunnerEnvironmentVariables);
        Assert.Equal(["UCLI_SECRET"], payload.RunnerEnvironmentSecrets);
        Assert.Equal("release", payload.RunnerEnvironmentVariableValues["UCLI_MODE"]);
        Assert.Equal("secret-value", payload.RunnerEnvironmentSecretValues["UCLI_SECRET"]);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithPlayStatus_CreatesPlayStatusPayload ()
    {
        var builder = new UnityIpcRequestBuilder();

        var request = builder.Build(new UnityRequestPayload.PlayStatus());

        Assert.Equal(IpcMethodNames.PlayStatus, request.Method);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcPlayStatusRequest _, out _));
        Assert.Empty(request.AllowedStartupLifecycleStates);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithPlayEnter_CreatesPlayEnterPayload ()
    {
        var builder = new UnityIpcRequestBuilder();

        var request = builder.Build(new UnityRequestPayload.PlayEnter(1500));

        Assert.Equal(IpcMethodNames.PlayEnter, request.Method);
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

        Assert.Equal(IpcMethodNames.PlayExit, request.Method);
        Assert.True(request.IsRecoverable);
        Assert.Equal(TimeSpan.FromMilliseconds(1000), request.RecoverableResponseAttemptTimeout);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcPlayExitRequest payload, out _));
        Assert.Equal(2500, payload.TimeoutMilliseconds);
        Assert.Empty(request.AllowedStartupLifecycleStates);
    }

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
        var dispatchRequest = new UnityIpcRequestBuilder().Build(new UnityRequestPayload.BuildRun(
            RunId: "build-run-1",
            BuildTarget: "standaloneLinux64",
            UnityBuildTarget: "StandaloneLinux64",
            SceneSource: "explicit",
            ScenePaths: ["Assets/Scenes/Main.unity"],
            Development: false,
            OutputPath: "/tmp/ucli/output",
            OutputLayout: new IpcBuildOutputLayout(
                Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
                LocationPathName: "/tmp/ucli/output/player/Player"),
            BuildReportPath: "/tmp/ucli/build-report.json",
            BuildLogPath: "/tmp/ucli/build.log",
            AllowedEditorModes: ["batchmode"],
            ProjectMutationMode: "forbid",
            RunnerKind: "buildPipeline"));

        var request = UnityIpcRequestFactory.Create(
            "session-token",
            dispatchRequest,
            TimeSpan.FromMilliseconds(1234));

        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcBuildRunRequest buildRunRequest, out _));
        Assert.Equal("build-run-1", buildRunRequest.RunId);
        Assert.Equal(1234, buildRunRequest.TimeoutMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithExecuteJson_CreatesExecutePayload ()
    {
        var executeArguments = JsonSerializer.SerializeToElement(new
        {
            requestId = "request-1",
        });
        var builder = new UnityIpcRequestBuilder();

        var request = builder.Build(new UnityRequestPayload.ExecuteJson(
            UcliCommandIds.Plan,
            executeArguments,
            FailFast: true,
            AllowDangerous: true,
            AllowPlayMode: true,
            PlanToken: "plan-token"));

        Assert.Equal(IpcMethodNames.Execute, request.Method);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcExecuteRequest payload, out _));
        Assert.Equal(UcliCommandIds.Plan.Name, payload.Command);
        Assert.True(payload.FailFast);
        Assert.True(payload.AllowDangerous);
        Assert.True(payload.AllowPlayMode);
        Assert.Equal("plan-token", payload.PlanToken);
        Assert.Equal(executeArguments.GetRawText(), payload.Arguments.GetRawText());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithExecuteOperation_CreatesSingleOperationExecutePayload ()
    {
        var args = JsonSerializer.SerializeToElement(new
        {
            path = "Assets/Test.prefab",
        });
        var builder = new UnityIpcRequestBuilder();

        var request = builder.Build(new UnityRequestPayload.ExecuteOperation(
            UcliCommandIds.Call,
            "request-1",
            "op-1",
            "asset.create",
            args,
            FailFast: false,
            AllowDangerous: true,
            PlanToken: "plan-token"));

        Assert.Equal(IpcMethodNames.Execute, request.Method);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcExecuteRequest payload, out _));
        Assert.Equal(UcliCommandIds.Call.Name, payload.Command);
        Assert.False(payload.FailFast);
        Assert.True(payload.AllowDangerous);
        Assert.False(payload.AllowPlayMode);
        Assert.Equal("plan-token", payload.PlanToken);
        Assert.Equal(IpcProtocol.CurrentVersion, payload.Arguments.GetProperty("protocolVersion").GetInt32());
        Assert.Equal("request-1", payload.Arguments.GetProperty("requestId").GetString());
        var step = payload.Arguments.GetProperty("steps")[0];
        Assert.Equal("op", step.GetProperty("kind").GetString());
        Assert.Equal("op-1", step.GetProperty("id").GetString());
        Assert.Equal("asset.create", step.GetProperty("op").GetString());
        Assert.Equal(args.GetRawText(), step.GetProperty("args").GetRawText());
    }
}
