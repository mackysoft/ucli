namespace MackySoft.Ucli.Application.Tests;

internal static class BuildRunInvocationAssert
{
    public static UnityRequestPayload.BuildRun ExplicitBuildPipelineRequest (
        RecordingUnityRequestExecutor requestExecutor,
        string expectedRunId,
        string expectedRunnerOutputDirectory,
        string expectedBuildReportPath,
        string expectedBuildLogPath,
        string expectedLocationPathName)
    {
        var request = BuildRunRequest(requestExecutor);
        Assert.Equal(expectedRunId, request.RunId);
        Assert.Equal("explicit", request.InputKind);
        Assert.Equal("standaloneLinux64", request.BuildTarget);
        Assert.Equal("StandaloneLinux64", request.UnityBuildTarget);
        Assert.Equal("explicit", request.SceneSource);
        Assert.Equal(["Assets/Scenes/Main.unity"], request.ScenePaths);
        Assert.True(request.Development);
        Assert.Equal(expectedRunnerOutputDirectory, request.OutputPath);
        Assert.NotNull(request.OutputLayout);
        Assert.Equal("file", request.OutputLayout!.Shape);
        Assert.Equal(expectedLocationPathName, request.OutputLayout.LocationPathName);
        Assert.Equal(expectedBuildReportPath, request.BuildReportPath);
        Assert.Equal(expectedBuildLogPath, request.BuildLogPath);
        Assert.Equal(["batchmode", "gui"], request.AllowedEditorModes);
        Assert.Equal("forbid", request.ProjectMutationMode);
        Assert.Equal("buildPipeline", request.RunnerKind);
        Assert.Null(request.ProfilePath);
        Assert.Null(request.RunnerMethod);
        return request;
    }

    public static UnityRequestPayload.BuildRun ExecuteMethodRunnerRequest (
        RecordingUnityRequestExecutor requestExecutor,
        string expectedRunId,
        string expectedProfilePath,
        string expectedProfileDigest,
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
        Assert.Equal("executeMethod", request.RunnerKind);
        Assert.Null(request.OutputLayout);
        Assert.Equal(expectedProfilePath, request.ProfilePath);
        Assert.Equal(expectedProfileDigest, request.ProfileDigest);
        Assert.Equal("Build.Entry.Run", request.RunnerMethod);
        Assert.Equal(expectedRunId, request.RunnerArguments["run"]);
        Assert.Equal(expectedOutputDirectory, request.RunnerArguments["output"]);
        Assert.Equal(expectedProfilePath, request.RunnerArguments["profile"]);
        Assert.Equal(expectedProfileDigest, request.RunnerArguments["digest"]);
        Assert.Equal(expectedProjectPath, request.RunnerArguments["project"]);
        Assert.Equal(expectedProjectFingerprint.ToString(), request.RunnerArguments["fingerprint"]);
        Assert.Equal(expectedBuildTarget, request.RunnerArguments["target"]);
        Assert.Equal([expectedEnvironmentVariable], request.RunnerEnvironmentVariables);
        Assert.Equal([expectedEnvironmentSecret], request.RunnerEnvironmentSecrets);
        Assert.Equal(expectedEnvironmentValue, request.RunnerEnvironmentVariableValues[expectedEnvironmentVariable]);
        Assert.Equal(expectedSecretValue, request.RunnerEnvironmentSecretValues[expectedEnvironmentSecret]);
        return request;
    }

    public static UnityRequestPayload.BuildRun UnityBuildProfileInputDelegatedToUnity (
        RecordingUnityRequestExecutor requestExecutor,
        string expectedUnityBuildProfilePath)
    {
        var request = BuildRunRequest(requestExecutor);
        Assert.Equal("unityBuildProfile", request.InputKind);
        Assert.Null(request.BuildTarget);
        Assert.Null(request.UnityBuildTarget);
        Assert.Null(request.SceneSource);
        Assert.Empty(request.ScenePaths);
        Assert.False(request.Development);
        Assert.Null(request.OutputLayout);
        Assert.NotNull(request.UnityBuildProfile);
        Assert.Equal(expectedUnityBuildProfilePath, request.UnityBuildProfile!.Path);
        Assert.Null(request.UnityBuildProfile.Digest);
        Assert.Null(request.UnityBuildProfile.ApplyAudit);
        return request;
    }

    public static UnityRequestPayload.BuildRun EditorBuildSettingsDelegatedToUnity (RecordingUnityRequestExecutor requestExecutor)
    {
        var request = BuildRunRequest(requestExecutor);
        Assert.Equal("editorBuildSettings", request.SceneSource);
        Assert.Empty(request.ScenePaths);
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
