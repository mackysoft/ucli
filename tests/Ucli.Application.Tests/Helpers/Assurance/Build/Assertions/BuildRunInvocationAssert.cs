using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

internal static class BuildRunInvocationAssert
{
    public static UnityRequestPayload.BuildRun ExplicitBuildPipelineRequest (
        RecordingUnityRequestExecutor requestExecutor,
        Guid expectedRunId,
        string expectedRunnerOutputDirectory,
        string expectedBuildReportPath,
        string expectedBuildLogPath,
        string expectedLocationPathName)
    {
        var request = BuildRunRequest(requestExecutor);
        var contract = request.Request;
        Assert.Equal(expectedRunId, contract.RunId);
        Assert.Equal(BuildProfileInputsKind.Explicit, contract.InputKind);
        Assert.Equal(BuildTargetStableName.StandaloneLinux64, contract.BuildTarget);
        Assert.Equal(BuildProfileSceneSource.Explicit, contract.SceneSource);
        Assert.Equal([new SceneAssetPath("Assets/Scenes/Main.unity")], contract.ScenePaths);
        Assert.True(contract.Development);
        Assert.Equal(expectedRunnerOutputDirectory, contract.OutputPath);
        Assert.NotNull(contract.OutputLayout);
        Assert.Equal(IpcBuildOutputLayoutShape.File, contract.OutputLayout!.Shape);
        Assert.Equal(expectedLocationPathName, contract.OutputLayout.LocationPathName);
        Assert.Equal(expectedBuildReportPath, contract.BuildReportPath);
        Assert.Equal(expectedBuildLogPath, contract.BuildLogPath);
        Assert.Equal([DaemonEditorMode.Batchmode, DaemonEditorMode.Gui], contract.AllowedEditorModes);
        Assert.Equal(BuildProfileProjectMutationMode.Forbid, contract.ProjectMutationMode);
        Assert.Equal(BuildRunnerKind.BuildPipeline, contract.RunnerKind);
        Assert.Null(contract.ProfilePath);
        Assert.Null(contract.RunnerMethod);
        return request;
    }

    public static UnityRequestPayload.BuildRun ExecuteMethodRunnerRequest (
        RecordingUnityRequestExecutor requestExecutor,
        Guid expectedRunId,
        string expectedProfilePath,
        Sha256Digest expectedProfileDigest,
        string expectedOutputDirectory,
        string expectedProjectPath,
        ProjectFingerprint expectedProjectFingerprint,
        string expectedBuildTarget,
        string expectedEnvironmentVariable,
        string expectedEnvironmentValue,
        string expectedEnvironmentSecret,
        string expectedSecretValue)
    {
        var request = BuildRunRequest(requestExecutor);
        var contract = request.Request;
        Assert.Equal(BuildRunnerKind.ExecuteMethod, contract.RunnerKind);
        Assert.Null(contract.OutputLayout);
        Assert.Equal(expectedProfilePath, contract.ProfilePath);
        Assert.Equal(expectedProfileDigest, contract.ProfileDigest);
        Assert.Equal("Build.Entry.Run", contract.RunnerMethod);
        Assert.Equal(expectedRunId.ToString("D"), contract.RunnerArguments["run"]);
        Assert.Equal(expectedOutputDirectory, contract.RunnerArguments["output"]);
        Assert.Equal(expectedProfilePath, contract.RunnerArguments["profile"]);
        Assert.Equal(expectedProfileDigest.ToString(), contract.RunnerArguments["digest"]);
        Assert.Equal(expectedProjectPath, contract.RunnerArguments["project"]);
        Assert.Equal(expectedProjectFingerprint.ToString(), contract.RunnerArguments["fingerprint"]);
        Assert.Equal(expectedBuildTarget, contract.RunnerArguments["target"]);
        Assert.Equal([expectedEnvironmentVariable], contract.RunnerEnvironmentVariables);
        Assert.Equal([expectedEnvironmentSecret], contract.RunnerEnvironmentSecrets);
        Assert.Equal(expectedEnvironmentValue, contract.RunnerEnvironmentVariableValues[expectedEnvironmentVariable]);
        Assert.Equal(expectedSecretValue, contract.RunnerEnvironmentSecretValues[expectedEnvironmentSecret]);
        return request;
    }

    public static UnityRequestPayload.BuildRun UnityBuildProfileInputDelegatedToUnity (
        RecordingUnityRequestExecutor requestExecutor,
        string expectedUnityBuildProfilePath)
    {
        var request = BuildRunRequest(requestExecutor);
        var contract = request.Request;
        Assert.Equal(BuildProfileInputsKind.UnityBuildProfile, contract.InputKind);
        Assert.Null(contract.BuildTarget);
        Assert.Null(contract.SceneSource);
        Assert.Empty(contract.ScenePaths);
        Assert.False(contract.Development);
        Assert.Null(contract.OutputLayout);
        Assert.NotNull(contract.UnityBuildProfile);
        Assert.Equal(expectedUnityBuildProfilePath, contract.UnityBuildProfile!.Path.Value);
        Assert.Null(contract.UnityBuildProfile.Digest);
        Assert.Null(contract.UnityBuildProfile.ApplyAudit);
        return request;
    }

    public static UnityRequestPayload.BuildRun EditorBuildSettingsDelegatedToUnity (RecordingUnityRequestExecutor requestExecutor)
    {
        var request = BuildRunRequest(requestExecutor);
        Assert.Equal(BuildProfileSceneSource.EditorBuildSettings, request.Request.SceneSource);
        Assert.Empty(request.Request.ScenePaths);
        return request;
    }

    public static RecordingUnityRequestExecutor.Invocation DispatchedWithTimeout (
        RecordingUnityRequestExecutor requestExecutor,
        TimeSpan expectedTimeout)
    {
        var invocation = Assert.Single(requestExecutor.Invocations);
        Assert.IsType<UnityRequestPayload.BuildRun>(invocation.Payload);
        Assert.Equal(expectedTimeout, invocation.Timeout);
        return invocation;
    }

    private static UnityRequestPayload.BuildRun BuildRunRequest (RecordingUnityRequestExecutor requestExecutor)
    {
        var invocation = Assert.Single(requestExecutor.Invocations);
        return Assert.IsType<UnityRequestPayload.BuildRun>(invocation.Payload);
    }
}
