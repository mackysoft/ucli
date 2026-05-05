using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;

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
            "session-token",
            failFast: true);

        Assert.Equal(IpcMethodNames.TestRun, request.Method);
        Assert.Equal(IpcProtocol.CurrentVersion, request.ProtocolVersion);
        Assert.Equal("session-token", request.SessionToken);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcTestRunRequest payload, out _));
        Assert.Equal("StandaloneWindows64", payload.TestPlatform);
        Assert.Equal(configuration.TestFilter, payload.TestFilter);
        Assert.Equal(configuration.TestCategories, payload.TestCategories);
        Assert.Equal(configuration.AssemblyNames, payload.AssemblyNames);
        Assert.Equal(configuration.TestSettingsPath, payload.TestSettingsPath);
        Assert.Equal(artifactPaths.ResultsXmlPath, payload.ResultsXmlPath);
        Assert.Equal(artifactPaths.EditorLogPath, payload.EditorLogPath);
        Assert.True(payload.FailFast);
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
            Mode: UnityExecutionMode.Daemon,
            UnityVersion: "6000.1.4f1",
            UnityEditorPath: scope.GetPath("Editors/6000.1.4f1/Editor/Unity"),
            TestPlatform: TestRunPlatform.Player("StandaloneWindows64"),
            TestFilter: "Category=Smoke",
            TestCategories: ["smoke", "quick"],
            AssemblyNames: ["Game.Tests"],
            TestSettingsPath: scope.GetPath("ProjectSettings/TestSettings.json"),
            TimeoutMilliseconds: null);
    }
}
