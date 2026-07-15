namespace MackySoft.Ucli.Tests.Ipc;

using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Daemon;
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
                Shape: IpcBuildOutputLayoutShape.File,
                LocationPathName: "/tmp/ucli/output/player/Player"),
            development: true));

        Assert.Equal(UnityIpcMethod.BuildRun, request.Method);
        Assert.False(request.IsRecoverable);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcBuildRunRequest payload, out _));
        Assert.Equal(RunIdTestValues.Build, payload.RunId);
        Assert.Equal(BuildProfileInputsKind.Explicit, payload.InputKind);
        Assert.Equal(BuildTargetStableName.StandaloneLinux64, payload.BuildTarget);
        Assert.Equal(BuildProfileSceneSource.Explicit, payload.SceneSource);
        Assert.Equal([new SceneAssetPath("Assets/Scenes/Main.unity")], payload.ScenePaths);
        Assert.True(payload.Development);
        Assert.Equal("/tmp/ucli/output", payload.OutputPath);
        Assert.NotNull(payload.OutputLayout);
        Assert.Equal(IpcBuildOutputLayoutShape.File, payload.OutputLayout!.Shape);
        Assert.Equal("/tmp/ucli/output/player/Player", payload.OutputLayout.LocationPathName);
        Assert.Equal("/tmp/ucli/build-report.json", payload.BuildReportPath);
        Assert.Equal("/tmp/ucli/build.log", payload.BuildLogPath);
        Assert.Equal([DaemonEditorMode.Batchmode], payload.AllowedEditorModes);
        Assert.Equal(BuildProfileProjectMutationMode.Forbid, payload.ProjectMutationMode);
        Assert.False(request.Payload.TryGetProperty("timeoutMilliseconds", out _));
        Assert.Equal(BuildRunnerKind.BuildPipeline, payload.RunnerKind);
        Assert.Null(payload.ProfilePath);
        Assert.Null(payload.RunnerMethod);
        Assert.Empty(payload.RunnerArguments);
        Assert.Empty(payload.RunnerEnvironmentVariables);
        Assert.Empty(payload.RunnerEnvironmentSecrets);
        Assert.Empty(payload.RunnerEnvironmentVariableValues);
        Assert.Empty(payload.RunnerEnvironmentSecretValues);
        Assert.Null(payload.UnityBuildProfile);
        Assert.False(request.Payload.TryGetProperty("unityBuildTarget", out _));
        Assert.Null(request.LaunchOptions.ActiveBuildProfilePath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithUnityBuildProfileBuildRun_SetsOneshotActiveBuildProfilePath ()
    {
        var builder = new UnityIpcRequestBuilder();

        var request = builder.Build(new UnityRequestPayload.BuildRun(new IpcBuildRunRequest(
            RunId: RunIdTestValues.Build,
            InputKind: BuildProfileInputsKind.UnityBuildProfile,
            BuildTarget: null,
            SceneSource: null,
            ScenePaths: [],
            Development: false,
            OutputPath: "/tmp/ucli/output",
            OutputLayout: null,
            BuildReportPath: "/tmp/ucli/build-report.json",
            BuildLogPath: "/tmp/ucli/build.log",
            AllowedEditorModes: [DaemonEditorMode.Batchmode],
            ProjectMutationMode: BuildProfileProjectMutationMode.Audit,
            RunnerKind: BuildRunnerKind.BuildPipeline,
            ProfileDigest: Sha256Digest.Parse(new string('a', 64)),
            UnityBuildProfile: new IpcUnityBuildProfileInput(
                Path: new UnityBuildProfileAssetPath("Assets/BuildProfiles/LinuxPlayer.asset"),
                Digest: null,
                ApplyAudit: null),
            ProfilePath: null,
            RunnerMethod: null,
            RunnerArguments: new Dictionary<string, string>(StringComparer.Ordinal),
            RunnerEnvironmentVariables: [],
            RunnerEnvironmentSecrets: [],
            RunnerEnvironmentVariableValues: new Dictionary<string, string>(StringComparer.Ordinal),
            RunnerEnvironmentSecretValues: new Dictionary<string, string>(StringComparer.Ordinal))));

        Assert.Equal(UnityIpcMethod.BuildRun, request.Method);
        Assert.Equal("Assets/BuildProfiles/LinuxPlayer.asset", request.LaunchOptions.ActiveBuildProfilePath!.Value);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcBuildRunRequest payload, out _));
        Assert.NotNull(payload.UnityBuildProfile);
        Assert.Equal("Assets/BuildProfiles/LinuxPlayer.asset", payload.UnityBuildProfile!.Path.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithExecuteMethodBuildRun_CreatesRunnerPayload ()
    {
        var builder = new UnityIpcRequestBuilder();
        var requestPayload = new UnityRequestPayload.BuildRun(new IpcBuildRunRequest(
            RunId: RunIdTestValues.Build,
            InputKind: BuildProfileInputsKind.Explicit,
            BuildTarget: BuildTargetStableName.StandaloneLinux64,
            SceneSource: BuildProfileSceneSource.Explicit,
            ScenePaths: [new SceneAssetPath("Assets/Scenes/Main.unity")],
            Development: false,
            OutputPath: "/tmp/ucli/output",
            OutputLayout: null,
            BuildReportPath: "/tmp/ucli/build-report.json",
            BuildLogPath: "/tmp/ucli/build.log",
            AllowedEditorModes: [DaemonEditorMode.Batchmode],
            ProjectMutationMode: BuildProfileProjectMutationMode.Forbid,
            RunnerKind: BuildRunnerKind.ExecuteMethod,
            ProfileDigest: Sha256Digest.Parse(new string('a', 64)),
            UnityBuildProfile: null,
            ProfilePath: "/workspace/build.ucli.json",
            RunnerMethod: "Build.Entry.Run",
            RunnerArguments: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["output"] = "/tmp/ucli/output",
            },
            RunnerEnvironmentVariables: ["UCLI_MODE"],
            RunnerEnvironmentSecrets: ["UCLI_SECRET"],
            RunnerEnvironmentVariableValues: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["UCLI_MODE"] = "release",
            },
            RunnerEnvironmentSecretValues: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["UCLI_SECRET"] = "secret-value",
            }));

        var request = builder.Build(requestPayload);

        Assert.Equal(UnityIpcMethod.BuildRun, request.Method);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcBuildRunRequest payload, out _));
        Assert.Equal(BuildRunnerKind.ExecuteMethod, payload.RunnerKind);
        Assert.Null(payload.OutputLayout);
        Assert.Equal("/workspace/build.ucli.json", payload.ProfilePath);
        Assert.Equal(Sha256Digest.Parse(new string('a', 64)), payload.ProfileDigest);
        Assert.Equal("Build.Entry.Run", payload.RunnerMethod);
        Assert.Equal("/tmp/ucli/output", payload.RunnerArguments["output"]);
        Assert.Equal(["UCLI_MODE"], payload.RunnerEnvironmentVariables);
        Assert.Equal(["UCLI_SECRET"], payload.RunnerEnvironmentSecrets);
        Assert.Equal("release", payload.RunnerEnvironmentVariableValues["UCLI_MODE"]);
        Assert.Equal("secret-value", payload.RunnerEnvironmentSecretValues["UCLI_SECRET"]);
    }
}
