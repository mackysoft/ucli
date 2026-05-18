using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
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

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.VerifyAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("verify", "default-success.json"),
            standardOutput,
            CreateVerifyGoldenNormalization());
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
                Digest: "sha256:1111111111111111111111111111111111111111111111111111111111111111"),
            ProfileDigest: "sha256:1111111111111111111111111111111111111111111111111111111111111111",
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
        private readonly Func<VerifyCommandInput, CancellationToken, ValueTask<VerifyExecutionResult>> handler;

        public StubVerifyService (Func<VerifyCommandInput, CancellationToken, ValueTask<VerifyExecutionResult>> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public VerifyCommandInput? CapturedInput { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public ValueTask<VerifyExecutionResult> ExecuteAsync (
            VerifyCommandInput input,
            CancellationToken cancellationToken = default)
        {
            CapturedInput = input;
            CapturedCancellationToken = cancellationToken;
            return handler(input, cancellationToken);
        }
    }
}
