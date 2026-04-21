using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Features.Testing.Run.Common.Contracts;
using MackySoft.Ucli.Features.Testing.Run.UseCases.TestRun;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Testing;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;

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
            testPlatform: "Android",
            testFilter: "Name~Smoke",
            testCategory: "smoke, fast,nightly",
            assemblyName: "MyGame.Tests.EditMode,MyGame.Tests.PlayMode",
            testSettingsPath: "/repo/UnityProject/ProjectSettings/TestSettings.json",
            timeout: 120,
            failFast: true,
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, service.CapturedCancellationToken);

        var input = Assert.IsType<TestRunCommandInput>(service.CapturedInput);
        Assert.Equal("/repo/UnityProject", input.ProjectPath);
        Assert.Equal("/repo/test.profile.json", input.ProfilePath);
        Assert.Equal(UnityExecutionMode.Oneshot, input.Mode);
        Assert.Equal("6000.1.4f1", input.UnityVersion);
        Assert.Equal("/Applications/Unity/Hub/Editor/6000.1.4f1/Unity.app", input.UnityEditorPath);
        Assert.Equal(TestRunPlatform.Player("Android"), input.TestPlatform);
        Assert.Equal("Name~Smoke", input.TestFilter);
        var testCategories = Assert.IsType<string[]>(input.TestCategory);
        var assemblyNames = Assert.IsType<string[]>(input.AssemblyName);
        Assert.Equal(["smoke", "fast", "nightly"], testCategories);
        Assert.Equal(["MyGame.Tests.EditMode", "MyGame.Tests.PlayMode"], assemblyNames);
        Assert.Equal("/repo/UnityProject/ProjectSettings/TestSettings.json", input.TestSettingsPath);
        Assert.Equal(120, input.TimeoutMilliseconds);
        Assert.True(input.FailFast);
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

    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_WhenModeIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubTestRunService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new TestRunCommand(service);

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Run(
            executionMode: "unsupported",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.TestRun,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .IsNull("result")
                .HasString("errorKind", "invalidInput")
                .IsNull("runId")
                .IsNull("artifactsDir")
                .IsNull("summaryJsonPath"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_WhenTestPlatformIsWhitespace_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubTestRunService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new TestRunCommand(service);

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Run(
            testPlatform: " ",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.TestRun,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .IsNull("result")
                .HasString("errorKind", "invalidInput")
                .IsNull("runId")
                .IsNull("artifactsDir")
                .IsNull("summaryJsonPath"));
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