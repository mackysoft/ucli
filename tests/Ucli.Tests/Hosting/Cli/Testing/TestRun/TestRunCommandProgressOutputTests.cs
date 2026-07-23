using System.Text.Json;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Hosting.Cli.Testing;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class TestRunCommandProgressOutputTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Run_WhenServiceReturnsPass_MatchesSuccessGolden ()
    {
        var artifactsDir = Path.Combine(Path.GetTempPath(), "ucli-test-run-artifacts");
        var summaryJsonPath = Path.Combine(artifactsDir, "summary.json");
        var service = new RecordingTestRunService(
            (_, _, _) => ValueTask.FromResult(TestRunServiceResult.Pass(
                message: "Unity test execution completed.",
                runId: RunIdTestValues.Test,
                artifactsDir: artifactsDir,
                summaryJsonPath: summaryJsonPath)));
        var command = new TestRunCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var result = await CommandResultCapture.ExecuteAsync(() => command.RunAsync(cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("test-run", "success.json"),
            result.StdOut,
            new JsonGoldenFileNormalization().NormalizePathPrefix(artifactsDir, "<artifacts>"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_WithJsonFormat_WritesProgressEntriesToStandardErrorAndFinalResultToStandardOutput ()
    {
        var artifactsDir = Path.Combine(Path.GetTempPath(), "ucli-test-run-artifacts");
        var summaryJsonPath = Path.Combine(artifactsDir, "summary.json");
        var service = new RecordingTestRunService(async (_, progressSink, cancellationToken) =>
        {
            Assert.NotNull(progressSink);
            await progressSink!.OnEntryAsync(
                TestRunProgressEventNames.RunStarted,
                new TestRunStartedEntry(
                    RunIdTestValues.Test,
                    "editmode",
                    "Name~Smoke",
                    ["MyGame.Tests"],
                    ["smoke"]),
                cancellationToken);
            await progressSink.OnEntryAsync(
                TestRunProgressEventNames.CaseStarted,
                new TestCaseStartedEntry(
                    RunIdTestValues.Test,
                    "test-id",
                    "SmokeTest.Passes",
                    "MyGame.Tests",
                    "editmode",
                    ["smoke"]),
                cancellationToken);
            await progressSink.OnEntryAsync(
                TestRunProgressEventNames.CaseFinished,
                new TestCaseFinishedEntry(
                    RunIdTestValues.Test,
                    "test-id",
                    "SmokeTest.Passes",
                    "MyGame.Tests",
                    "editmode",
                    ["smoke"],
                    TestCaseResult.Pass,
                    42,
                    null,
                    null),
                cancellationToken);
            await progressSink.OnEntryAsync(
                TestRunProgressEventNames.RunDiagnostic,
                new TestRunDiagnosticEntry(
                    RunIdTestValues.Test,
                    new UcliCode("TEST_PROGRESS_STUB"),
                    "stub progress",
                    UcliDiagnosticSeverity.Info),
                cancellationToken);
            return TestRunServiceResult.Pass(
                message: "Unity test execution completed.",
                runId: RunIdTestValues.Test,
                artifactsDir: artifactsDir,
                summaryJsonPath: summaryJsonPath);
        });
        var command = new TestRunCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.RunAsync(
            format: "json",
            testFilter: "Name~Smoke",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.TestRun);
        var lines = result.StdErr.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, lines.Length);
        using var startedEntry = JsonDocument.Parse(lines[0]);
        using var caseStartedEntry = JsonDocument.Parse(lines[1]);
        using var finishedEntry = JsonDocument.Parse(lines[2]);
        using var diagnosticEntry = JsonDocument.Parse(lines[3]);
        AssertTestStreamEnvelope(startedEntry.RootElement, sequence: 1, TestRunProgressEventNames.RunStarted);
        AssertTestStreamEnvelope(caseStartedEntry.RootElement, sequence: 2, TestRunProgressEventNames.CaseStarted);
        AssertTestStreamEnvelope(finishedEntry.RootElement, sequence: 3, TestRunProgressEventNames.CaseFinished);
        AssertTestStreamEnvelope(diagnosticEntry.RootElement, sequence: 4, TestRunProgressEventNames.RunDiagnostic);
        Assert.Equal(RunIdTestValues.TestText, startedEntry.RootElement.GetProperty("payload").GetProperty("runId").GetString());
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
        var service = new RecordingTestRunService(async (_, progressSink, cancellationToken) =>
        {
            Assert.NotNull(progressSink);
            await progressSink!.OnEntryAsync(
                TestRunProgressEventNames.RunStarted,
                new TestRunStartedEntry(
                    RunIdTestValues.Test,
                    "editmode",
                    null,
                    ["MyGame.Tests"],
                    []),
                cancellationToken);
            return TestRunServiceResult.InfraError(
                "Unity test infrastructure failed.",
                TestRunErrorCodes.UnityTestExecutionFailed,
                runId: RunIdTestValues.Test,
                artifactsDir: artifactsDir,
                summaryJsonPath: summaryJsonPath);
        });
        var command = new TestRunCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.RunAsync(
            format: "json",
            cancellationToken: CancellationToken.None));

        Assert.Equal(2, result.ExitCode);
        var lines = result.StdErr.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        using var entryJson = JsonDocument.Parse(lines[0]);
        AssertTestStreamEnvelope(entryJson.RootElement, sequence: 1, TestRunProgressEventNames.RunStarted);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.TestRun,
            TextVocabulary.GetText(CommandResultStatus.Error),
            2);
        CommandResultAssert.HasSingleError(outputJson.RootElement, TestRunErrorCodes.UnityTestExecutionFailed);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .IsNull("result")
                .HasString("errorKind", "infraError")
                .HasString("runId", RunIdTestValues.TestText)
                .HasString("artifactsDir", artifactsDir)
                .HasString("summaryJsonPath", summaryJsonPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_WithDefaultFormat_WritesTextProgressToStandardError ()
    {
        var service = new RecordingTestRunService(async (_, progressSink, cancellationToken) =>
        {
            Assert.NotNull(progressSink);
            await progressSink!.OnEntryAsync(
                TestRunProgressEventNames.RunDiagnostic,
                new TestRunDiagnosticEntry(
                    RunIdTestValues.Test,
                    new UcliCode("TEST_PROGRESS_STUB"),
                    "line 1\nline 2",
                    UcliDiagnosticSeverity.Info),
                cancellationToken);
            return TestRunServiceResult.Pass(
                message: "Unity test execution completed.",
                runId: RunIdTestValues.Test,
                artifactsDir: "/tmp/ucli-test-run-artifacts",
                summaryJsonPath: "/tmp/ucli-test-run-artifacts/summary.json");
        });
        var command = new TestRunCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.RunAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.TestRun);
        Assert.Equal(
            "info TEST_PROGRESS_STUB: line 1\\nline 2" + Environment.NewLine,
            result.StdErr);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_WithTextFormat_WritesDotnetStyleCompletedCasesToStandardError ()
    {
        var service = new RecordingTestRunService(async (_, progressSink, cancellationToken) =>
        {
            Assert.NotNull(progressSink);
            await progressSink!.OnEntryAsync(
                TestRunProgressEventNames.RunStarted,
                new TestRunStartedEntry(
                    RunIdTestValues.Test,
                    "editmode",
                    null,
                    ["MyGame.Tests"],
                    []),
                cancellationToken);
            await progressSink.OnEntryAsync(
                TestRunProgressEventNames.CaseStarted,
                new TestCaseStartedEntry(
                    RunIdTestValues.Test,
                    "test-id-pass",
                    "SmokeTest.Passes",
                    "MyGame.Tests",
                    "editmode",
                    ["smoke"]),
                cancellationToken);
            await progressSink.OnEntryAsync(
                TestRunProgressEventNames.CaseFinished,
                new TestCaseFinishedEntry(
                    RunIdTestValues.Test,
                    "test-id-pass",
                    "SmokeTest.Passes",
                    "MyGame.Tests",
                    "editmode",
                    ["smoke"],
                    TestCaseResult.Pass,
                    42,
                    null,
                    null),
                cancellationToken);
            await progressSink.OnEntryAsync(
                TestRunProgressEventNames.CaseFinished,
                new TestCaseFinishedEntry(
                    RunIdTestValues.Test,
                    "test-id-fail",
                    "SmokeTest.Fails",
                    "MyGame.Tests",
                    "editmode",
                    ["smoke"],
                    TestCaseResult.Fail,
                    13,
                    "assertion failed",
                    "at SmokeTest.Fails()"),
                cancellationToken);
            return TestRunServiceResult.Fail(
                message: "Unity test execution completed with failures.",
                runId: RunIdTestValues.Test,
                artifactsDir: "/tmp/ucli-test-run-artifacts",
                summaryJsonPath: "/tmp/ucli-test-run-artifacts/summary.json");
        });
        var command = new TestRunCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.RunAsync(
            format: "text",
            cancellationToken: CancellationToken.None));

        Assert.Equal(1, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.TestRun,
            TextVocabulary.GetText(CommandResultStatus.Ok),
            1);
        Assert.Equal(
            "Passed SmokeTest.Passes [42 ms]" + Environment.NewLine
                + "Failed SmokeTest.Fails [13 ms]" + Environment.NewLine,
            result.StdErr);
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
}
