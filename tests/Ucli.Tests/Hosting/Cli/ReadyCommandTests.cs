using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Shared.CommandContracts;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class ReadyCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Ready_MapsOptionsToServiceInputAndCancellationToken ()
    {
        var service = new StubReadyService((_, _) => ValueTask.FromResult(ReadyExecutionResult.Success(CreateOutput())));
        var command = new ReadyCommand(service, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        await StandardOutputCapture.ExecuteAsync(() => command.ReadyAsync(
            @for: "execution",
            projectPath: "/repo/UnityProject",
            mode: "daemon",
            timeout: "1234",
            failFast: true,
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, service.CapturedCancellationToken);
        var input = Assert.IsType<ReadyCommandInput>(service.CapturedInput);
        Assert.Equal(ReadyTarget.Execution, input.Target);
        Assert.Equal("/repo/UnityProject", input.ProjectPath);
        Assert.Equal(UnityExecutionMode.Daemon, input.Mode);
        Assert.Null(input.ReadIndexMode);
        Assert.False(input.IsReadIndexModeSpecified);
        Assert.Equal(1234, input.TimeoutMilliseconds);
        Assert.True(input.FailFast);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ready_WithReadIndexTarget_MapsReadIndexModeToServiceInput ()
    {
        var service = new StubReadyService((_, _) => ValueTask.FromResult(ReadyExecutionResult.Success(CreateOutput())));
        var command = new ReadyCommand(service, CommandResultTestWriter.Create());

        await StandardOutputCapture.ExecuteAsync(() => command.ReadyAsync(
            @for: "readIndex",
            readIndexMode: "requireFresh",
            cancellationToken: CancellationToken.None));

        var input = Assert.IsType<ReadyCommandInput>(service.CapturedInput);
        Assert.Equal(ReadyTarget.ReadIndex, input.Target);
        Assert.Null(input.Mode);
        Assert.Equal(ReadIndexMode.RequireFresh, input.ReadIndexMode);
        Assert.True(input.IsReadIndexModeSpecified);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ready_WhenTargetIsOmitted_UsesExecutionTarget ()
    {
        var service = new StubReadyService((_, _) => ValueTask.FromResult(ReadyExecutionResult.Success(CreateOutput())));
        var command = new ReadyCommand(service, CommandResultTestWriter.Create());

        await StandardOutputCapture.ExecuteAsync(() => command.ReadyAsync(
            @for: null,
            cancellationToken: CancellationToken.None));

        var input = Assert.IsType<ReadyCommandInput>(service.CapturedInput);
        Assert.Equal(ReadyTarget.Execution, input.Target);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ready_WhenTargetIsBlank_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubReadyService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new ReadyCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.ReadyAsync(
            @for: "   ",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Ready,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ready_WhenTargetIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubReadyService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new ReadyCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.ReadyAsync(
            @for: "unknown",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Ready,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Theory]
    [InlineData(ReadyVerdictValues.Fail)]
    [InlineData(ReadyVerdictValues.Incomplete)]
    [Trait("Size", "Small")]
    public async Task Ready_WithNonPassVerdict_ReturnsOkEnvelopeWithFailureExitCode (string verdict)
    {
        var service = new StubReadyService((_, _) => ValueTask.FromResult(ReadyExecutionResult.Success(CreateOutput(verdict))));
        var command = new ReadyCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.ReadyAsync(
            @for: "execution",
            cancellationToken: CancellationToken.None));

        Assert.Equal(1, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Ready,
            IpcProtocol.StatusOk,
            1);
        Assert.Equal(verdict, outputJson.RootElement.GetProperty("payload").GetProperty("verdict").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ready_WithAutoOneshotOutput_MatchesGolden ()
    {
        var service = new StubReadyService((_, _) => ValueTask.FromResult(ReadyExecutionResult.Success(CreateOutput())));
        var command = new ReadyCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.ReadyAsync(
            @for: "execution",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("ready", "auto-oneshot-success.json"),
            standardOutput,
            CreateReadyGoldenNormalization());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ready_WithReadIndexOutput_MatchesGolden ()
    {
        var service = new StubReadyService((_, _) => ValueTask.FromResult(ReadyExecutionResult.Success(CreateReadIndexOutput())));
        var command = new ReadyCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.ReadyAsync(
            @for: "readIndex",
            readIndexMode: "allowStale",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("ready", "read-index-success.json"),
            standardOutput,
            CreateReadyGoldenNormalization());
    }

    private static ReadyExecutionOutput CreateOutput (
        string verdict = ReadyVerdictValues.Pass)
    {
        var lifecycle = CreateLifecycle();
        var claimStatus = string.Equals(verdict, ReadyVerdictValues.Pass, StringComparison.Ordinal)
            ? ReadyClaimStatusValues.Passed
            : ReadyClaimStatusValues.Failed;
        return new ReadyExecutionOutput(
            Verdict: verdict,
            Project: CreateProject(),
            Verifiers:
            [
                new ReadyVerifierOutput(
                    Id: "ready.lifecycle",
                    Kind: "ready.lifecycle",
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: [ReadyClaimCodes.UnityReadyExecution],
                    Effects: []),
            ],
            Claims:
            [
                new ReadyClaimOutput(
                    Id: ReadyClaimCodes.UnityReadyExecution,
                    Status: claimStatus,
                    Coverage: ReadyCoverageValues.Full,
                    Required: true,
                    VerifierRef: "ready.lifecycle",
                    Statement: "Unity is ready for execution.",
                    Subject: CreateSubject("execution", "auto", "oneshot", "transientProbe"),
                    Validity: new ReadyClaimValidityOutput(
                        ReadyValidityKindValues.ProbeOnly,
                        GuaranteesReusableSession: false),
                    Evidence:
                    [
                        new ReadyEvidenceOutput(
                            Kind: "lifecycleSnapshot",
                            Data: lifecycle),
                    ],
                    ResidualRisks: []),
            ],
            Reports: new Dictionary<string, ReadyReportOutput>(StringComparer.Ordinal),
            ResidualRisks: [],
            Target: "execution",
            RequestedMode: "auto",
            ResolvedMode: "oneshot",
            SessionKind: "transientProbe",
            TimeoutMilliseconds: 10000,
            Lifecycle: lifecycle,
            ReadIndex: null);
    }

    private static ReadyExecutionOutput CreateReadIndexOutput ()
    {
        var readIndex = new ReadyReadIndexOutput("allowStale", CreateReadIndexArtifacts());
        return new ReadyExecutionOutput(
            Verdict: ReadyVerdictValues.Pass,
            Project: CreateProject(),
            Verifiers:
            [
                new ReadyVerifierOutput(
                    Id: "ready.readIndex",
                    Kind: "ready.readIndex",
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: [ReadyClaimCodes.UnityReadyReadIndex],
                    Effects: []),
            ],
            Claims:
            [
                new ReadyClaimOutput(
                    Id: ReadyClaimCodes.UnityReadyReadIndex,
                    Status: ReadyClaimStatusValues.Passed,
                    Coverage: ReadyCoverageValues.Full,
                    Required: true,
                    VerifierRef: "ready.readIndex",
                    Statement: "Unity is ready for readIndex.",
                    Subject: CreateSubject("readIndex", "auto", AssuranceExecutionModeCodec.NotApplicable, AssuranceSessionKindValues.ArtifactOnly),
                    Validity: new ReadyClaimValidityOutput(
                        ReadyValidityKindValues.ProbeOnly,
                        GuaranteesReusableSession: false),
                    Evidence:
                    [
                        new ReadyEvidenceOutput(
                            Kind: "readIndexSummary",
                            Data: readIndex),
                    ],
                    ResidualRisks: []),
            ],
            Reports: new Dictionary<string, ReadyReportOutput>(StringComparer.Ordinal),
            ResidualRisks: [],
            Target: "readIndex",
            RequestedMode: "auto",
            ResolvedMode: AssuranceExecutionModeCodec.NotApplicable,
            SessionKind: AssuranceSessionKindValues.ArtifactOnly,
            TimeoutMilliseconds: 10000,
            Lifecycle: null,
            ReadIndex: readIndex);
    }

    private static ProjectIdentityInfo CreateProject ()
    {
        return new ProjectIdentityInfo(
            ProjectPath: "<projectPath>",
            ProjectFingerprint: "<projectFingerprint>",
            UnityVersion: "6000.1.4f1");
    }

    private static JsonGoldenFileNormalization CreateReadyGoldenNormalization ()
    {
        return new JsonGoldenFileNormalization()
            .NormalizeStringPropertyValue("projectPath", "<projectPath>")
            .NormalizeStringPropertyValue("projectFingerprint", "<projectFingerprint>");
    }

    private static ReadyLifecycleOutput CreateLifecycle ()
    {
        return new ReadyLifecycleOutput(
            ServerVersion: "0.5.0",
            UnityVersion: "6000.1.4f1",
            EditorMode: "batchmode",
            LifecycleState: "ready",
            BlockingReason: null,
            CompileState: "ready",
            CompileGeneration: "12",
            DomainReloadGeneration: "7",
            CanAcceptExecutionRequests: true,
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00Z"),
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: new PlayModeSnapshotOutput(
                State: IpcPlayModeStateNames.Stopped,
                Transition: IpcPlayModeTransitionNames.None,
                IsPlaying: false,
                IsPlayingOrWillChangePlaymode: false,
                Generation: "2"));
    }

    private static Dictionary<string, object?> CreateSubject (
        string target,
        string requestedMode,
        string resolvedMode,
        string sessionKind)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["kind"] = "unityReady",
            ["target"] = target,
            ["requestedMode"] = requestedMode,
            ["resolvedMode"] = resolvedMode,
            ["sessionKind"] = sessionKind,
        };
    }

    private static IReadOnlyList<ReadyReadIndexArtifactOutput> CreateReadIndexArtifacts ()
    {
        var generatedAtUtc = DateTimeOffset.Parse("2026-05-17T00:00:00Z");
        return
        [
            new ReadyReadIndexArtifactOutput(
                Name: "inputs.manifest",
                Status: ReadyReadIndexArtifactStatusValues.Available,
                SourceInputsHash: "source-hash",
                GeneratedAtUtc: generatedAtUtc),
            CreateCatalogArtifact("ops.catalog", generatedAtUtc),
            CreateCatalogArtifact("types.catalog", generatedAtUtc),
            CreateCatalogArtifact("schemas.catalog", generatedAtUtc),
            CreateCatalogArtifact("asset-search.lookup", generatedAtUtc),
            CreateCatalogArtifact("guid-path.lookup", generatedAtUtc),
        ];
    }

    private static ReadyReadIndexArtifactOutput CreateCatalogArtifact (
        string name,
        DateTimeOffset generatedAtUtc)
    {
        return new ReadyReadIndexArtifactOutput(
            Name: name,
            Status: ReadyReadIndexArtifactStatusValues.Available,
            Freshness: "fresh",
            SourceInputsHash: "source-hash",
            GeneratedAtUtc: generatedAtUtc);
    }

    private sealed class StubReadyService : IReadyService
    {
        private readonly Func<ReadyCommandInput, CancellationToken, ValueTask<ReadyExecutionResult>> handler;

        public StubReadyService (Func<ReadyCommandInput, CancellationToken, ValueTask<ReadyExecutionResult>> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public ReadyCommandInput? CapturedInput { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public ValueTask<ReadyExecutionResult> ExecuteAsync (
            ReadyCommandInput input,
            CancellationToken cancellationToken = default)
        {
            CapturedInput = input;
            CapturedCancellationToken = cancellationToken;
            return handler(input, cancellationToken);
        }
    }
}
