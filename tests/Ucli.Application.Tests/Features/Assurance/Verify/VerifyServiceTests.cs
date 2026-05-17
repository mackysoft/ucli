using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Vocabulary;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Execution;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Input;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Profiles;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Testing.Run.Common.Contracts;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Verify;

public sealed class VerifyServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithBuiltInMutationProfile_DoesNotExecuteCompile ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithBuiltInMutationProfile_DoesNotExecuteCompile));
        var compileService = new StubCompileService(_ => throw new InvalidOperationException("Compile must not execute."));
        var service = CreateService(scope.FullPath, compileService: compileService);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: "built-in:mutation",
            ProfilePath: null,
            FromPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, compileService.ExecuteCount);
        Assert.DoesNotContain(result.Output!.Verifiers, static verifier => string.Equals(verifier.Kind, VerifyStepKindValues.Compile, StringComparison.Ordinal));
        Assert.DoesNotContain(
            result.Output.Verifiers.SelectMany(static verifier => verifier.Effects),
            static effect => VerifyEffectValues.Compile.Contains(effect, StringComparer.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithProfileAndProfilePath_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithProfileAndProfilePath_ReturnsInvalidArgument));
        var service = CreateService(scope.FullPath);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: "built-in:default",
            ProfilePath: "verify.json",
            FromPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, Assert.Single(result.Errors).Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithFromFingerprintMismatch_ReturnsProjectFingerprintMismatch ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithFromFingerprintMismatch_ReturnsProjectFingerprintMismatch));
        var fromPath = scope.WriteFile("from.json", CreateFromJson("other-fingerprint", coverageImpact: "none"));
        var readyService = new StubReadyService(input => CreateReadyResult(input.Target, CreateProjectIdentity(scope.FullPath)));
        var service = CreateService(scope.FullPath, readyService: readyService);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: "built-in:mutation",
            ProfilePath: null,
            FromPath: fromPath,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        Assert.False(result.IsSuccess);
        Assert.Equal(VerifyErrorCodes.ProjectFingerprintMismatch, Assert.Single(result.Errors).Code);
        Assert.Equal(0, readyService.ExecuteCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithPostReadPartialDiagnostics_ReturnsPartialOptionalClaim ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithPostReadPartialDiagnostics_ReturnsPartialOptionalClaim));
        var fromPath = scope.WriteFile("from.json", CreateFromJson("project-fingerprint", coverageImpact: "partial"));
        var logsService = new StubLogsUnityService(LogsReadServiceResult.Success());
        var service = CreateService(scope.FullPath, logsService: logsService);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: "built-in:mutation",
            ProfilePath: null,
            FromPath: fromPath,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        Assert.True(result.IsSuccess);
        Assert.Equal(VerifyVerdictValues.Pass, result.Output!.Verdict);
        var claim = Assert.Single(result.Output.Claims, static claim => string.Equals(claim.Id, VerifyClaimCodes.ReadSurfaceSafe.Value, StringComparison.Ordinal));
        Assert.False(claim.Required);
        Assert.Equal(VerifyClaimStatusValues.Passed, claim.Status);
        Assert.Equal(VerifyCoverageValues.Partial, claim.Coverage);
        Assert.Equal(1, logsService.ExecuteCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithFileProfileTestPass_MapsUnityTestClaimAndReport ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithFileProfileTestPass_MapsUnityTestClaimAndReport));
        scope.WriteFile(
            "verify.json",
            """
            {
              "schemaVersion": 1,
              "name": "test-profile",
              "steps": [
                {
                  "kind": "test",
                  "required": true,
                  "effects": [
                    "unityTestRunner"
                  ],
                  "testPlatform": "editmode"
                }
              ]
            }
            """);
        var testRunService = new StubTestRunService(_ => TestRunServiceResult.Pass(
            "Tests passed.",
            "test-run-1",
            "/repo/.ucli/local/test/test-run-1",
            "/repo/.ucli/local/test/test-run-1/summary.json"));
        var service = CreateService(scope.FullPath, testRunService: testRunService);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: null,
            ProfilePath: "verify.json",
            FromPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        Assert.True(result.IsSuccess);
        Assert.Equal(VerifyVerdictValues.Pass, result.Output!.Verdict);
        Assert.Equal(1, testRunService.ExecuteCount);
        Assert.Equal(TestRunPlatform.EditMode, testRunService.CapturedInput!.TestPlatform);
        Assert.True(result.Output.Reports.ContainsKey("test.summary"));
        var claim = Assert.Single(result.Output.Claims);
        Assert.Equal(VerifyClaimCodes.UnityTestsPassed.Value, claim.Id);
        Assert.Equal(VerifyClaimStatusValues.Passed, claim.Status);
        Assert.True(claim.Required);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithFileProfileTestFail_MapsFailedClaimWithoutCommandError ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithFileProfileTestFail_MapsFailedClaimWithoutCommandError));
        scope.WriteFile(
            "verify.json",
            """
            {
              "schemaVersion": 1,
              "name": "test-profile",
              "steps": [
                {
                  "kind": "test",
                  "required": true
                }
              ]
            }
            """);
        var testRunService = new StubTestRunService(_ => TestRunServiceResult.Fail(
            "Tests failed.",
            "test-run-1",
            "/repo/.ucli/local/test/test-run-1",
            "/repo/.ucli/local/test/test-run-1/summary.json"));
        var service = CreateService(scope.FullPath, testRunService: testRunService);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: null,
            ProfilePath: "verify.json",
            FromPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        Assert.True(result.IsSuccess);
        Assert.Equal(VerifyVerdictValues.Fail, result.Output!.Verdict);
        var claim = Assert.Single(result.Output.Claims);
        Assert.Equal(VerifyClaimCodes.UnityTestsPassed.Value, claim.Id);
        Assert.Equal(VerifyClaimStatusValues.Failed, claim.Status);
        Assert.True(claim.Required);
    }

    private static VerifyService CreateService (
        string repositoryRoot,
        StubReadyService? readyService = null,
        StubCompileService? compileService = null,
        StubTestRunService? testRunService = null,
        StubLogsUnityService? logsService = null,
        StubProfileFileReader? profileFileReader = null,
        StubFromInputFileReader? fromInputFileReader = null)
    {
        var project = CreateProjectIdentity(repositoryRoot);
        return new VerifyService(
            new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateProjectContext(repositoryRoot))),
            readyService ?? new StubReadyService(input => CreateReadyResult(input.Target, project)),
            compileService ?? new StubCompileService(_ => CreateCompileResult(project)),
            testRunService ?? new StubTestRunService(_ => TestRunServiceResult.Pass(
                "Tests passed.",
                "test-run-1",
                "/repo/.ucli/local/test/test-run-1",
                "/repo/.ucli/local/test/test-run-1/summary.json")),
            logsService ?? new StubLogsUnityService(LogsReadServiceResult.Success()),
            profileFileReader ?? new StubProfileFileReader((profilePath, root) => VerifyProfileFileReadResult.Success(
                File.ReadAllText(Path.Combine(root, profilePath)),
                profilePath.Replace('\\', '/'))),
            fromInputFileReader ?? new StubFromInputFileReader((fromPath, root) => VerifyFromInputFileReadResult.Success(
                File.ReadAllText(Path.Combine(root, fromPath)))));
    }

    private static ProjectContext CreateProjectContext (string repositoryRoot)
    {
        var unityProject = new ResolvedUnityProjectContext(
            UnityProjectRoot: Path.Combine(repositoryRoot, "UnityProject"),
            RepositoryRoot: repositoryRoot,
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption,
            PathSourceLabel: "--projectPath",
            UnityVersion: "6000.1.4f1");
        return new ProjectContext(
            unityProject,
            UcliConfig.CreateDefault(),
            ConfigSource.Default);
    }

    private static ProjectIdentityInfo CreateProjectIdentity (string repositoryRoot)
    {
        return new ProjectIdentityInfo(
            ProjectPath: Path.Combine(repositoryRoot, "UnityProject"),
            ProjectFingerprint: "project-fingerprint",
            UnityVersion: "6000.1.4f1");
    }

    private static ReadyExecutionResult CreateReadyResult (
        ReadyTarget target,
        ProjectIdentityInfo project)
    {
        var claimCode = ReadyClaimCodes.ForTarget(target).Value;
        var verifierId = "ready.lifecycle";
        return ReadyExecutionResult.Success(new ReadyExecutionOutput(
            Verdict: ReadyVerdictValues.Pass,
            Project: project,
            Verifiers:
            [
                new ReadyVerifierOutput(
                    Id: verifierId,
                    Kind: verifierId,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: [claimCode],
                    Effects: [])
            ],
            Claims:
            [
                new ReadyClaimOutput(
                    Id: claimCode,
                    Status: ReadyClaimStatusValues.Passed,
                    Coverage: ReadyCoverageValues.Full,
                    Required: true,
                    VerifierRef: verifierId,
                    Statement: "Unity is ready.",
                    Subject: new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["target"] = ReadyTargetCodec.ToValue(target),
                    },
                    Validity: new ReadyClaimValidityOutput(
                        ReadyValidityKindValues.ProbeOnly,
                        GuaranteesReusableSession: false),
                    Evidence: [],
                    ResidualRisks: [])
            ],
            Reports: new Dictionary<string, ReadyReportOutput>(StringComparer.Ordinal),
            ResidualRisks: [],
            Target: ReadyTargetCodec.ToValue(target),
            RequestedMode: AssuranceExecutionModeCodec.Auto,
            ResolvedMode: AssuranceExecutionModeCodec.Oneshot,
            SessionKind: AssuranceSessionKindValues.TransientProbe,
            TimeoutMilliseconds: 10000));
    }

    private static CompileExecutionResult CreateCompileResult (ProjectIdentityInfo project)
    {
        return CompileExecutionResult.Success(new CompileExecutionOutput(
            Verdict: CompileVerdictValues.Pass,
            Project: project,
            Verifiers:
            [
                new CompileVerifierOutput(
                    Id: "compile",
                    Kind: "compile",
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: [CompileClaimCodes.UnityCompileNoErrors.Value],
                    Effects: CompileEffectValues.All,
                    ReportRef: "compile.summary")
            ],
            Claims:
            [
                new CompileClaimOutput(
                    Id: CompileClaimCodes.UnityCompileNoErrors.Value,
                    Status: CompileClaimStatusValues.Passed,
                    Coverage: CompileCoverageValues.Full,
                    Required: true,
                    VerifierRef: "compile",
                    Statement: "Unity script compilation has no errors.",
                    Subject: new Dictionary<string, object?>(StringComparer.Ordinal),
                    Evidence: [],
                    ResidualRisks: [])
            ],
            Reports: new Dictionary<string, CompileReportOutput>(StringComparer.Ordinal)
            {
                ["compile.summary"] = new CompileReportOutput("compileSummary", "/repo/.ucli/local/compile/run-1/summary.json"),
            },
            ResidualRisks: [],
            RequestedMode: AssuranceExecutionModeCodec.Auto,
            ResolvedMode: AssuranceExecutionModeCodec.Oneshot,
            SessionKind: AssuranceSessionKindValues.TransientProbe,
            TimeoutMilliseconds: 10000,
            Compile: new CompileOutput(
                RunId: "compile-run-1",
                Refresh: new CompileRefreshOutput(
                    Origin: CompileEffectValues.AssetDatabaseRefresh,
                    Requested: true,
                    StartedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00Z"),
                    CompletedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:01Z"),
                    Completed: true),
                ScriptCompilation: new CompileScriptCompilationOutput(
                    Started: true,
                    Completed: true,
                    CompileGenerationBefore: "1",
                    CompileGenerationAfter: "2",
                    Diagnostics: new CompileDiagnosticsOutput(
                        ErrorCount: 0,
                        WarningCount: 0,
                        PrimaryDiagnostic: null)),
                DomainReload: new CompileDomainReloadOutput(
                    ReloadRequired: false,
                    ReloadObserved: false,
                    GenerationBefore: "1",
                    GenerationAfter: "1",
                    Settled: true),
                Lifecycle: new CompileLifecycleOutput(
                    ServerVersion: "0.5.0",
                    UnityVersion: "6000.1.4f1",
                    EditorMode: "batchmode",
                    LifecycleState: "ready",
                    BlockingReason: null,
                    CompileState: "ready",
                    CompileGeneration: "2",
                    DomainReloadGeneration: "1",
                    CanAcceptExecutionRequests: true,
                    ObservedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:02Z"),
                    ActionRequired: null,
                    PrimaryDiagnostic: null))));
    }

    private static string CreateFromJson (
        string projectFingerprint,
        string coverageImpact)
    {
        return $$"""
        {
          "protocolVersion": 1,
          "command": "call",
          "status": "ok",
          "payload": {
            "project": {
              "projectPath": "/repo/UnityProject",
              "projectFingerprint": "{{projectFingerprint}}",
              "unityVersion": "6000.1.4f1"
            },
            "opResults": [
              {
                "opId": "op-1",
                "op": "Scene.Touch",
                "applied": true,
                "changed": true,
                "touched": [
                  {
                    "kind": "asset",
                    "path": "Assets/Scene.unity"
                  }
                ],
                "diagnostics": [
                  {
                    "code": "READ_SURFACE_PARTIAL",
                    "coverageImpact": "{{coverageImpact}}",
                    "message": "Read surface coverage is partial."
                  }
                ]
              }
            ],
            "readPostcondition": {
              "requirements": [
                {
                  "kind": "scene"
                }
              ]
            }
          },
          "errors": []
        }
        """;
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

    private sealed class StubReadyService : IReadyService
    {
        private readonly Func<ReadyCommandInput, ReadyExecutionResult> resultFactory;

        public StubReadyService (Func<ReadyCommandInput, ReadyExecutionResult> resultFactory)
        {
            this.resultFactory = resultFactory;
        }

        public int ExecuteCount { get; private set; }

        public ValueTask<ReadyExecutionResult> ExecuteAsync (
            ReadyCommandInput input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecuteCount++;
            return ValueTask.FromResult(resultFactory(input));
        }
    }

    private sealed class StubCompileService : ICompileService
    {
        private readonly Func<CompileCommandInput, CompileExecutionResult> resultFactory;

        public StubCompileService (Func<CompileCommandInput, CompileExecutionResult> resultFactory)
        {
            this.resultFactory = resultFactory;
        }

        public int ExecuteCount { get; private set; }

        public ValueTask<CompileExecutionResult> ExecuteAsync (
            CompileCommandInput input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecuteCount++;
            return ValueTask.FromResult(resultFactory(input));
        }
    }

    private sealed class StubTestRunService : ITestRunService
    {
        private readonly Func<TestRunCommandInput, TestRunServiceResult> resultFactory;

        public StubTestRunService (Func<TestRunCommandInput, TestRunServiceResult> resultFactory)
        {
            this.resultFactory = resultFactory;
        }

        public int ExecuteCount { get; private set; }

        public TestRunCommandInput? CapturedInput { get; private set; }

        public ValueTask<TestRunServiceResult> ExecuteAsync (
            TestRunCommandInput input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecuteCount++;
            CapturedInput = input;
            return ValueTask.FromResult(resultFactory(input));
        }
    }

    private sealed class StubLogsUnityService : ILogsUnityService
    {
        private readonly LogsReadServiceResult result;

        public StubLogsUnityService (LogsReadServiceResult result)
        {
            this.result = result;
        }

        public int ExecuteCount { get; private set; }

        public ValueTask<LogsReadServiceResult> ExecuteAsync (
            LogsUnityServiceRequest request,
            Func<IpcUnityLogEvent, string, CancellationToken, ValueTask> onEvent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecuteCount++;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubProfileFileReader : IVerifyProfileFileReader
    {
        private readonly Func<string, string, VerifyProfileFileReadResult> resultFactory;

        public StubProfileFileReader (Func<string, string, VerifyProfileFileReadResult> resultFactory)
        {
            this.resultFactory = resultFactory;
        }

        public ValueTask<VerifyProfileFileReadResult> ReadAsync (
            string profilePath,
            string repositoryRoot,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(resultFactory(profilePath, repositoryRoot));
        }
    }

    private sealed class StubFromInputFileReader : IVerifyFromInputFileReader
    {
        private readonly Func<string, string, VerifyFromInputFileReadResult> resultFactory;

        public StubFromInputFileReader (Func<string, string, VerifyFromInputFileReadResult> resultFactory)
        {
            this.resultFactory = resultFactory;
        }

        public ValueTask<VerifyFromInputFileReadResult> ReadAsync (
            string fromPath,
            string repositoryRoot,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(resultFactory(fromPath, repositoryRoot));
        }
    }
}
