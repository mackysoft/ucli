using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Hosting.Cli.Testing;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class TestRunCommandDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_WithSupportedOptions_DispatchesResolvedInputAndCancellationToken ()
    {
        var artifactsDir = AbsolutePath.Parse(Path.Combine(Path.GetTempPath(), "ucli-test-run-artifacts"));
        var service = new RecordingTestRunService(
            (_, _, _) => ValueTask.FromResult<TestRunServiceResult>(TestRunResultTestValues.CreateCompleted(
                Verdict.Pass,
                TestArtifactPaths.CreateSession(
                    RunIdTestValues.Test,
                    artifactsDir.Value))));
        var command = new TestRunCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);
        var profilePath = AbsolutePath.Parse(Path.GetFullPath("test.profile.json"));
        var unityEditorPath = AbsolutePath.Parse(Path.GetFullPath(Path.Combine("Editors", "6000.1.4f1", "Unity")));
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await CommandResultCapture.ExecuteAsync(() => command.RunAsync(
            projectPath: AbsolutePath.Parse(ProjectPathTestValues.RepositoryUnityProject),
            profilePath: profilePath,
            executionMode: "oneshot",
            unityVersion: "6000.1.4f1",
            unityEditorPath: unityEditorPath,
            testPlatform: "Android",
            testFilter: "Name~Smoke",
            testCategory: "smoke, fast,nightly",
            assemblyName: "MyGame.Tests.EditMode,MyGame.Tests.PlayMode",
            timeout: 120,
            failFast: true,
            allowEmptyTestRun: true,
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var invocation = Assert.Single(service.Invocations);
        Assert.Equal(cancellationTokenSource.Token, invocation.CancellationToken);

        var input = Assert.IsType<TestRunCommandInput>(invocation.Input);
        ProjectPathDispatchAssert.EqualNormalized(ProjectPathTestValues.RepositoryUnityProject, input.ProjectPath);
        Assert.Equal(profilePath, input.ProfilePath);
        Assert.Equal(UnityExecutionMode.Oneshot, input.Mode);
        Assert.Equal("6000.1.4f1", input.UnityVersion);
        Assert.Equal(unityEditorPath, input.UnityEditorPath);
        Assert.Equal(TestRunPlatform.Player("Android"), input.TestPlatform);
        Assert.Equal("Name~Smoke", input.TestFilter);
        Assert.Equal(["smoke", "fast", "nightly"], Assert.IsType<string[]>(input.TestCategory));
        Assert.Equal(["MyGame.Tests.EditMode", "MyGame.Tests.PlayMode"], Assert.IsType<string[]>(input.AssemblyName));
        Assert.Equal(120, input.TimeoutMilliseconds);
        Assert.True(input.FailFast);
        Assert.True(input.AllowEmptyTestRun);
    }
}
