using MackySoft.Tests;
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
        var artifactsDir = Path.Combine(Path.GetTempPath(), "ucli-test-run-artifacts");
        var summaryJsonPath = Path.Combine(artifactsDir, "summary.json");
        var service = new RecordingTestRunService(
            (_, _, _) => ValueTask.FromResult(TestRunServiceResult.Pass(
                message: "Unity test execution completed.",
                runId: RunIdTestValues.Test,
                artifactsDir: artifactsDir,
                summaryJsonPath: summaryJsonPath)));
        var command = new TestRunCommand(service, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await CommandResultCapture.ExecuteAsync(() => command.RunAsync(
            projectPath: "/repo/UnityProject",
            profilePath: "/repo/test.profile.json",
            executionMode: "oneshot",
            unityVersion: "6000.1.4f1",
            unityEditorPath: "/Applications/Unity/Hub/Editor/6000.1.4f1/Unity.app",
            testPlatform: "Android",
            testFilter: "Name~Smoke",
            testCategory: "smoke, fast,nightly",
            assemblyName: "MyGame.Tests.EditMode,MyGame.Tests.PlayMode",
            testSettingsPath: "/repo/UnityProject/ProjectSettings/TestSettings.json",
            timeout: 120,
            failFast: true,
            allowEmptyTestRun: true,
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var invocation = Assert.Single(service.Invocations);
        Assert.Equal(cancellationTokenSource.Token, invocation.CancellationToken);

        var input = Assert.IsType<TestRunCommandInput>(invocation.Input);
        Assert.Equal("/repo/UnityProject", input.ProjectPath);
        Assert.Equal("/repo/test.profile.json", input.ProfilePath);
        Assert.Equal(UnityExecutionMode.Oneshot, input.Mode);
        Assert.Equal("6000.1.4f1", input.UnityVersion);
        Assert.Equal("/Applications/Unity/Hub/Editor/6000.1.4f1/Unity.app", input.UnityEditorPath);
        Assert.Equal(TestRunPlatform.Player("Android"), input.TestPlatform);
        Assert.Equal("Name~Smoke", input.TestFilter);
        Assert.Equal(["smoke", "fast", "nightly"], Assert.IsType<string[]>(input.TestCategory));
        Assert.Equal(["MyGame.Tests.EditMode", "MyGame.Tests.PlayMode"], Assert.IsType<string[]>(input.AssemblyName));
        Assert.Equal("/repo/UnityProject/ProjectSettings/TestSettings.json", input.TestSettingsPath);
        Assert.Equal(120, input.TimeoutMilliseconds);
        Assert.True(input.FailFast);
        Assert.True(input.AllowEmptyTestRun);
    }
}
