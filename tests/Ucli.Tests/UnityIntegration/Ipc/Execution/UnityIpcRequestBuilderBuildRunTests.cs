namespace MackySoft.Ucli.Tests.Ipc;

using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;
using static MackySoft.Ucli.Tests.Ipc.UnityIpcRequestBuilderTestSupport;

public sealed class UnityIpcRequestBuilderBuildRunTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithBuildRun_CreatesBuildRunPayload ()
    {
        var builder = new UnityIpcRequestBuilder();

        var request = builder.Build(CreateExplicitBuildRunPayload(
            outputLayout: new IpcBuildOutputLayout(
                Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
                LocationPathName: "/tmp/ucli/output/player/Player"),
            development: true));

        Assert.Equal(UnityIpcMethod.BuildRun, request.Method);
        Assert.False(request.IsRecoverable);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcBuildRunRequest payload, out _));
        Assert.Equal(RunIdTestValues.Build, payload.RunId);
        Assert.Equal(ContractLiteralCodec.ToValue(BuildProfileInputsKind.Explicit), payload.InputKind);
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
        Assert.Null(payload.ProfilePath);
        Assert.Null(payload.RunnerMethod);
        Assert.Empty(payload.RunnerArguments);
        Assert.Empty(payload.RunnerEnvironmentVariables);
        Assert.Empty(payload.RunnerEnvironmentSecrets);
        Assert.Empty(payload.RunnerEnvironmentVariableValues);
        Assert.Empty(payload.RunnerEnvironmentSecretValues);
        Assert.Null(payload.UnityBuildProfile);
        Assert.Null(request.OneshotActiveBuildProfilePath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithUnityBuildProfileBuildRun_SetsOneshotActiveBuildProfilePath ()
    {
        var builder = new UnityIpcRequestBuilder();

        var request = builder.Build(new UnityRequestPayload.BuildRun(
            RunId: RunIdTestValues.Build,
            InputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile),
            BuildTarget: null,
            UnityBuildTarget: null,
            SceneSource: null,
            ScenePaths: [],
            Development: false,
            OutputPath: "/tmp/ucli/output",
            OutputLayout: null,
            BuildReportPath: "/tmp/ucli/build-report.json",
            BuildLogPath: "/tmp/ucli/build.log",
            AllowedEditorModes: ["batchmode"],
            ProjectMutationMode: "audit",
            RunnerKind: "buildPipeline")
        {
            UnityBuildProfile = new IpcUnityBuildProfileInput("Assets/BuildProfiles/LinuxPlayer.asset"),
        });

        Assert.Equal(UnityIpcMethod.BuildRun, request.Method);
        Assert.Equal("Assets/BuildProfiles/LinuxPlayer.asset", request.OneshotActiveBuildProfilePath);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcBuildRunRequest payload, out _));
        Assert.NotNull(payload.UnityBuildProfile);
        Assert.Equal("Assets/BuildProfiles/LinuxPlayer.asset", payload.UnityBuildProfile!.Path);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithExecuteMethodBuildRun_CreatesRunnerPayload ()
    {
        var builder = new UnityIpcRequestBuilder();
        var requestPayload = new UnityRequestPayload.BuildRun(
            RunId: RunIdTestValues.Build,
            InputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.Explicit),
            BuildTarget: "standaloneLinux64",
            UnityBuildTarget: "StandaloneLinux64",
            SceneSource: "explicit",
            ScenePaths: ["Assets/Scenes/Main.unity"],
            Development: false,
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
        };

        var request = builder.Build(requestPayload);

        Assert.Equal(UnityIpcMethod.BuildRun, request.Method);
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
}
