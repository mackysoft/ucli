using MackySoft.Tests;
using MackySoft.Ucli.Cli;
using MackySoft.Ucli.TestRun;
using MackySoft.Ucli.TestRun.Service;

namespace MackySoft.Ucli.Tests;

public sealed class TestRunCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_MapsOptionsToServiceInputAndCancellationToken ()
    {
        var service = new StubTestRunService(
            (_, _) => ValueTask.FromResult(TestRunServiceResult.Pass(
                message: "Unity test execution completed.",
                runId: "run-id",
                artifactsDir: "/tmp/artifacts",
                summaryJsonPath: "/tmp/artifacts/summary.json")));
        var command = new TestRunCommand(service);
        using var cancellationTokenSource = new CancellationTokenSource();

        await StandardOutputCapture.Execute(() => command.Run(
            projectPath: "/repo/UnityProject",
            profilePath: "/repo/test.profile.json",
            executionMode: "oneshot",
            unityVersion: "6000.1.4f1",
            unityEditorPath: "/Applications/Unity/Hub/Editor/6000.1.4f1/Unity.app",
            testPlatform: "playmode",
            buildTarget: "Android",
            testFilter: "Name~Smoke",
            testCategory: "smoke, fast,nightly",
            assemblyName: "MyGame.Tests.EditMode,MyGame.Tests.PlayMode",
            testSettingsPath: "/repo/UnityProject/ProjectSettings/TestSettings.json",
            timeout: 120,
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, service.CapturedCancellationToken);

        var input = Assert.IsType<TestRunCommandInput>(service.CapturedInput);
        Assert.Equal("/repo/UnityProject", input.ProjectPath);
        Assert.Equal("/repo/test.profile.json", input.ProfilePath);
        Assert.Equal("oneshot", input.Mode);
        Assert.Equal("6000.1.4f1", input.UnityVersion);
        Assert.Equal("/Applications/Unity/Hub/Editor/6000.1.4f1/Unity.app", input.UnityEditorPath);
        Assert.Equal("playmode", input.TestPlatform);
        Assert.Equal("Android", input.BuildTarget);
        Assert.Equal("Name~Smoke", input.TestFilter);
        var testCategories = Assert.IsType<string[]>(input.TestCategory);
        var assemblyNames = Assert.IsType<string[]>(input.AssemblyName);
        Assert.Equal(["smoke", "fast", "nightly"], testCategories);
        Assert.Equal(["MyGame.Tests.EditMode", "MyGame.Tests.PlayMode"], assemblyNames);
        Assert.Equal("/repo/UnityProject/ProjectSettings/TestSettings.json", input.TestSettingsPath);
        Assert.Equal(120, input.TimeoutMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SplitCommaSeparatedValues_WithCommaSeparatedValue_ReturnsTrimmedEntries ()
    {
        var values = TestRunCommand.SplitCommaSeparatedValues(
            "MyGame.Tests.EditMode, MyGame.Tests.PlayMode");

        Assert.NotNull(values);
        Assert.Equal(
            ["MyGame.Tests.EditMode", "MyGame.Tests.PlayMode"],
            values);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SplitCommaSeparatedValues_WithNull_ReturnsNull ()
    {
        var values = TestRunCommand.SplitCommaSeparatedValues(null);

        Assert.Null(values);
    }

    private sealed class StubTestRunService : ITestRunService
    {
        private readonly Func<TestRunCommandInput, CancellationToken, ValueTask<TestRunServiceResult>> handler;

        public StubTestRunService (Func<TestRunCommandInput, CancellationToken, ValueTask<TestRunServiceResult>> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public TestRunCommandInput? CapturedInput { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public ValueTask<TestRunServiceResult> Execute (
            TestRunCommandInput input,
            CancellationToken cancellationToken = default)
        {
            CapturedInput = input;
            CapturedCancellationToken = cancellationToken;
            return handler(input, cancellationToken);
        }
    }
}
