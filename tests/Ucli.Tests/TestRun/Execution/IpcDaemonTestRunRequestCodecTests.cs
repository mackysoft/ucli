using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.TestRun.Artifacts;
using MackySoft.Ucli.TestRun.Configuration;
using MackySoft.Ucli.TestRun.Execution;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests;

public sealed class IpcDaemonTestRunRequestCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CreateRequest_WithResolvedConfiguration_ReturnsTestRunRequest ()
    {
        using var scope = TestDirectories.CreateTempScope("ipc-daemon-test-run-request-codec", "create");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = new ArtifactPaths(scope.GetPath("run"));

        var request = IpcDaemonTestRunRequestCodec.CreateRequest(
            configuration,
            artifactPaths,
            "session-token");

        Assert.Equal(IpcMethodNames.TestRun, request.Method);
        Assert.Equal(IpcProtocol.CurrentVersion, request.ProtocolVersion);
        Assert.Equal("session-token", request.SessionToken);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcTestRunRequest payload, out _));
        Assert.Equal(IpcTestRunPlatformCodec.PlayMode, payload.TestPlatform);
        Assert.Equal(configuration.BuildTarget, payload.BuildTarget);
        Assert.Equal(configuration.TestFilter, payload.TestFilter);
        Assert.Equal(configuration.TestCategories, payload.TestCategories);
        Assert.Equal(configuration.AssemblyNames, payload.AssemblyNames);
        Assert.Equal(configuration.TestSettingsPath, payload.TestSettingsPath);
        Assert.Equal(artifactPaths.ResultsXmlPath, payload.ResultsXmlPath);
        Assert.Equal(artifactPaths.EditorLogPath, payload.EditorLogPath);
    }

    private static ResolvedTestRunConfiguration CreateConfiguration (TestDirectoryScope scope)
    {
        var projectPath = scope.GetPath("UnityProject");
        return new ResolvedTestRunConfiguration(
            UnityProject: new ResolvedUnityProjectContext(
                UnityProjectRoot: projectPath,
                RepositoryRoot: scope.FullPath,
                ProjectFingerprint: "fingerprint",
                PathSource: UnityProjectPathSource.CommandOption),
            Mode: "daemon",
            UnityVersion: "6000.1.4f1",
            UnityEditorPath: scope.GetPath("Editors/6000.1.4f1/Editor/Unity"),
            TestPlatform: IpcTestRunPlatform.PlayMode,
            RawTestPlatform: IpcTestRunPlatformCodec.PlayMode,
            BuildTarget: "StandaloneWindows64",
            TestFilter: "Category=Smoke",
            TestCategories: ["smoke", "quick"],
            AssemblyNames: ["Game.Tests"],
            TestSettingsPath: scope.GetPath("ProjectSettings/TestSettings.json"),
            TimeoutMilliseconds: null);
    }
}