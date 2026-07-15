using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Methods.Build;

public sealed class IpcBuildRunRequestShapeTests
{
    private static readonly Guid RunId = Guid.Parse("00000000-0000-0000-0000-000000000621");
    private static readonly Sha256Digest ProfileDigest = Sha256Digest.Parse(new string('a', 64));

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenExplicitBuildPipelineHasNoOutputLayout_Throws ()
    {
        Assert.Throws<ArgumentNullException>(() => CreateExplicitBuildPipeline(outputLayout: null));
    }

    [Theory]
    [InlineData("ProfilePath")]
    [InlineData("RunnerMethod")]
    [Trait("Size", "Small")]
    public void Constructor_WhenExecuteMethodInvocationValueIsMissing_Throws (string missingValue)
    {
        Assert.ThrowsAny<ArgumentException>(() => CreateExplicitExecuteMethod(
            profilePath: missingValue == "ProfilePath" ? null : "/tmp/ucli/build.ucli.json",
            runnerMethod: missingValue == "RunnerMethod" ? null : "Build.Entry.Run"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenUnityBuildProfileUsesExecuteMethod_Throws ()
    {
        Assert.Throws<ArgumentException>(() => CreateUnityBuildProfile(
            BuildRunnerKind.ExecuteMethod,
            CreateUnityBuildProfileInput()));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenUnityBuildProfilePayloadIsMissing_Throws ()
    {
        Assert.Throws<ArgumentNullException>(() => CreateUnityBuildProfile(
            BuildRunnerKind.BuildPipeline,
            unityBuildProfile: null));
    }

    private static IpcBuildRunRequest CreateExplicitBuildPipeline (IpcBuildOutputLayout? outputLayout)
    {
        return new IpcBuildRunRequest(
            RunId: RunId,
            InputKind: BuildProfileInputsKind.Explicit,
            BuildTarget: BuildTargetStableName.StandaloneLinux64,
            SceneSource: BuildProfileSceneSource.Explicit,
            ScenePaths: [new SceneAssetPath("Assets/Scenes/Main.unity")],
            Development: false,
            OutputPath: "/tmp/ucli/output",
            OutputLayout: outputLayout,
            BuildReportPath: "/tmp/ucli/build-report.json",
            BuildLogPath: "/tmp/ucli/build.log",
            AllowedEditorModes: [DaemonEditorMode.Batchmode],
            ProjectMutationMode: BuildProfileProjectMutationMode.Forbid,
            RunnerKind: BuildRunnerKind.BuildPipeline,
            ProfileDigest: ProfileDigest,
            UnityBuildProfile: null,
            ProfilePath: null,
            RunnerMethod: null,
            RunnerArguments: new Dictionary<string, string>(),
            RunnerEnvironmentVariables: [],
            RunnerEnvironmentSecrets: [],
            RunnerEnvironmentVariableValues: new Dictionary<string, string>(),
            RunnerEnvironmentSecretValues: new Dictionary<string, string>());
    }

    private static IpcBuildRunRequest CreateExplicitExecuteMethod (
        string? profilePath,
        string? runnerMethod)
    {
        return new IpcBuildRunRequest(
            RunId: RunId,
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
            ProfileDigest: ProfileDigest,
            UnityBuildProfile: null,
            ProfilePath: profilePath,
            RunnerMethod: runnerMethod,
            RunnerArguments: new Dictionary<string, string>(),
            RunnerEnvironmentVariables: [],
            RunnerEnvironmentSecrets: [],
            RunnerEnvironmentVariableValues: new Dictionary<string, string>(),
            RunnerEnvironmentSecretValues: new Dictionary<string, string>());
    }

    private static IpcBuildRunRequest CreateUnityBuildProfile (
        BuildRunnerKind runnerKind,
        IpcUnityBuildProfileInput? unityBuildProfile)
    {
        return new IpcBuildRunRequest(
            RunId: RunId,
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
            ProjectMutationMode: BuildProfileProjectMutationMode.Forbid,
            RunnerKind: runnerKind,
            ProfileDigest: ProfileDigest,
            UnityBuildProfile: unityBuildProfile,
            ProfilePath: runnerKind == BuildRunnerKind.ExecuteMethod ? "/tmp/ucli/build.ucli.json" : null,
            RunnerMethod: runnerKind == BuildRunnerKind.ExecuteMethod ? "Build.Entry.Run" : null,
            RunnerArguments: new Dictionary<string, string>(),
            RunnerEnvironmentVariables: [],
            RunnerEnvironmentSecrets: [],
            RunnerEnvironmentVariableValues: new Dictionary<string, string>(),
            RunnerEnvironmentSecretValues: new Dictionary<string, string>());
    }

    private static IpcUnityBuildProfileInput CreateUnityBuildProfileInput ()
    {
        return new IpcUnityBuildProfileInput(
            Path: new UnityBuildProfileAssetPath("Assets/BuildProfiles/Linux.asset"),
            Digest: null,
            ApplyAudit: null);
    }
}
