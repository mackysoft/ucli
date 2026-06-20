using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Vocabulary;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class CompileCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Compile_MapsOptionsToServiceInputAndCancellationToken ()
    {
        var service = new StubCompileService((_, _, _) => ValueTask.FromResult(CompileExecutionResult.Success(CreateOutput())));
        var command = new CompileCommand(service, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        await StandardOutputCapture.ExecuteAsync(() => command.CompileAsync(
            projectPath: "/repo/UnityProject",
            mode: "daemon",
            timeout: "1234",
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, service.CapturedCancellationToken);
        var input = Assert.IsType<CompileCommandInput>(service.CapturedInput);
        Assert.Equal("/repo/UnityProject", input.ProjectPath);
        Assert.Equal(UnityExecutionMode.Daemon, input.Mode);
        Assert.Equal(1234, input.TimeoutMilliseconds);
        Assert.NotNull(service.CapturedProgressSink);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Compile_WhenModeIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubCompileService((_, _, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new CompileCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.CompileAsync(
            mode: "unknown",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Compile,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Compile_WithPassOutput_MatchesGolden ()
    {
        var service = new StubCompileService((_, _, _) => ValueTask.FromResult(CompileExecutionResult.Success(CreateOutput())));
        var command = new CompileCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.CompileAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Equal(string.Empty, standardError);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("compile", "pass-no-reload.json"),
            standardOutput,
            CreateCompileGoldenNormalization());
    }

    [Theory]
    [InlineData("text")]
    [InlineData("json")]
    [Trait("Size", "Small")]
    public async Task Compile_WithSupportedFormat_WritesOnlyFinalCommandResult (
        string format)
    {
        var service = new StubCompileService((_, _, _) => ValueTask.FromResult(CompileExecutionResult.Success(CreateOutput())));
        var command = new CompileCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.CompileAsync(
            format: format,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Equal(string.Empty, standardError);
        Assert.NotNull(service.CapturedInput);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("compile", "pass-no-reload.json"),
            standardOutput,
            CreateCompileGoldenNormalization());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Compile_WithJsonFormat_WritesProgressEntryToStandardError ()
    {
        var service = new StubCompileService(async (_, progressSink, cancellationToken) =>
        {
            Assert.NotNull(progressSink);
            await progressSink!.OnEntryAsync(
                CompileProgressEventNames.Completed,
                CreateCompletedEntry(),
                cancellationToken);
            return CompileExecutionResult.Success(CreateOutput());
        });
        var command = new CompileCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.CompileAsync(
            format: "json",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Compile,
            IpcProtocol.StatusOk,
            (int)CliExitCode.Success);
        var line = Assert.Single(standardError.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        using var entryJson = JsonDocument.Parse(line);
        AssertCompileStreamEnvelope(entryJson.RootElement, sequence: 1, CompileProgressEventNames.Completed);
        Assert.Equal("pass", entryJson.RootElement.GetProperty("payload").GetProperty("verdict").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Compile_WithDefaultFormat_WritesTextProgressToStandardError ()
    {
        var service = new StubCompileService(async (_, progressSink, cancellationToken) =>
        {
            Assert.NotNull(progressSink);
            await progressSink!.OnEntryAsync(
                CompileProgressEventNames.Completed,
                CreateCompletedEntry(),
                cancellationToken);
            return CompileExecutionResult.Success(CreateOutput());
        });
        var command = new CompileCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.CompileAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Compile,
            IpcProtocol.StatusOk,
            (int)CliExitCode.Success);
        Assert.Equal(
            "compile runId=run-1 verdict=pass errorCount=0 warningCount=0 completed" + Environment.NewLine,
            standardError);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Compile_WhenFormatIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubCompileService((_, _, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new CompileCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.CompileAsync(
            format: "yaml",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Equal(string.Empty, standardError);
        Assert.Null(service.CapturedInput);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Compile,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Compile_ProcessWithInvalidFormat_ReturnsInvalidArgument ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Compile,
            "--format",
            "yaml");

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.True(string.IsNullOrEmpty(result.StdErr), result.StdErr);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Compile,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
        Assert.DoesNotContain("Argument '--format' is not recognized.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Compile_WithCompileErrorOutput_ReturnsOkEnvelopeWithFailureExitCodeAndMatchesGolden ()
    {
        var service = new StubCompileService((_, _, _) => ValueTask.FromResult(CompileExecutionResult.Success(CreateOutput(errorCount: 1))));
        var command = new CompileCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.CompileAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal(1, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Compile,
            IpcProtocol.StatusOk,
            1);
        Assert.Equal(CompileVerdictValues.Fail, outputJson.RootElement.GetProperty("payload").GetProperty("verdict").GetString());

        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("compile", "compile-error.json"),
            standardOutput,
            CreateCompileGoldenNormalization());
    }

    private static JsonGoldenFileNormalization CreateCompileGoldenNormalization ()
    {
        return new JsonGoldenFileNormalization()
            .NormalizeStringPropertyValue("projectPath", "<projectPath>")
            .NormalizeStringPropertyValue("projectFingerprint", "<projectFingerprint>");
    }

    private static void AssertCompileStreamEnvelope (
        JsonElement root,
        int sequence,
        string eventName)
    {
        Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Assert.Equal(UcliCommandNames.Compile, root.GetProperty("command").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("streamId").GetString()));
        Assert.Equal(sequence, root.GetProperty("sequence").GetInt32());
        Assert.True(DateTimeOffset.TryParse(root.GetProperty("timestamp").GetString(), out _));
        Assert.Equal(eventName, root.GetProperty("event").GetString());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("payload").ValueKind);
    }

    private static CompileCompletedEntry CreateCompletedEntry ()
    {
        return new CompileCompletedEntry(
            RunId: "run-1",
            Verdict: "pass",
            ErrorCount: 0,
            WarningCount: 0,
            SummaryJsonPath: "/tmp/ucli/compile/run-1/summary.json",
            DiagnosticsJsonPath: "/tmp/ucli/compile/run-1/diagnostics.json");
    }

    private static CompileExecutionOutput CreateOutput (int errorCount = 0)
    {
        var compile = CreateCompileOutput(errorCount);
        var compileStatus = errorCount == 0 ? CompileClaimStatusValues.Passed : CompileClaimStatusValues.Failed;
        var lifecycleStatus = errorCount == 0 ? CompileClaimStatusValues.Passed : CompileClaimStatusValues.Failed;
        return new CompileExecutionOutput(
            Verdict: errorCount == 0 ? CompileVerdictValues.Pass : CompileVerdictValues.Fail,
            Project: new ProjectIdentityInfo(
                ProjectPath: "<projectPath>",
                ProjectFingerprint: "<projectFingerprint>",
                UnityVersion: "6000.1.4f1"),
            Verifiers:
            [
                new CompileVerifierOutput(
                    Id: "compile",
                    Kind: "compile",
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: CompileClaimCodes.All.Select(static code => code.Value).ToArray(),
                    Effects: CompileEffectValues.All,
                    ReportRef: "compile.summary"),
            ],
            Claims:
            [
                CreateClaim(
                    CompileClaimCodes.UnityCompileNoErrors,
                    compileStatus,
                    "Unity script compilation completed without compiler errors.",
                    "unityCompile",
                    new CompileEvidenceOutput(CompileEffectValues.ScriptCompilation, "compile.diagnostics", compile.ScriptCompilation)),
                CreateClaim(
                    CompileClaimCodes.UnityDomainReloadSettled,
                    CompileClaimStatusValues.Passed,
                    "Unity domain reload reached a settled state after compile observation.",
                    "unityDomainReload",
                    new CompileEvidenceOutput(CompileEffectValues.DomainReload, Data: compile.DomainReload)),
                CreateClaim(
                    CompileClaimCodes.UnityLifecycleReadyAfterCompile,
                    lifecycleStatus,
                    "Unity lifecycle is ready after compile observation.",
                    "unityLifecycle",
                    new CompileEvidenceOutput("lifecycleSnapshot", Data: compile.Lifecycle)),
            ],
            Reports: new Dictionary<string, CompileReportOutput>(StringComparer.Ordinal)
            {
                ["compile.summary"] = new CompileReportOutput("/tmp/ucli/compile/summary.json"),
                ["compile.diagnostics"] = new CompileReportOutput("/tmp/ucli/compile/diagnostics.json"),
            },
            ResidualRisks: [],
            RequestedMode: AssuranceExecutionModeCodec.Auto,
            ResolvedMode: AssuranceExecutionModeCodec.Oneshot,
            SessionKind: AssuranceSessionKindValues.TransientProbe,
            TimeoutMilliseconds: 10000,
            Compile: compile);
    }

    private static CompileClaimOutput CreateClaim (
        string id,
        string status,
        string statement,
        string subjectKind,
        CompileEvidenceOutput evidence)
    {
        return new CompileClaimOutput(
            Id: id,
            Status: status,
            Coverage: CompileCoverageValues.Full,
            Required: true,
            VerifierRef: "compile",
            Statement: statement,
            Subject: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["kind"] = subjectKind,
                ["runId"] = "20260517_000000Z_abcdef12",
            },
            Evidence: [evidence],
            ResidualRisks: []);
    }

    private static CompileOutput CreateCompileOutput (int errorCount)
    {
        var primaryDiagnostic = errorCount == 0
            ? null
            : new CompilePrimaryDiagnosticOutput(
                Kind: "compiler",
                Code: "CS1002",
                File: "Assets/Broken.cs",
                Line: 4,
                Column: 16,
                Message: "; expected");
        var canAcceptExecutionRequests = errorCount == 0;
        return new CompileOutput(
            RunId: "20260517_000000Z_abcdef12",
            Refresh: new CompileRefreshOutput(
                Origin: CompileEffectValues.AssetDatabaseRefresh,
                Requested: true,
                StartedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00Z"),
                CompletedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:02Z"),
                Completed: true),
            ScriptCompilation: new CompileScriptCompilationOutput(
                Started: true,
                Completed: true,
                CompileGenerationBefore: "12",
                CompileGenerationAfter: "14",
                Diagnostics: new CompileDiagnosticsOutput(
                    ErrorCount: errorCount,
                    WarningCount: 0,
                    PrimaryDiagnostic: primaryDiagnostic)),
            DomainReload: new CompileDomainReloadOutput(
                ReloadRequired: false,
                ReloadObserved: false,
                GenerationBefore: "7",
                GenerationAfter: "7",
                Settled: true),
            Lifecycle: new CompileLifecycleOutput(
                ServerVersion: "0.5.0",
                UnityVersion: "6000.1.4f1",
                EditorMode: "batchmode",
                LifecycleState: canAcceptExecutionRequests ? "ready" : "compileFailed",
                BlockingReason: canAcceptExecutionRequests ? null : "compileFailed",
                CompileState: canAcceptExecutionRequests ? "ready" : "failed",
                CompileGeneration: "14",
                DomainReloadGeneration: "7",
                CanAcceptExecutionRequests: canAcceptExecutionRequests,
                ObservedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:03Z"),
                ActionRequired: canAcceptExecutionRequests ? null : "fixCompileErrors",
                PrimaryDiagnostic: primaryDiagnostic));
    }

    private sealed class StubCompileService : ICompileService
    {
        private readonly Func<CompileCommandInput, ICommandProgressSink?, CancellationToken, ValueTask<CompileExecutionResult>> handler;

        public StubCompileService (Func<CompileCommandInput, ICommandProgressSink?, CancellationToken, ValueTask<CompileExecutionResult>> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public CompileCommandInput? CapturedInput { get; private set; }

        public ICommandProgressSink? CapturedProgressSink { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public ValueTask<CompileExecutionResult> ExecuteAsync (
            CompileCommandInput input,
            ICommandProgressSink? progressSink = null,
            CancellationToken cancellationToken = default)
        {
            CapturedInput = input;
            CapturedProgressSink = progressSink;
            CapturedCancellationToken = cancellationToken;
            return handler(input, progressSink, cancellationToken);
        }
    }
}
