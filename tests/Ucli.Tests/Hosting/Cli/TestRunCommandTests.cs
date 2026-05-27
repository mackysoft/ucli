using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Hosting.Cli.Testing;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class TestRunCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_MapsOptionsToServiceInputAndCancellationToken ()
    {
        var artifactsDir = Path.Combine(Path.GetTempPath(), "ucli-test-run-artifacts");
        var summaryJsonPath = Path.Combine(artifactsDir, "summary.json");
        var service = new StubTestRunService(
            (_, _) => ValueTask.FromResult(TestRunServiceResult.Pass(
                message: "Unity test execution completed.",
                runId: "run-id",
                artifactsDir: artifactsDir,
                summaryJsonPath: summaryJsonPath)));
        var command = new TestRunCommand(service, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.RunAsync(
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

        Assert.Equal((int)CliExitCode.Success, exitCode);
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
        Assert.True(input.AllowEmptyTestRun);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("test-run", "success.json"),
            standardOutput,
            new JsonGoldenFileNormalization().NormalizePathPrefix(artifactsDir, "<artifacts>"));
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
        var command = new TestRunCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.RunAsync(
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
        var command = new TestRunCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.RunAsync(
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

    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_WithJsonFormat_WritesProgressEntriesToStandardErrorAndFinalResultToStandardOutput ()
    {
        var artifactsDir = Path.Combine(Path.GetTempPath(), "ucli-test-run-artifacts");
        var summaryJsonPath = Path.Combine(artifactsDir, "summary.json");
        var service = new StubTestRunService(async (_, progressSink, cancellationToken) =>
        {
            Assert.NotNull(progressSink);
            await progressSink!.OnEntryAsync(
                TestRunProgressEventNames.RunStarted,
                new TestRunStartedEntry(
                    "run-id",
                    "editmode",
                    "Name~Smoke",
                    ["MyGame.Tests"],
                    ["smoke"]),
                cancellationToken);
            await progressSink.OnEntryAsync(
                TestRunProgressEventNames.CaseStarted,
                new TestCaseStartedEntry(
                    "run-id",
                    "test-id",
                    "SmokeTest.Passes",
                    "MyGame.Tests",
                    "editmode",
                    ["smoke"]),
                cancellationToken);
            await progressSink.OnEntryAsync(
                TestRunProgressEventNames.CaseFinished,
                new TestCaseFinishedEntry(
                    "run-id",
                    "test-id",
                    "SmokeTest.Passes",
                    "MyGame.Tests",
                    "editmode",
                    ["smoke"],
                    "pass",
                    42,
                    null,
                    null),
                cancellationToken);
            await progressSink.OnEntryAsync(
                TestRunProgressEventNames.RunDiagnostic,
                new TestRunDiagnosticEntry(
                    "run-id",
                    "TEST_PROGRESS_STUB",
                    "stub progress",
                    "info"),
                cancellationToken);
            return TestRunServiceResult.Pass(
                message: "Unity test execution completed.",
                runId: "run-id",
                artifactsDir: artifactsDir,
                summaryJsonPath: summaryJsonPath);
        });
        var command = new TestRunCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.RunAsync(
            format: "json",
            testFilter: "Name~Smoke",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.TestRun,
            IpcProtocol.StatusOk,
            (int)CliExitCode.Success);
        var lines = standardError.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, lines.Length);
        using var startedEntry = JsonDocument.Parse(lines[0]);
        using var caseStartedEntry = JsonDocument.Parse(lines[1]);
        using var finishedEntry = JsonDocument.Parse(lines[2]);
        using var diagnosticEntry = JsonDocument.Parse(lines[3]);
        AssertTestStreamEnvelope(startedEntry.RootElement, sequence: 1, TestRunProgressEventNames.RunStarted);
        AssertTestStreamEnvelope(caseStartedEntry.RootElement, sequence: 2, TestRunProgressEventNames.CaseStarted);
        AssertTestStreamEnvelope(finishedEntry.RootElement, sequence: 3, TestRunProgressEventNames.CaseFinished);
        AssertTestStreamEnvelope(diagnosticEntry.RootElement, sequence: 4, TestRunProgressEventNames.RunDiagnostic);
        Assert.Equal("run-id", startedEntry.RootElement.GetProperty("payload").GetProperty("runId").GetString());
        Assert.Equal("SmokeTest.Passes", caseStartedEntry.RootElement.GetProperty("payload").GetProperty("testName").GetString());
        Assert.Equal("SmokeTest.Passes", finishedEntry.RootElement.GetProperty("payload").GetProperty("testName").GetString());
        Assert.Equal("TEST_PROGRESS_STUB", diagnosticEntry.RootElement.GetProperty("payload").GetProperty("code").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_WithJsonFormatAndServiceError_WritesProgressEntryThenFinalErrorResult ()
    {
        var artifactsDir = Path.Combine(Path.GetTempPath(), "ucli-test-run-artifacts");
        var summaryJsonPath = Path.Combine(artifactsDir, "summary.json");
        var service = new StubTestRunService(async (_, progressSink, cancellationToken) =>
        {
            Assert.NotNull(progressSink);
            await progressSink!.OnEntryAsync(
                TestRunProgressEventNames.RunStarted,
                new TestRunStartedEntry(
                    "run-id",
                    "editmode",
                    null,
                    ["MyGame.Tests"],
                    []),
                cancellationToken);
            return TestRunServiceResult.InfraError(
                "Unity test infrastructure failed.",
                TestRunErrorCodes.UnityTestExecutionFailed,
                runId: "run-id",
                artifactsDir: artifactsDir,
                summaryJsonPath: summaryJsonPath);
        });
        var command = new TestRunCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.RunAsync(
            format: "json",
            cancellationToken: CancellationToken.None));

        Assert.Equal(2, exitCode);
        var lines = standardError.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        using var entryJson = JsonDocument.Parse(lines[0]);
        AssertTestStreamEnvelope(entryJson.RootElement, sequence: 1, TestRunProgressEventNames.RunStarted);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.TestRun,
            IpcProtocol.StatusError,
            2);
        CommandResultAssert.HasSingleError(outputJson.RootElement, TestRunErrorCodes.UnityTestExecutionFailed);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .IsNull("result")
                .HasString("errorKind", "infraError")
                .HasString("runId", "run-id")
                .HasString("artifactsDir", artifactsDir)
                .HasString("summaryJsonPath", summaryJsonPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_WithDefaultFormat_WritesTextProgressToStandardError ()
    {
        var service = new StubTestRunService(async (_, progressSink, cancellationToken) =>
        {
            Assert.NotNull(progressSink);
            await progressSink!.OnEntryAsync(
                TestRunProgressEventNames.RunDiagnostic,
                new TestRunDiagnosticEntry(
                    "run-id",
                    "TEST_PROGRESS_STUB",
                    "line 1\nline 2",
                    "info"),
                cancellationToken);
            return TestRunServiceResult.Pass(
                message: "Unity test execution completed.",
                runId: "run-id",
                artifactsDir: "/tmp/ucli-test-run-artifacts",
                summaryJsonPath: "/tmp/ucli-test-run-artifacts/summary.json");
        });
        var command = new TestRunCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.RunAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.TestRun,
            IpcProtocol.StatusOk,
            (int)CliExitCode.Success);
        Assert.Equal(
            "info TEST_PROGRESS_STUB: line 1\\nline 2" + Environment.NewLine,
            standardError);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_WithTextFormat_WritesDotnetStyleCompletedCasesToStandardError ()
    {
        var service = new StubTestRunService(async (_, progressSink, cancellationToken) =>
        {
            Assert.NotNull(progressSink);
            await progressSink!.OnEntryAsync(
                TestRunProgressEventNames.RunStarted,
                new TestRunStartedEntry(
                    "run-id",
                    "editmode",
                    null,
                    ["MyGame.Tests"],
                    []),
                cancellationToken);
            await progressSink.OnEntryAsync(
                TestRunProgressEventNames.CaseStarted,
                new TestCaseStartedEntry(
                    "run-id",
                    "test-id-pass",
                    "SmokeTest.Passes",
                    "MyGame.Tests",
                    "editmode",
                    ["smoke"]),
                cancellationToken);
            await progressSink.OnEntryAsync(
                TestRunProgressEventNames.CaseFinished,
                new TestCaseFinishedEntry(
                    "run-id",
                    "test-id-pass",
                    "SmokeTest.Passes",
                    "MyGame.Tests",
                    "editmode",
                    ["smoke"],
                    "pass",
                    42,
                    null,
                    null),
                cancellationToken);
            await progressSink.OnEntryAsync(
                TestRunProgressEventNames.CaseFinished,
                new TestCaseFinishedEntry(
                    "run-id",
                    "test-id-fail",
                    "SmokeTest.Fails",
                    "MyGame.Tests",
                    "editmode",
                    ["smoke"],
                    "fail",
                    13,
                    "assertion failed",
                    "at SmokeTest.Fails()"),
                cancellationToken);
            return TestRunServiceResult.Fail(
                message: "Unity test execution completed with failures.",
                runId: "run-id",
                artifactsDir: "/tmp/ucli-test-run-artifacts",
                summaryJsonPath: "/tmp/ucli-test-run-artifacts/summary.json");
        });
        var command = new TestRunCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.RunAsync(
            format: "text",
            cancellationToken: CancellationToken.None));

        Assert.Equal(1, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.TestRun,
            IpcProtocol.StatusOk,
            1);
        Assert.Equal(
            "Passed SmokeTest.Passes [42 ms]" + Environment.NewLine
                + "Failed SmokeTest.Fails [13 ms]" + Environment.NewLine,
            standardError);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_WithUnsupportedFormat_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubTestRunService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new TestRunCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.RunAsync(
            format: "yaml",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Equal(string.Empty, standardError);
        Assert.Null(service.CapturedInput);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.TestRun,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_WhenCancellationIsRequested_WritesTestRunCommandResult ()
    {
        var service = new StubTestRunService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new TestRunCommand(service, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.RunAsync(
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal((int)CliExitCode.ToolError, exitCode);
        Assert.Equal(string.Empty, standardError);
        Assert.Null(service.CapturedInput);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.TestRun,
            IpcProtocol.StatusError,
            (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, ExecutionErrorCodes.Canceled);
    }

    private static void AssertTestStreamEnvelope (
        JsonElement root,
        int sequence,
        string eventName)
    {
        Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Assert.Equal(UcliCommandNames.TestRun, root.GetProperty("command").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("streamId").GetString()));
        Assert.Equal(sequence, root.GetProperty("sequence").GetInt32());
        Assert.True(DateTimeOffset.TryParse(root.GetProperty("timestamp").GetString(), out _));
        Assert.Equal(eventName, root.GetProperty("event").GetString());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("payload").ValueKind);
    }

    private sealed class StubTestRunService : ITestRunService
    {
        private readonly Func<TestRunCommandInput, ICommandProgressSink?, CancellationToken, ValueTask<TestRunServiceResult>> handler;

        public StubTestRunService (Func<TestRunCommandInput, CancellationToken, ValueTask<TestRunServiceResult>> handler)
            : this((input, _, cancellationToken) => handler(input, cancellationToken))
        {
        }

        public StubTestRunService (Func<TestRunCommandInput, ICommandProgressSink?, CancellationToken, ValueTask<TestRunServiceResult>> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public TestRunCommandInput? CapturedInput { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public ValueTask<TestRunServiceResult> ExecuteAsync (
            TestRunCommandInput input,
            ICommandProgressSink? progressSink = null,
            CancellationToken cancellationToken = default)
        {
            CapturedInput = input;
            CapturedCancellationToken = cancellationToken;
            return handler(input, progressSink, cancellationToken);
        }
    }
}
