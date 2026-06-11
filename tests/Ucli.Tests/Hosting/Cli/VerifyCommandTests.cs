using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class VerifyCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Verify_MapsOptionsToServiceInputAndCancellationToken ()
    {
        var service = new StubVerifyService((_, _) => ValueTask.FromResult(VerifyExecutionResult.Success(CreateOutput())));
        var command = new VerifyCommand(service, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        await StandardOutputCapture.ExecuteAsync(() => command.VerifyAsync(
            profile: null,
            profilePath: "profiles/verify.json",
            from: "artifacts/call-result.json",
            projectPath: "/repo/UnityProject",
            mode: "daemon",
            timeout: "1234",
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, service.CapturedCancellationToken);
        var input = Assert.IsType<VerifyCommandInput>(service.CapturedInput);
        Assert.Null(input.Profile);
        Assert.Equal("profiles/verify.json", input.ProfilePath);
        Assert.Equal("artifacts/call-result.json", input.FromPath);
        Assert.Equal("/repo/UnityProject", input.ProjectPath);
        Assert.Equal(UnityExecutionMode.Daemon, input.Mode);
        Assert.Equal(1234, input.TimeoutMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Verify_WhenModeIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubVerifyService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new VerifyCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.VerifyAsync(
            mode: "unknown",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Verify,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Verify_WhenTimeoutIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubVerifyService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new VerifyCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.VerifyAsync(
            timeout: "not-an-int",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Verify,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Theory]
    [InlineData("--mode", "unknown")]
    [InlineData("--timeout", "not-an-int")]
    [InlineData("--format", "yaml")]
    [Trait("Size", "Medium")]
    public async Task Verify_ProcessWithInvalidOption_ReturnsJsonInvalidArgument (
        string option,
        string value)
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Verify,
            option,
            value);

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.True(string.IsNullOrEmpty(result.StdErr), result.StdErr);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Verify,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Verify_WithProfilePathCamelCaseAlias_IsAcceptedByParser ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Verify_WithProfilePathCamelCaseAlias_IsAcceptedByParser));
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var profilePath = Path.Combine(unityProjectPath, "verify-profile.json");
        await File.WriteAllTextAsync(
            profilePath,
            "{\"schemaVersion\":1,\"steps\":[]}",
            CancellationToken.None);

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Verify,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.Profile,
            "built-in:default",
            UcliContractConstants.CliOption.ProfilePath,
            profilePath);

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.True(string.IsNullOrEmpty(result.StdErr), result.StdErr);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Verify,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Verify_WithHelpOutput_IncludesProfilePathCamelCaseOption ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Verify,
            "--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains(UcliContractConstants.CliOption.ProfilePath, result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Verify_WithPassOutput_MatchesGolden ()
    {
        var service = new StubVerifyService((_, _) => ValueTask.FromResult(VerifyExecutionResult.Success(CreateOutput())));
        var command = new VerifyCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.VerifyAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Equal(string.Empty, standardError);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("verify", "default-success.json"),
            standardOutput,
            CreateVerifyGoldenNormalization());
    }

    [Theory]
    [InlineData("text")]
    [InlineData("json")]
    [Trait("Size", "Small")]
    public async Task Verify_WithSupportedFormat_WritesOnlyFinalCommandResult (
        string format)
    {
        var service = new StubVerifyService((_, _) => ValueTask.FromResult(VerifyExecutionResult.Success(CreateOutput())));
        var command = new VerifyCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.VerifyAsync(
            format: format,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Equal(string.Empty, standardError);
        Assert.NotNull(service.CapturedInput);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("verify", "default-success.json"),
            standardOutput,
            CreateVerifyGoldenNormalization());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Verify_WithJsonFormat_WritesProgressEntriesToStandardErrorAndFinalResultToStandardOutput ()
    {
        var service = new StubVerifyService(async (_, progressSink, cancellationToken) =>
        {
            Assert.NotNull(progressSink);
            await progressSink!.OnEntryAsync(
                    VerifyProgressEventNames.StepStarted,
                    new VerifyStepProgressEntry(
                        VerifyStepKindValues.Ready,
                        Required: true,
                        Effects: [],
                        SkipReason: null),
                    cancellationToken)
                .ConfigureAwait(false);
            await progressSink.OnEntryAsync(
                    VerifyProgressEventNames.StepCompleted,
                    new VerifyStepProgressEntry(
                        VerifyStepKindValues.Ready,
                        Required: true,
                        Effects: [],
                        SkipReason: null),
                    cancellationToken)
                .ConfigureAwait(false);
            return VerifyExecutionResult.Success(CreateOutput());
        });
        var command = new VerifyCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.VerifyAsync(
            format: "json",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Verify,
            IpcProtocol.StatusOk,
            (int)CliExitCode.Success);
        var lines = standardError.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        using var startedEntry = JsonDocument.Parse(lines[0]);
        using var completedEntry = JsonDocument.Parse(lines[1]);
        AssertVerifyStreamEnvelope(startedEntry.RootElement, sequence: 1, VerifyProgressEventNames.StepStarted);
        AssertVerifyStreamEnvelope(completedEntry.RootElement, sequence: 2, VerifyProgressEventNames.StepCompleted);
        Assert.Equal(VerifyStepKindValues.Ready, startedEntry.RootElement.GetProperty("payload").GetProperty("kind").GetString());
        Assert.True(startedEntry.RootElement.GetProperty("payload").GetProperty("required").GetBoolean());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Verify_WithDefaultFormat_WritesTextProgressToStandardError ()
    {
        var service = new StubVerifyService(async (_, progressSink, cancellationToken) =>
        {
            Assert.NotNull(progressSink);
            await progressSink!.OnEntryAsync(
                    VerifyProgressEventNames.StepStarted,
                    new VerifyStepProgressEntry(VerifyStepKindValues.Ready, Required: true, Effects: [], SkipReason: null),
                    cancellationToken)
                .ConfigureAwait(false);
            await progressSink.OnEntryAsync(
                    VerifyProgressEventNames.StepCompleted,
                    new VerifyStepProgressEntry(VerifyStepKindValues.Ready, Required: true, Effects: [], SkipReason: null),
                    cancellationToken)
                .ConfigureAwait(false);
            await progressSink.OnEntryAsync(
                    VerifyProgressEventNames.StepSkipped,
                    new VerifyStepProgressEntry(VerifyStepKindValues.PostRead, Required: false, Effects: [], SkipReason: VerifyStepSkipReasons.PostReadNotNeeded),
                    cancellationToken)
                .ConfigureAwait(false);
            await progressSink.OnEntryAsync(
                    VerifyProgressEventNames.Diagnostic,
                    new VerifyDiagnosticEntry("VERIFY_STUB", "stub diagnostic", "error", VerifyStepKindValues.Compile),
                    cancellationToken)
                .ConfigureAwait(false);
            return VerifyExecutionResult.Success(CreateOutput());
        });
        var command = new VerifyCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.VerifyAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Verify,
            IpcProtocol.StatusOk,
            (int)CliExitCode.Success);
        Assert.Equal(
            "verify ready required=true started" + Environment.NewLine
                + "verify ready required=true completed" + Environment.NewLine
                + "verify postRead required=false skipped" + Environment.NewLine
                + "verify diagnostic step=compile error VERIFY_STUB: stub diagnostic" + Environment.NewLine,
            standardError);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Verify_WhenFormatIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubVerifyService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new VerifyCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.VerifyAsync(
            format: "yaml",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Equal(string.Empty, standardError);
        Assert.Null(service.CapturedInput);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Verify,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Theory]
    [InlineData(VerifyVerdictValues.Fail)]
    [InlineData(VerifyVerdictValues.Incomplete)]
    [Trait("Size", "Small")]
    public async Task Verify_WithNonPassVerdict_ReturnsOkEnvelopeWithFailureExitCode (string verdict)
    {
        var service = new StubVerifyService((_, _) => ValueTask.FromResult(VerifyExecutionResult.Success(CreateOutput(verdict))));
        var command = new VerifyCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.VerifyAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal(1, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Verify,
            IpcProtocol.StatusOk,
            1);
        Assert.Equal(verdict, outputJson.RootElement.GetProperty("payload").GetProperty("verdict").GetString());
    }

    private static void AssertVerifyStreamEnvelope (
        JsonElement root,
        int sequence,
        string eventName)
    {
        Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Assert.Equal(UcliCommandNames.Verify, root.GetProperty("command").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("streamId").GetString()));
        Assert.Equal(sequence, root.GetProperty("sequence").GetInt32());
        Assert.True(DateTimeOffset.TryParse(root.GetProperty("timestamp").GetString(), out _));
        Assert.Equal(eventName, root.GetProperty("event").GetString());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("payload").ValueKind);
    }

    private static VerifyExecutionOutput CreateOutput (
        string verdict = VerifyVerdictValues.Pass)
    {
        var compileClaimStatus = string.Equals(verdict, VerifyVerdictValues.Pass, StringComparison.Ordinal)
            ? VerifyClaimStatusValues.Passed
            : VerifyClaimStatusValues.Failed;
        return new VerifyExecutionOutput(
            Verdict: verdict,
            Project: new ProjectIdentityInfo(
                ProjectPath: "<projectPath>",
                ProjectFingerprint: "<projectFingerprint>",
                UnityVersion: "<unityVersion>"),
            Verifiers:
            [
                new VerifyVerifierOutput(
                    Id: "ready.lifecycle",
                    Kind: VerifyStepKindValues.Ready,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: ["UNITY_READY_EXECUTION"],
                    Effects: []),
                new VerifyVerifierOutput(
                    Id: "compile",
                    Kind: VerifyStepKindValues.Compile,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: ["UNITY_COMPILE_NO_ERRORS"],
                    Effects: VerifyEffectValues.Compile)
                {
                    ReportRef = "compile.summary",
                },
            ],
            Claims:
            [
                new VerifyClaimOutput(
                    Id: "UNITY_READY_EXECUTION",
                    Status: VerifyClaimStatusValues.Passed,
                    Coverage: VerifyCoverageValues.Full,
                    Required: true,
                    VerifierRef: "ready.lifecycle",
                    Statement: "Unity is ready for execution.",
                    Subject: new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["target"] = "execution",
                    },
                    Evidence: [],
                    ResidualRisks: [])
                {
                    Validity = new ReadyClaimValidityOutput(
                        Kind: "probeOnly",
                        GuaranteesReusableSession: false),
                },
                new VerifyClaimOutput(
                    Id: "UNITY_COMPILE_NO_ERRORS",
                    Status: compileClaimStatus,
                    Coverage: VerifyCoverageValues.Full,
                    Required: true,
                    VerifierRef: "compile",
                    Statement: "Unity script compilation has no errors.",
                    Subject: new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["kind"] = "unityCompile",
                    },
                    Evidence:
                    [
                        new VerifyEvidenceOutput("compileSummary")
                        {
                            EvidenceRef = "compile.summary",
                        },
                    ],
                    ResidualRisks: []),
            ],
            Reports: new Dictionary<string, VerifyReportOutput>(StringComparer.Ordinal)
            {
                ["compile.summary"] = new VerifyReportOutput("compileSummary")
                {
                    Path = ".ucli/local/compile/run-1/summary.json",
                },
            },
            ResidualRisks: [],
            Profile: new VerifyProfileOutput(
                Source: VerifyProfileSourceValues.BuiltIn,
                Name: "built-in:default",
                Path: null,
                Digest: "1111111111111111111111111111111111111111111111111111111111111111"),
            TimeoutMilliseconds: 120000);
    }

    private static JsonGoldenFileNormalization CreateVerifyGoldenNormalization ()
    {
        return new JsonGoldenFileNormalization()
            .NormalizeStringPropertyValue("projectPath", "<projectPath>")
            .NormalizeStringPropertyValue("projectFingerprint", "<projectFingerprint>")
            .NormalizeStringPropertyValue("unityVersion", "<unityVersion>");
    }

    private sealed class StubVerifyService : IVerifyService
    {
        private readonly Func<VerifyCommandInput, ICommandProgressSink?, CancellationToken, ValueTask<VerifyExecutionResult>> handler;

        public StubVerifyService (Func<VerifyCommandInput, CancellationToken, ValueTask<VerifyExecutionResult>> handler)
            : this((input, _, cancellationToken) => handler(input, cancellationToken))
        {
        }

        public StubVerifyService (Func<VerifyCommandInput, ICommandProgressSink?, CancellationToken, ValueTask<VerifyExecutionResult>> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public VerifyCommandInput? CapturedInput { get; private set; }

        public ICommandProgressSink? CapturedProgressSink { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public ValueTask<VerifyExecutionResult> ExecuteAsync (
            VerifyCommandInput input,
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
