using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Compile;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Compile;

public sealed class CompileServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithSuccessfulCompileResponse_ReturnsPassOutput ()
    {
        var unityRequestExecutor = new StubUnityRequestExecutor(CreateCompileResponseResult(CreateSummary()));
        var service = CreateService(unityRequestExecutor: unityRequestExecutor);

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        Assert.True(result.IsSuccess);
        var output = result.Output!;
        Assert.Equal(CompileVerdictValues.Pass, output.Verdict);
        Assert.Equal("run-1", output.Compile.RunId);
        Assert.Equal("oneshot", output.ResolvedMode);
        Assert.Equal("transientProbe", output.SessionKind);
        Assert.Equal(3, output.Claims.Count);
        var payload = Assert.IsType<UnityRequestPayload.Compile>(unityRequestExecutor.CapturedPayload);
        Assert.Equal("run-1", payload.RunId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithRecoveredArtifactRunIdMismatch_ReturnsCommandFailure ()
    {
        var service = CreateService(
            unityRequestExecutor: new StubUnityRequestExecutor(UnityRequestExecutionResult.Failure(new UnityRequestFailure(
                ExecutionErrorCodes.IpcTimeout,
                "Unity compile request timed out."))),
            artifactStore: new StubCompileRunArtifactReader(CompileRunArtifactReadResult.Success(CreateSummary(runId: "other-run"))));

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 10000));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("runId mismatch", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithCompileResponseMissingSummary_ReturnsCommandFailure ()
    {
        using var document = JsonDocument.Parse("""{"runId":"run-1","summary":null}""");
        var payload = document.RootElement.Clone();
        var service = CreateService(
            unityRequestExecutor: new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(new UnityRequestResponse(
                payload,
                [],
                HasFailureStatus: false))));

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 10000));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("summary", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithCompileTimeoutResponse_ReadsRecoveredArtifact ()
    {
        var artifactStore = new StubCompileRunArtifactReader(CompileRunArtifactReadResult.Success(CreateSummary(errorCount: 1)));
        var service = CreateService(
            unityRequestExecutor: new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(new UnityRequestResponse(
                default,
                [new OperationExecutionError(ExecutionErrorCodes.IpcTimeout, "Unity compile assurance timed out.", null)],
                HasFailureStatus: true))),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 10000));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, artifactStore.ReadCount);
        Assert.Equal(CompileVerdictValues.Fail, result.Output!.Verdict);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithStartupCompilerDiagnostic_ReturnsDiagnosticsReadFailurePacket ()
    {
        var artifactStore = new StubCompileRunArtifactReader();
        var service = CreateService(
            unityRequestExecutor: new StubUnityRequestExecutor(UnityRequestExecutionResult.Failure(new UnityRequestFailure(
                DaemonErrorCodes.DaemonStartupBlocked,
                "Unity startup was blocked by script compilation errors.",
                CreateCompilerStartupFailure()))),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 10000));

        Assert.True(result.IsSuccess);
        var output = result.Output!;
        Assert.Equal(CompileVerdictValues.Fail, output.Verdict);
        Assert.Equal("diagnosticsRead", output.Compile.Refresh.Origin);
        Assert.False(output.Compile.Refresh.Requested);
        Assert.Equal(1, output.Compile.ScriptCompilation.Diagnostics.ErrorCount);
        Assert.Equal("CS0246", output.Compile.ScriptCompilation.Diagnostics.PrimaryDiagnostic!.Code);
        Assert.Equal("compileFailed", output.Compile.Lifecycle.LifecycleState);
        Assert.Equal(0, artifactStore.ReadCount);
        Assert.Equal(1, artifactStore.WriteCount);
        Assert.Equal("diagnosticsRead", artifactStore.WrittenSummary!.Refresh.Origin);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithStartupCompilerDiagnosisWithoutPrimaryDiagnostic_ReturnsDiagnosticsReadFailurePacket ()
    {
        var artifactStore = new StubCompileRunArtifactReader();
        var service = CreateService(
            unityRequestExecutor: new StubUnityRequestExecutor(UnityRequestExecutionResult.Failure(new UnityRequestFailure(
                DaemonErrorCodes.DaemonStartupBlocked,
                "Unity startup was blocked by script compilation errors.",
                CreateStartupFailure(
                    DaemonDiagnosisReasonValues.UnityScriptCompilationFailed,
                    primaryDiagnostic: null)))),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 10000));

        Assert.True(result.IsSuccess);
        var diagnostic = result.Output!.Compile.ScriptCompilation.Diagnostics.PrimaryDiagnostic!;
        Assert.Equal("compiler", diagnostic.Kind);
        Assert.Null(diagnostic.Code);
        Assert.Contains("script compilation", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, artifactStore.ReadCount);
        Assert.Equal(1, artifactStore.WriteCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithNonCompilerStartupFailure_ReturnsFailureWithoutPollingArtifact ()
    {
        var artifactStore = new StubCompileRunArtifactReader();
        var service = CreateService(
            unityRequestExecutor: new StubUnityRequestExecutor(UnityRequestExecutionResult.Failure(new UnityRequestFailure(
                DaemonErrorCodes.DaemonStartupBlocked,
                "Unity startup was blocked by package resolution.",
                CreateStartupFailure(
                    DaemonDiagnosisReasonValues.UnityPackageResolutionFailed,
                    primaryDiagnostic: null)))),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 10000));

        Assert.False(result.IsSuccess);
        Assert.Equal(0, artifactStore.ReadCount);
        Assert.Equal(0, artifactStore.WriteCount);
        var error = Assert.Single(result.Errors);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, error.Code);
        Assert.NotNull(error.StartupFailure);
    }

    private static CompileService CreateService (
        IProjectContextResolver? projectContextResolver = null,
        IUnityExecutionModeDecisionService? modeDecisionService = null,
        IUnityRequestExecutor? unityRequestExecutor = null,
        ICompileRunIdFactory? runIdFactory = null,
        ICompileRunArtifactStore? artifactStore = null,
        TimeProvider? timeProvider = null)
    {
        return new CompileService(
            projectContextResolver ?? new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateProjectContext())),
            modeDecisionService ?? new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(new UnityExecutionModeDecision(
                UnityExecutionMode.Auto,
                DaemonRunning: false,
                UnityExecutionTarget.Oneshot,
                TimeSpan.FromSeconds(10)))),
            unityRequestExecutor ?? new StubUnityRequestExecutor(CreateCompileResponseResult(CreateSummary())),
            runIdFactory ?? new StubCompileRunIdFactory("run-1"),
            artifactStore ?? new StubCompileRunArtifactReader(),
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

    private static UnityRequestExecutionResult CreateCompileResponseResult (IpcCompileSummary summary)
    {
        return UnityRequestExecutionResult.Success(new UnityRequestResponse(
            IpcPayloadCodec.SerializeToElement(new IpcCompileResponse(summary.RunId, summary)),
            [],
            HasFailureStatus: false));
    }

    private static IpcCompileSummary CreateSummary (
        string runId = "run-1",
        string projectFingerprint = "project-fingerprint",
        int errorCount = 0)
    {
        var primaryDiagnostic = errorCount == 0
            ? null
            : new IpcPrimaryDiagnostic(
                Kind: "compiler",
                Code: "CS1002",
                File: "Assets/Broken.cs",
                Line: 4,
                Column: 16,
                Message: "; expected");
        var canAcceptExecutionRequests = errorCount == 0;
        return new IpcCompileSummary(
            RunId: runId,
            ProjectFingerprint: projectFingerprint,
            Completed: true,
            StartedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00Z"),
            CompletedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:02Z"),
            Refresh: new IpcCompileSummary.RefreshEvidence(
                Origin: "assetDatabaseRefresh",
                Requested: true,
                StartedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00Z"),
                CompletedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:02Z"),
                Completed: true),
            ScriptCompilation: new IpcCompileSummary.ScriptCompilationEvidence(
                Started: true,
                Completed: true,
                CompileGenerationBefore: "12",
                CompileGenerationAfter: "14",
                Diagnostics: new IpcCompileSummary.DiagnosticsEvidence(
                    ErrorCount: errorCount,
                    WarningCount: 0,
                    PrimaryDiagnostic: primaryDiagnostic)),
            DomainReload: new IpcCompileSummary.DomainReloadEvidence(
                ReloadRequired: false,
                ReloadObserved: false,
                GenerationBefore: "7",
                GenerationAfter: "7",
                Settled: true),
            Lifecycle: new IpcCompileSummary.LifecycleEvidence(
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

    private static StartupFailureDetail CreateCompilerStartupFailure ()
    {
        return CreateStartupFailure(
            DaemonDiagnosisReasonValues.UnityScriptCompilationFailed,
            new DaemonPrimaryDiagnosticOutput(
                Kind: DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler,
                Code: "CS0246",
                File: "Assets/Broken.cs",
                Line: 10,
                Column: 5,
                Message: "MissingType could not be found."));
    }

    private static StartupFailureDetail CreateStartupFailure (
        string reason,
        DaemonPrimaryDiagnosticOutput? primaryDiagnostic)
    {
        return new StartupFailureDetail(
            Startup: new DaemonStartupObservationOutput(
                StartupStatus: "blocked",
                StartupBlockingReason: string.Equals(reason, DaemonDiagnosisReasonValues.UnityScriptCompilationFailed, StringComparison.Ordinal)
                    ? "compile"
                    : "packageResolution",
                LaunchAttemptId: null,
                EditorMode: "batchmode",
                OwnerKind: "oneshot",
                CanShutdownProcess: null,
                ProcessId: 1234,
                StartedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00Z"),
                ElapsedMilliseconds: 1200,
                ProcessAction: "unknown",
                ProcessTermination: null,
                ArtifactPath: null,
                RetryDisposition: "manualActionRequired"),
            Diagnosis: new DaemonDiagnosisOutput(
                Reason: reason,
                Message: string.Equals(reason, DaemonDiagnosisReasonValues.UnityScriptCompilationFailed, StringComparison.Ordinal)
                    ? "Unity script compilation failed."
                    : "Unity package resolution failed.",
                ReportedBy: "unityLog",
                IsInferred: true,
                UpdatedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:02Z"),
                ProcessId: 1234,
                EditorInstancePath: null,
                ProcessStartedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00Z"),
                UnityLogPath: "/workspace/UnityProject/Logs/Editor.log",
                StartupPhase: "scriptCompilation",
                ActionRequired: string.Equals(reason, DaemonDiagnosisReasonValues.UnityScriptCompilationFailed, StringComparison.Ordinal)
                    ? DaemonDiagnosisActionRequiredValues.FixCompileErrors
                    : DaemonDiagnosisActionRequiredValues.ResolvePackages,
                PrimaryDiagnostic: primaryDiagnostic),
            RetryDisposition: "manualActionRequired",
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
            cancellationToken.ThrowIfCancellationRequested();
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
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubUnityRequestExecutor : IUnityRequestExecutor
    {
        private readonly UnityRequestExecutionResult result;

        public StubUnityRequestExecutor (UnityRequestExecutionResult result)
        {
            this.result = result;
        }

        public UnityRequestPayload? CapturedPayload { get; private set; }

        public ValueTask<UnityRequestExecutionResult> ExecuteAsync (
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            UcliConfig config,
            ResolvedUnityProjectContext unityProject,
            UnityRequestPayload payload,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CapturedPayload = payload;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubCompileRunIdFactory : ICompileRunIdFactory
    {
        private readonly string runId;

        public StubCompileRunIdFactory (string runId)
        {
            this.runId = runId;
        }

        public string Create ()
        {
            return runId;
        }
    }

    private sealed class StubCompileRunArtifactReader : ICompileRunArtifactStore
    {
        private readonly Queue<CompileRunArtifactReadResult> results;

        public StubCompileRunArtifactReader (params CompileRunArtifactReadResult[] results)
        {
            this.results = new Queue<CompileRunArtifactReadResult>(results);
        }

        public int ReadCount { get; private set; }

        public int WriteCount { get; private set; }

        public IpcCompileSummary? WrittenSummary { get; private set; }

        public ValueTask<CompileRunArtifactReadResult> ReadSummaryAsync (
            ResolvedUnityProjectContext unityProject,
            string runId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadCount++;
            return ValueTask.FromResult(results.Count == 0
                ? CompileRunArtifactReadResult.Missing()
                : results.Dequeue());
        }

        public ValueTask<ExecutionError?> WriteArtifactsAsync (
            ResolvedUnityProjectContext unityProject,
            string runId,
            IpcCompileSummary summary,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteCount++;
            WrittenSummary = summary;
            return ValueTask.FromResult<ExecutionError?>(null);
        }

        public string ResolveSummaryPath (
            ResolvedUnityProjectContext unityProject,
            string runId)
        {
            return "/workspace/.ucli/local/compile/run-1/summary.json";
        }

        public string ResolveDiagnosticsPath (
            ResolvedUnityProjectContext unityProject,
            string runId)
        {
            return "/workspace/.ucli/local/compile/run-1/diagnostics.json";
        }
    }
}
