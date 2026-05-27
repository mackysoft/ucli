using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Ready;

public sealed class ReadyServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithAutoResolvedToOneshot_ReturnsProbeOnlyValidityWithoutReusableSession ()
    {
        var unityRequestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(new UnityRequestResponse(
            IpcPayloadCodec.SerializeToElement(CreateReadyPingResponse()),
            [],
            HasFailureStatus: false)));
        var service = CreateService(
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(new UnityExecutionModeDecision(
                UnityExecutionMode.Auto,
                DaemonRunning: false,
                UnityExecutionTarget.Oneshot,
                TimeSpan.FromSeconds(10)))),
            unityRequestExecutor: unityRequestExecutor);

        var result = await service.ExecuteAsync(new ReadyCommandInput(
            ProjectPath: null,
            Target: ReadyTarget.Execution,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000,
            ReadIndexMode: null,
            IsReadIndexModeSpecified: false,
            FailFast: false));

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<ReadyExecutionOutput>(result.Output);
        Assert.Equal(ReadyVerdictValues.Pass, output.Verdict);
        Assert.Equal("oneshot", output.ResolvedMode);
        Assert.Equal("transientProbe", output.SessionKind);
        Assert.NotNull(output.Lifecycle);
        Assert.NotNull(output.Lifecycle.PlayMode);
        Assert.Equal("stopped", output.Lifecycle.PlayMode.State);
        Assert.Equal("none", output.Lifecycle.PlayMode.Transition);
        var claim = Assert.Single(output.Claims);
        Assert.Equal("probeOnly", claim.Validity.Kind);
        Assert.False(claim.Validity.GuaranteesReusableSession);
        var payload = Assert.IsType<UnityRequestPayload.Ping>(unityRequestExecutor.CapturedPayload);
        Assert.Equal(IpcPingClientVersions.Ready, payload.ClientVersion);
        Assert.False(payload.FailFast);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithBlockedDaemonLifecycle_ReturnsFailedClaimPacket ()
    {
        var service = CreateService(
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(new UnityExecutionModeDecision(
                UnityExecutionMode.Daemon,
                DaemonRunning: true,
                UnityExecutionTarget.Daemon,
                TimeSpan.FromSeconds(10)))),
            daemonPingInfoClient: new StubDaemonPingInfoClient(CreateReadyPingResponse(
                lifecycleState: IpcEditorLifecycleStateCodec.CompileFailed,
                canAcceptExecutionRequests: false)));

        var result = await service.ExecuteAsync(new ReadyCommandInput(
            ProjectPath: null,
            Target: ReadyTarget.Execution,
            Mode: UnityExecutionMode.Daemon,
            TimeoutMilliseconds: 10000,
            ReadIndexMode: null,
            IsReadIndexModeSpecified: false,
            FailFast: true));

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<ReadyExecutionOutput>(result.Output);
        Assert.Equal(ReadyVerdictValues.Fail, output.Verdict);
        Assert.Equal("daemon", output.ResolvedMode);
        var claim = Assert.Single(output.Claims);
        Assert.Equal(ReadyClaimStatusValues.Failed, claim.Status);
        Assert.Equal("sessionBound", claim.Validity.Kind);
        Assert.False(claim.Validity.GuaranteesReusableSession);
        Assert.Contains(claim.Evidence, static evidence => string.Equals(evidence.Kind, "readinessDecision", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithUnsupportedDaemonLifecycleState_ReturnsCommandFailure ()
    {
        var service = CreateService(
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(new UnityExecutionModeDecision(
                UnityExecutionMode.Daemon,
                DaemonRunning: true,
                UnityExecutionTarget.Daemon,
                TimeSpan.FromSeconds(10)))),
            daemonPingInfoClient: new StubDaemonPingInfoClient(CreateReadyPingResponse(
                lifecycleState: "futureState",
                canAcceptExecutionRequests: false)));

        var result = await service.ExecuteAsync(new ReadyCommandInput(
            ProjectPath: null,
            Target: ReadyTarget.Execution,
            Mode: UnityExecutionMode.Daemon,
            TimeoutMilliseconds: 10000,
            ReadIndexMode: null,
            IsReadIndexModeSpecified: false,
            FailFast: true));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("unsupported state", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithReadIndexModeOnNonReadIndexTarget_ReturnsInvalidArgument ()
    {
        var service = CreateService();

        var result = await service.ExecuteAsync(new ReadyCommandInput(
            ProjectPath: null,
            Target: ReadyTarget.Execution,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000,
            ReadIndexMode: ReadIndexMode.RequireFresh,
            IsReadIndexModeSpecified: true,
            FailFast: false));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Contains("--readIndexMode", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithReadIndexTarget_ReadsArtifactsWithoutUnityLifecycleProbe ()
    {
        var service = CreateService(
            modeDecisionService: new ThrowingModeDecisionService(),
            daemonPingInfoClient: new ThrowingDaemonPingInfoClient(),
            unityRequestExecutor: new ThrowingUnityRequestExecutor(),
            readIndexArtifactReader: new SuccessfulReadIndexArtifactReader());

        var result = await service.ExecuteAsync(new ReadyCommandInput(
            ProjectPath: null,
            Target: ReadyTarget.ReadIndex,
            Mode: null,
            TimeoutMilliseconds: 10000,
            ReadIndexMode: ReadIndexMode.AllowStale,
            IsReadIndexModeSpecified: true,
            FailFast: false));

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<ReadyExecutionOutput>(result.Output);
        Assert.Equal(ReadyVerdictValues.Pass, output.Verdict);
        Assert.Equal(AssuranceExecutionModeCodec.NotApplicable, output.ResolvedMode);
        Assert.Equal(AssuranceSessionKindValues.ArtifactOnly, output.SessionKind);
        Assert.Null(output.Lifecycle);
        Assert.NotNull(output.ReadIndex);
        Assert.Equal("allowStale", output.ReadIndex.Mode);
        Assert.Equal(
            ["ops.catalog", "asset-search.lookup", "guid-path.lookup"],
            output.ReadIndex.Artifacts.Select(static artifact => artifact.Name));
        Assert.All(output.ReadIndex.Artifacts, static artifact => Assert.True(artifact.Required));
        var claim = Assert.Single(output.Claims);
        Assert.Equal(ReadyValidityKindValues.ProbeOnly, claim.Validity.Kind);
        Assert.False(claim.Validity.GuaranteesReusableSession);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithReadIndexTarget_WhenRequiredArtifactIsMissing_ReturnsFailedClaimWithActionRequired ()
    {
        var service = CreateService(
            readIndexArtifactReader: new SuccessfulReadIndexArtifactReader(missingArtifactName: "asset-search.lookup"));

        var result = await service.ExecuteAsync(new ReadyCommandInput(
            ProjectPath: null,
            Target: ReadyTarget.ReadIndex,
            Mode: null,
            TimeoutMilliseconds: 10000,
            ReadIndexMode: ReadIndexMode.RequireFresh,
            IsReadIndexModeSpecified: true,
            FailFast: false));

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<ReadyExecutionOutput>(result.Output);
        Assert.Equal(ReadyVerdictValues.Fail, output.Verdict);
        var claim = Assert.Single(output.Claims);
        Assert.Equal(ReadyClaimStatusValues.Failed, claim.Status);

        var artifact = Assert.Single(
            output.ReadIndex!.Artifacts,
            static artifact => string.Equals(artifact.Name, "asset-search.lookup", StringComparison.Ordinal));
        Assert.True(artifact.Required);
        Assert.Equal(ReadyReadIndexArtifactStatusValues.Failed, artifact.Status);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexBootstrapFailed.Value, artifact.Code);
        Assert.Contains("query assets find", artifact.ActionRequired, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithExplicitRuntimeModeOnReadIndexTarget_ReturnsInvalidArgument ()
    {
        var service = CreateService(readIndexArtifactReader: new SuccessfulReadIndexArtifactReader());

        var result = await service.ExecuteAsync(new ReadyCommandInput(
            ProjectPath: null,
            Target: ReadyTarget.ReadIndex,
            Mode: UnityExecutionMode.Daemon,
            TimeoutMilliseconds: 10000,
            ReadIndexMode: ReadIndexMode.AllowStale,
            IsReadIndexModeSpecified: true,
            FailFast: false));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Contains("--mode daemon", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithReadIndexDisabled_ReturnsInvalidArgument ()
    {
        var service = CreateService(readIndexArtifactReader: new SuccessfulReadIndexArtifactReader());

        var result = await service.ExecuteAsync(new ReadyCommandInput(
            ProjectPath: null,
            Target: ReadyTarget.ReadIndex,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000,
            ReadIndexMode: ReadIndexMode.Disabled,
            IsReadIndexModeSpecified: true,
            FailFast: false));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Contains("allowStale or requireFresh", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithModeContractError_ReturnsCommandFailure ()
    {
        var service = CreateService(
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.ContractFailure(
                new UnityExecutionModeDecisionContractError(
                    UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
                    "Daemon is not running for mode=daemon."))));

        var result = await service.ExecuteAsync(new ReadyCommandInput(
            ProjectPath: null,
            Target: ReadyTarget.Execution,
            Mode: UnityExecutionMode.Daemon,
            TimeoutMilliseconds: 10000,
            ReadIndexMode: null,
            IsReadIndexModeSpecified: false,
            FailFast: false));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithDaemonTimeoutBeforeLifecycleObservation_ReturnsCommandFailure ()
    {
        var timeProvider = new ManualTimeProvider();
        var service = CreateService(
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(new UnityExecutionModeDecision(
                UnityExecutionMode.Daemon,
                DaemonRunning: true,
                UnityExecutionTarget.Daemon,
                TimeSpan.FromSeconds(10)))),
            daemonPingInfoClient: new TimeoutDaemonPingInfoClient(timeProvider),
            timeProvider: timeProvider);

        var result = await service.ExecuteAsync(new ReadyCommandInput(
            ProjectPath: null,
            Target: ReadyTarget.Execution,
            Mode: UnityExecutionMode.Daemon,
            TimeoutMilliseconds: 10000,
            ReadIndexMode: null,
            IsReadIndexModeSpecified: false,
            FailFast: false));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithOneshotObservedLifecycleFailure_ReturnsFailedClaimPacket ()
    {
        var service = CreateService(
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(new UnityExecutionModeDecision(
                UnityExecutionMode.Auto,
                DaemonRunning: false,
                UnityExecutionTarget.Oneshot,
                TimeSpan.FromSeconds(10)))),
            unityRequestExecutor: new StubUnityRequestExecutor(UnityRequestExecutionResult.Failure(new UnityRequestFailure(
                EditorLifecycleErrorCodes.EditorCompileFailed,
                "Unity editor has script compilation failures."))));

        var result = await service.ExecuteAsync(new ReadyCommandInput(
            ProjectPath: null,
            Target: ReadyTarget.Execution,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000,
            ReadIndexMode: null,
            IsReadIndexModeSpecified: false,
            FailFast: false));

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<ReadyExecutionOutput>(result.Output);
        Assert.Equal(ReadyVerdictValues.Fail, output.Verdict);
        var claim = Assert.Single(output.Claims);
        Assert.Equal(ReadyClaimStatusValues.Failed, claim.Status);
        Assert.Contains(claim.Evidence, static evidence => string.Equals(evidence.Kind, "readinessDecision", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithOneshotStartupFailure_PreservesStartupFailureOnCommandFailure ()
    {
        var startupFailure = CreateStartupFailureDetail();
        var service = CreateService(
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(new UnityExecutionModeDecision(
                UnityExecutionMode.Auto,
                DaemonRunning: false,
                UnityExecutionTarget.Oneshot,
                TimeSpan.FromSeconds(10)))),
            unityRequestExecutor: new StubUnityRequestExecutor(UnityRequestExecutionResult.Failure(new UnityRequestFailure(
                DaemonErrorCodes.DaemonStartupBlocked,
                "Unity startup is blocked.",
                startupFailure))));

        var result = await service.ExecuteAsync(new ReadyCommandInput(
            ProjectPath: null,
            Target: ReadyTarget.Execution,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000,
            ReadIndexMode: null,
            IsReadIndexModeSpecified: false,
            FailFast: true));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, error.Code);
        Assert.Same(startupFailure, error.StartupFailure);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithOneshotPingProjectFingerprintMismatch_ReturnsCommandFailure ()
    {
        var service = CreateService(
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(new UnityExecutionModeDecision(
                UnityExecutionMode.Auto,
                DaemonRunning: false,
                UnityExecutionTarget.Oneshot,
                TimeSpan.FromSeconds(10)))),
            unityRequestExecutor: new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(new UnityRequestResponse(
                IpcPayloadCodec.SerializeToElement(CreateReadyPingResponse(projectFingerprint: "other-fingerprint")),
                [],
                HasFailureStatus: false))));

        var result = await service.ExecuteAsync(new ReadyCommandInput(
            ProjectPath: null,
            Target: ReadyTarget.Execution,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000,
            ReadIndexMode: null,
            IsReadIndexModeSpecified: false,
            FailFast: false));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("projectFingerprint mismatch", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithDomainReloadingLifecycle_ReturnsFailedClaimWithoutWaiting ()
    {
        var service = CreateService(
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(new UnityExecutionModeDecision(
                UnityExecutionMode.Auto,
                DaemonRunning: false,
                UnityExecutionTarget.Oneshot,
                TimeSpan.FromSeconds(10)))),
            unityRequestExecutor: new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(new UnityRequestResponse(
                IpcPayloadCodec.SerializeToElement(CreateReadyPingResponse(
                    lifecycleState: IpcEditorLifecycleStateCodec.DomainReloading,
                    canAcceptExecutionRequests: false)),
                [],
                HasFailureStatus: false))));

        var result = await service.ExecuteAsync(new ReadyCommandInput(
            ProjectPath: null,
            Target: ReadyTarget.Execution,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000,
            ReadIndexMode: null,
            IsReadIndexModeSpecified: false,
            FailFast: false));

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<ReadyExecutionOutput>(result.Output);
        Assert.Equal(ReadyVerdictValues.Fail, output.Verdict);
        var claim = Assert.Single(output.Claims);
        Assert.Equal(ReadyClaimStatusValues.Failed, claim.Status);
        Assert.Contains(claim.Evidence, static evidence => string.Equals(evidence.Kind, "readinessDecision", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithOneshotFailFast_PropagatesFailFastToPingPayload ()
    {
        var unityRequestExecutor = new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(new UnityRequestResponse(
            IpcPayloadCodec.SerializeToElement(CreateReadyPingResponse()),
            [],
            HasFailureStatus: false)));
        var service = CreateService(
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(new UnityExecutionModeDecision(
                UnityExecutionMode.Oneshot,
                DaemonRunning: false,
                UnityExecutionTarget.Oneshot,
                TimeSpan.FromSeconds(10)))),
            unityRequestExecutor: unityRequestExecutor);

        var result = await service.ExecuteAsync(new ReadyCommandInput(
            ProjectPath: null,
            Target: ReadyTarget.Execution,
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 10000,
            ReadIndexMode: null,
            IsReadIndexModeSpecified: false,
            FailFast: true));

        Assert.True(result.IsSuccess);
        var payload = Assert.IsType<UnityRequestPayload.Ping>(unityRequestExecutor.CapturedPayload);
        Assert.True(payload.FailFast);
    }

    private static ReadyService CreateService (
        IProjectContextResolver? projectContextResolver = null,
        IUnityExecutionModeDecisionService? modeDecisionService = null,
        IDaemonPingInfoClient? daemonPingInfoClient = null,
        IUnityRequestExecutor? unityRequestExecutor = null,
        IReadIndexArtifactReader? readIndexArtifactReader = null,
        IReadIndexFreshnessEvaluator? freshnessEvaluator = null,
        TimeProvider? timeProvider = null)
    {
        return new ReadyService(
            projectContextResolver ?? new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateProjectContext())),
            modeDecisionService ?? new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(new UnityExecutionModeDecision(
                UnityExecutionMode.Auto,
                DaemonRunning: false,
                UnityExecutionTarget.Oneshot,
                TimeSpan.FromSeconds(10)))),
            daemonPingInfoClient ?? new StubDaemonPingInfoClient(CreateReadyPingResponse()),
            unityRequestExecutor ?? new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(new UnityRequestResponse(
                IpcPayloadCodec.SerializeToElement(CreateReadyPingResponse()),
                [],
                HasFailureStatus: false))),
            readIndexArtifactReader ?? new ThrowingReadIndexArtifactReader(),
            freshnessEvaluator ?? new StubReadIndexFreshnessEvaluator(IndexFreshnessEvaluationResult.Success(IndexFreshness.Fresh)),
            timeProvider ?? TimeProvider.System);
    }

    private static ProjectContext CreateProjectContext ()
    {
        var unityProject = new ResolvedUnityProjectContext(
            UnityProjectRoot: "/workspace/UnityProject",
            RepositoryRoot: "/workspace",
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption,
            PathSourceLabel: "--projectPath",
            UnityVersion: "6000.1.4f1");
        return new ProjectContext(
            unityProject,
            UcliConfig.CreateDefault(),
            ConfigSource.Default);
    }

    private static IpcPingResponse CreateReadyPingResponse (
        string lifecycleState = IpcEditorLifecycleStateCodec.Ready,
        bool canAcceptExecutionRequests = true,
        string projectFingerprint = "project-fingerprint")
    {
        return new IpcPingResponse(
            ServerVersion: "0.5.0",
            EditorMode: "batchmode",
            UnityVersion: "6000.1.4f1",
            ProjectFingerprint: projectFingerprint,
            CompileState: "ready",
            LifecycleState: lifecycleState,
            BlockingReason: lifecycleState == IpcEditorLifecycleStateCodec.Ready ? null : "compileFailed",
            CompileGeneration: "12",
            DomainReloadGeneration: "7",
            CanAcceptExecutionRequests: canAcceptExecutionRequests,
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00Z"),
            PlayMode: new IpcPlayModeSnapshot(
                State: "stopped",
                Transition: "none",
                IsPlaying: false,
                IsPlayingOrWillChangePlaymode: false,
                Generation: "2"));
    }

    private static StartupFailureDetail CreateStartupFailureDetail ()
    {
        return new StartupFailureDetail(
            Startup: new DaemonStartupObservationOutput(
                StartupStatus: "blocked",
                StartupBlockingReason: "compile",
                LaunchAttemptId: null,
                EditorMode: "batchmode",
                OwnerKind: "cli",
                CanShutdownProcess: true,
                ProcessId: 1234,
                StartedAtUtc: DateTimeOffset.Parse("2026-03-12T04:05:01+00:00"),
                ElapsedMilliseconds: null,
                ProcessAction: "terminated",
                ProcessTermination: null,
                ArtifactPath: null,
                RetryDisposition: "retryAfterFix"),
            Diagnosis: new DaemonDiagnosisOutput(
                Reason: "unityScriptCompilationFailed",
                Message: "Unity startup is blocked.",
                ReportedBy: "cli",
                IsInferred: true,
                UpdatedAtUtc: DateTimeOffset.Parse("2026-03-12T04:05:06+00:00"),
                ProcessId: 1234,
                EditorInstancePath: null,
                ProcessStartedAtUtc: DateTimeOffset.Parse("2026-03-12T04:05:01+00:00"),
                UnityLogPath: "/repo/.ucli/local/logs/unity.log",
                StartupPhase: "scriptCompilation",
                ActionRequired: "fixCompileErrors",
                PrimaryDiagnostic: new DaemonPrimaryDiagnosticOutput(
                    Kind: "compiler",
                    Code: "CS0246",
                    File: "Assets/Scripts/Broken.cs",
                    Line: 10,
                    Column: 5,
                    Message: "error CS0246")),
            RetryDisposition: "retryAfterFix",
            SafeToRetryImmediately: false);
    }

    private sealed class StubProjectContextResolver : IProjectContextResolver
    {
        private readonly ProjectContextResolutionResult result;

        public StubProjectContextResolver (ProjectContextResolutionResult result)
        {
            this.result = result;
        }

        public ValueTask<ProjectContextResolutionResult> ResolveAsync (
            string? projectPath,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubModeDecisionService : IUnityExecutionModeDecisionService
    {
        private readonly UnityExecutionModeDecisionResult result;

        public StubModeDecisionService (UnityExecutionModeDecisionResult result)
        {
            this.result = result;
        }

        public ValueTask<UnityExecutionModeDecisionResult> DecideAsync (
            UnityExecutionMode mode,
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ThrowingModeDecisionService : IUnityExecutionModeDecisionService
    {
        public ValueTask<UnityExecutionModeDecisionResult> DecideAsync (
            UnityExecutionMode mode,
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Mode decision should not be called.");
        }
    }

    private sealed class StubDaemonPingInfoClient : IDaemonPingInfoClient
    {
        private readonly IpcPingResponse response;

        public StubDaemonPingInfoClient (IpcPingResponse response)
        {
            this.response = response;
        }

        public ValueTask<IpcPingResponse> PingAndReadAsync (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            string? sessionToken = null,
            bool validateProjectFingerprint = true,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(response);
        }
    }

    private sealed class TimeoutDaemonPingInfoClient : IDaemonPingInfoClient
    {
        private readonly ManualTimeProvider timeProvider;

        public TimeoutDaemonPingInfoClient (ManualTimeProvider timeProvider)
        {
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        public ValueTask<IpcPingResponse> PingAndReadAsync (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            string? sessionToken = null,
            bool validateProjectFingerprint = true,
            CancellationToken cancellationToken = default)
        {
            timeProvider.Advance(TimeSpan.FromDays(1));
            throw new TimeoutException("daemon ping timed out");
        }
    }

    private sealed class ThrowingDaemonPingInfoClient : IDaemonPingInfoClient
    {
        public ValueTask<IpcPingResponse> PingAndReadAsync (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            string? sessionToken = null,
            bool validateProjectFingerprint = true,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Daemon ping should not be called.");
        }
    }

    private sealed class StubUnityRequestExecutor : IUnityRequestExecutor
    {
        private readonly UnityRequestExecutionResult result;

        public StubUnityRequestExecutor (UnityRequestExecutionResult result)
        {
            this.result = result;
        }

        public ValueTask<UnityRequestExecutionResult> ExecuteAsync (
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            UcliConfig config,
            ResolvedUnityProjectContext unityProject,
            UnityRequestPayload payload,
            CancellationToken cancellationToken = default)
        {
            CapturedPayload = payload;
            return ValueTask.FromResult(result);
        }

        public UnityRequestPayload? CapturedPayload { get; private set; }
    }

    private sealed class ThrowingUnityRequestExecutor : IUnityRequestExecutor
    {
        public ValueTask<UnityRequestExecutionResult> ExecuteAsync (
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            UcliConfig config,
            ResolvedUnityProjectContext unityProject,
            UnityRequestPayload payload,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Unity request executor should not be called.");
        }
    }

    private sealed class StubReadIndexFreshnessEvaluator : IReadIndexFreshnessEvaluator
    {
        private readonly IndexFreshnessEvaluationResult result;

        public StubReadIndexFreshnessEvaluator (IndexFreshnessEvaluationResult result)
        {
            this.result = result;
        }

        public ValueTask<IndexFreshnessEvaluationResult> ObserveAsync (
            ResolvedUnityProjectContext unityProject,
            IndexFreshnessTarget target,
            string? persistedSourceInputsHash,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(result);
        }

        public ValueTask<IndexFreshnessEvaluationResult> ObserveSceneTreeLiteAsync (
            ResolvedUnityProjectContext unityProject,
            string scenePath,
            string? persistedSourceInputsHash,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ThrowingReadIndexArtifactReader : IReadIndexArtifactReader
    {
        public ValueTask<ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>> ReadOpsCatalogAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexOpsDescribeJsonContract>> ReadOpsDescribeAsync (
            ResolvedUnityProjectContext unityProject,
            IndexOpsCatalogEntryJsonContract catalogEntry,
            string sourceInputsHash,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexTypesCatalogJsonContract>> ReadTypesCatalogAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexSchemasCatalogJsonContract>> ReadSchemasCatalogAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexAssetSearchLookupJsonContract>> ReadAssetSearchLookupAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexGuidPathLookupJsonContract>> ReadGuidPathLookupAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>> ReadSceneTreeLiteLookupAsync (
            ResolvedUnityProjectContext unityProject,
            string scenePath,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexInputsManifestJsonContract>> ReadInputsManifestAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class SuccessfulReadIndexArtifactReader : IReadIndexArtifactReader
    {
        private static readonly DateTimeOffset GeneratedAtUtc = DateTimeOffset.Parse("2026-05-17T00:00:00Z");

        private readonly string? missingArtifactName;

        public SuccessfulReadIndexArtifactReader (string? missingArtifactName = null)
        {
            this.missingArtifactName = missingArtifactName;
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>> ReadOpsCatalogAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            if (IsMissing("ops.catalog"))
            {
                return Missing<IndexOpsCatalogJsonContract>("ops.catalog.json");
            }

            return Success(new IndexOpsCatalogJsonContract(1, GeneratedAtUtc, "source-hash", []));
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexOpsDescribeJsonContract>> ReadOpsDescribeAsync (
            ResolvedUnityProjectContext unityProject,
            IndexOpsCatalogEntryJsonContract catalogEntry,
            string sourceInputsHash,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexTypesCatalogJsonContract>> ReadTypesCatalogAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("types.catalog is not part of readIndex readiness.");
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexSchemasCatalogJsonContract>> ReadSchemasCatalogAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("schemas.catalog is not part of readIndex readiness.");
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexAssetSearchLookupJsonContract>> ReadAssetSearchLookupAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            if (IsMissing("asset-search.lookup"))
            {
                return Missing<IndexAssetSearchLookupJsonContract>("lookups/asset-search.lookup.json");
            }

            return Success(new IndexAssetSearchLookupJsonContract(1, GeneratedAtUtc, "source-hash", []));
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexGuidPathLookupJsonContract>> ReadGuidPathLookupAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            if (IsMissing("guid-path.lookup"))
            {
                return Missing<IndexGuidPathLookupJsonContract>("lookups/guid-path.lookup.json");
            }

            return Success(new IndexGuidPathLookupJsonContract(1, GeneratedAtUtc, "source-hash", []));
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>> ReadSceneTreeLiteLookupAsync (
            ResolvedUnityProjectContext unityProject,
            string scenePath,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadIndexArtifactReadResult<IndexInputsManifestJsonContract>> ReadInputsManifestAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("inputs.manifest is not part of readIndex readiness.");
        }

        private bool IsMissing (string artifactName)
        {
            return string.Equals(missingArtifactName, artifactName, StringComparison.Ordinal);
        }

        private static ValueTask<ReadIndexArtifactReadResult<T>> Missing<T> (string artifactPath)
            where T : class
        {
            return ValueTask.FromResult(ReadIndexArtifactReadResult<T>.Failure(
                ReadIndexErrorCodes.ReadIndexBootstrapFailed,
                $"Index contract file '{artifactPath}' does not exist."));
        }

        private static ValueTask<ReadIndexArtifactReadResult<T>> Success<T> (T value)
            where T : class
        {
            return ValueTask.FromResult(ReadIndexArtifactReadResult<T>.Success(value));
        }
    }
}
