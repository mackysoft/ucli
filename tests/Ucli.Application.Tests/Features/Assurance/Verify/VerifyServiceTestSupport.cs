using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Vocabulary;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Execution;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Input;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Profiles;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Tests.Features.Assurance.Payload;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Verify;

internal static class VerifyServiceTestSupport
{
    public static readonly Guid CompileExecutionId =
        Guid.Parse("34c0c330-8798-4ec1-87ae-3d0ae87fc715");
    public static readonly Guid TestRunId = Guid.Parse("83ca6714-565c-4c9d-a3ca-44446393afca");

    private static string ProjectPathJson { get; } = JsonSerializer.Serialize(ProjectPathTestValues.RepositoryUnityProject);

    public static VerifyService CreateService (
        string repositoryRoot,
        RecordingVerifyReadyService? readyService = null,
        RecordingVerifyCompileService? compileService = null,
        RecordingVerifyTestRunService? testRunService = null,
        RecordingVerifyLogsUnityService? logsService = null,
        StubVerifyProfileFileReader? profileFileReader = null,
        StubVerifyFromInputFileReader? fromInputFileReader = null,
        TimeProvider? timeProvider = null)
    {
        var project = ProjectIdentityInfoTestFactory.CreateForRepositoryRoot(repositoryRoot);
        var clock = timeProvider ?? TimeProvider.System;
        var projectContext = ProjectContextTestFactory.CreateWithPaths(
            unityProjectRoot: Path.Combine(repositoryRoot, "UnityProject"),
            repositoryRoot: repositoryRoot);
        var testRunArtifactsDirectory = AbsolutePath.Parse(Path.Combine(
            project.ProjectPath,
            ".ucli",
            "local",
            "test",
            "test-run-1"));
        return new VerifyService(
            new StaticProjectContextResolver(ProjectContextResolutionResult.Success(projectContext)),
            readyService ?? new RecordingVerifyReadyService(input => CreateReadyResult(input.Target, project)),
            compileService ?? new RecordingVerifyCompileService(_ => CreateCompileResult(project)),
            new VerifyInvocationFactory(projectContext, clock),
            testRunService ?? new RecordingVerifyTestRunService(_ => TestRunResultTestValues.CreateCompleted(
                Verdict.Pass,
                TestArtifactPaths.CreateSession(TestRunId, testRunArtifactsDirectory.Value))),
            logsService ?? new RecordingVerifyLogsUnityService((_, _, _) => ValueTask.FromResult(LogsReadServiceResult.Completed(0, null))),
            profileFileReader ?? new StubVerifyProfileFileReader((profilePath, root) =>
            {
                var resolvedPath = ContainedPath.Resolve(root, profilePath);
                return VerifyProfileFileReadResult.Success(
                    File.ReadAllText(resolvedPath.Target.Value),
                    profilePath);
            }),
            fromInputFileReader ?? new StubVerifyFromInputFileReader((fromPath, root) => VerifyFromInputFileReadResult.Success(
                File.ReadAllText(Path.Combine(root.Value, fromPath)))),
            clock);
    }

    public static void WriteRequiredPostReadProfile (TestDirectoryScope scope)
    {
        scope.WriteFile(
            "verify.json",
            """
            {
              "schemaVersion": 1,
              "steps": [
                {
                  "kind": "postRead",
                  "required": true
                }
              ]
            }
            """);
    }

    public static ReadyExecutionResult CreateReadyResult (
        ReadyTarget target,
        ProjectIdentityInfo project)
    {
        var claimCode = ReadyClaimCodes.ForTarget(target);
        var verifierId = new AssuranceVerifierId("ready.lifecycle");
        var lifecycle = AssuranceExecutionOutputTestFactory.CreateReadyLifecycleOutput();
        return ReadyExecutionResult.Completed(new ReadyExecutionOutput(
            Project: project,
            Verifiers:
            [
                new ReadyVerifierOutput(
                    Id: verifierId,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: [claimCode])
            ],
            Claims:
            [
                new ReadyClaimOutput(
                    Id: claimCode,
                    Status: AssuranceClaimStatus.Passed,
                    Coverage: AssuranceCoverage.Full,
                    Required: true,
                    VerifierRef: verifierId,
                    Statement: "Unity is ready.",
                    Subject: new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["target"] = TextVocabulary.GetText(target),
                    },
                    Validity: ReadyClaimValidityOutput.ProbeOnly(),
                    Evidence:
                    [
                        ReadyLifecycleEvidenceOutput.Create(lifecycle),
                    ],
                    ResidualRisks: [])
            ],
            Reports: new Dictionary<string, AssuranceReportReference>(StringComparer.Ordinal),
            ResidualRisks: [],
            Target: target,
            RequestedMode: AssuranceRequestedExecutionMode.Auto,
            ResolvedMode: AssuranceResolvedExecutionMode.Oneshot,
            SessionKind: AssuranceSessionKind.TransientProbe,
            TimeoutMilliseconds: 10000,
            Lifecycle: lifecycle,
            ReadIndex: null));
    }

    public static CompileExecutionResult CreateCompileResult (ProjectIdentityInfo project)
    {
        return CreateCompileResult(project, AssuranceClaimStatus.Passed);
    }

    public static CompileExecutionResult CreateCompileResult (
        ProjectIdentityInfo project,
        AssuranceClaimStatus claimStatus)
    {
        var failed = claimStatus == AssuranceClaimStatus.Failed;
        var lifecycleExecutionRef =
            AssuranceExecutionOutputTestFactory.CreateCompileExecutionRef(
                CompileExecutionId);
        var terminalRecordRef =
            (PathArtifactRef)lifecycleExecutionRef.TerminalRecordRef;
        var terminalRecordReport = AssuranceReportReference.FromPath(
            terminalRecordRef.Path.Value,
            terminalRecordRef.Digest);
        var scriptCompilation = new CompileScriptCompilationOutput(
            Started: true,
            Completed: true,
            CompileGenerationBefore: 1,
            CompileGenerationAfter: 2,
            Diagnostics: new CompileDiagnosticsOutput(
                ErrorCount: failed ? 1 : 0,
                WarningCount: 0,
                PrimaryDiagnostic: null));
        return CompileExecutionResult.Completed(new CompileExecutionOutput(
            Project: project,
            LifecycleExecutionRef: lifecycleExecutionRef,
            Verdict: failed
                ? Verdict.Fail
                : Verdict.Pass,
            Verifiers:
            [
                new CompileVerifierOutput(
                    Id: new AssuranceVerifierId("compile"),
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: [CompileClaimCodes.UnityCompileNoErrors],
                    Effects: AssuranceEffectSets.Compile,
                    ReportRef: AssuranceReportIds.CompileSummary)
            ],
            Claims:
            [
                new CompileClaimOutput(
                    Id: CompileClaimCodes.UnityCompileNoErrors,
                    Status: claimStatus,
                    Coverage: AssuranceCoverage.Full,
                    Required: true,
                    VerifierRef: new AssuranceVerifierId("compile"),
                    Statement: "Unity script compilation has no errors.",
                    Subject: new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["kind"] = "unityCompile",
                        ["executionId"] = CompileExecutionId,
                    },
                    Evidence:
                    [
                        CompileScriptEvidenceOutput.Create(
                            AssuranceReportIds.CompileDiagnostics,
                            scriptCompilation),
                    ],
                    ResidualRisks: [])
            ],
            Reports: new Dictionary<string, AssuranceReportReference>(StringComparer.Ordinal)
            {
                [AssuranceReportIds.CompileSummary.Value] = terminalRecordReport,
                [AssuranceReportIds.CompileDiagnostics.Value] = terminalRecordReport,
            },
            ResidualRisks: [],
            Compile: new CompileOutput(
                refresh: new CompileRefreshOutput(
                    Origin: CompileLifecycleRefreshOrigin.AssetDatabaseRefresh,
                    Requested: true,
                    StartedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00Z"),
                    CompletedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:01Z"),
                    Completed: true),
                scriptCompilation: scriptCompilation,
                domainReload: new CompileDomainReloadOutput(
                    ReloadRequired: false,
                    ReloadObserved: false,
                    GenerationBefore: 1,
                    GenerationAfter: 1,
                    Settled: true),
                lifecycle: new CompileLifecycleOutput(
                    ServerVersion: "0.5.0",
                    UnityVersion: "6000.1.4f1",
                    EditorMode: UnityEditorMode.Batchmode,
                    LifecycleState: UnityEditorLifecycleState.Ready,
                    BlockingReason: null,
                    CompileState: UnityEditorCompileState.Ready,
                    Generations: new UnityEditorGenerationSnapshot(
                        CompileGeneration: 2,
                        DomainReloadGeneration: 1,
                        AssetRefreshGeneration: 1,
                        PlayModeGeneration: 1),
                    CanAcceptExecutionRequests: true,
                    ObservedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:02Z"),
                    ActionRequired: null,
                    PrimaryDiagnostic: null))));
    }

    public static string CreateFromJson (
        ProjectFingerprint projectFingerprint,
        string coverageImpact,
        string severity = "warning",
        bool applied = true,
        bool changed = true,
        string touchedJson = """
                  [
                    {
                      "kind": "asset",
                      "path": "Assets/Scene.unity"
                    }
                  ]
            """,
        string sourceKind = "edit",
        string? commit = "context",
        bool persistenceExpected = true,
        string expectedPostState = "deterministic",
        bool includeReadPostcondition = true,
        string op = "edit")
    {
        var projectFingerprintText = projectFingerprint.ToString();
        var commitJson = commit is null ? "null" : $"\"{commit}\"";
        var readPostconditionJson = includeReadPostcondition
            ? """
            ,
            "readPostcondition": {
              "requirements": [
                {
                  "surface": "sceneTreeLite",
                  "minSafeGeneratedAtUtc": "2026-05-17T00:00:00+00:00"
                }
              ]
            }
            """
            : string.Empty;
        return $$"""
        {
          "protocolVersion": 1,
          "status": "ok",
          "exitCode": 0,
          "command": "call",
          "payload": {
            "project": {
              "projectPath": {{ProjectPathJson}},
              "projectFingerprint": "{{projectFingerprintText}}",
              "unityVersion": "6000.1.4f1"
            },
            "opResults": [
              {
                  "op": "{{op}}",
                  "phase": "call",
                  "applied": {{JsonSerializer.Serialize(applied)}},
                  "changed": {{JsonSerializer.Serialize(changed)}},
                "touched": {{touchedJson}},
                "diagnostics": [
                    {
                      "code": "READ_SURFACE_PARTIAL",
                      "severity": "{{severity}}",
                      "coverageImpact": "{{coverageImpact}}",
                      "message": "Read surface coverage is partial."
                  }
                ]
              }
            ],
            "postReadSource": {
              "schemaVersion": 1,
              "steps": [
                {
                  "sourceKind": "{{sourceKind}}",
                  "playModeMutation": false,
                  "commit": {{commitJson}},
                  "persistenceExpected": {{JsonSerializer.Serialize(persistenceExpected)}},
                  "expectedPostState": "{{expectedPostState}}"
                }
              ]
            }{{readPostconditionJson}}
          },
          "errors": []
        }
        """;
    }

    public static string CreateNoOpFromJson (ProjectFingerprint projectFingerprint)
    {
        var projectFingerprintText = projectFingerprint.ToString();
        return $$"""
        {
          "protocolVersion": 1,
          "status": "ok",
          "exitCode": 0,
          "command": "call",
          "payload": {
            "project": {
              "projectPath": {{ProjectPathJson}},
              "projectFingerprint": "{{projectFingerprintText}}",
              "unityVersion": "6000.1.4f1"
            },
            "opResults": [],
            "postReadSource": {
              "schemaVersion": 1,
              "steps": []
            }
          },
          "errors": []
        }
        """;
    }

    public static string CreateMixedBoundAndUnboundDiagnosticFromJson (ProjectFingerprint projectFingerprint)
    {
        var projectFingerprintText = projectFingerprint.ToString();
        return $$"""
        {
          "protocolVersion": 1,
          "status": "ok",
          "exitCode": 0,
          "command": "call",
          "payload": {
            "project": {
              "projectPath": {{ProjectPathJson}},
              "projectFingerprint": "{{projectFingerprintText}}",
              "unityVersion": "6000.1.4f1"
            },
            "opResults": [
              {
                "op": "edit",
                "phase": "call",
                "applied": true,
                "changed": true,
                "touched": [],
                "diagnostics": []
              },
              {
                "op": "ucli.scene.open",
                "phase": "call",
                "applied": true,
                "changed": true,
                "touched": [],
                "diagnostics": [
                  {
                    "code": "READ_SURFACE_PARTIAL",
                    "severity": "warning",
                    "coverageImpact": "partial",
                    "message": "Read surface coverage is partial."
                  }
                ]
              }
            ],
            "postReadSource": {
              "schemaVersion": 1,
              "steps": [
                {
                  "sourceKind": "edit",
                  "playModeMutation": false,
                  "commit": "none",
                  "persistenceExpected": false,
                  "expectedPostState": "deterministic"
                },
                {
                  "sourceKind": "operation",
                  "playModeMutation": false,
                  "commit": null,
                  "persistenceExpected": false,
                  "expectedPostState": "unavailable"
                }
              ]
            }
          },
          "errors": []
        }
        """;
    }

    public static IpcUnityLogEvent CreateLogEvent (long sequence)
    {
        return new IpcUnityLogEvent(
            Timestamp: new DateTimeOffset(2026, 5, 17, 0, 0, 0, TimeSpan.Zero),
            Level: IpcLogLevel.Error,
            Source: IpcUnityLogSource.Runtime,
            Message: "Unity log event.",
            StackTrace: null,
            Cursor: IpcLogCursor.Create(
                Guid.Parse("abcdef01-2345-6789-abcd-ef0123456789"),
                sequence));
    }

}

internal sealed class VerifyInvocationFactory : ILifecycleExecutionStartInvocationFactory
{
    private readonly ProjectContext projectContext;
    private readonly TimeProvider timeProvider;

    public VerifyInvocationFactory (ProjectContext projectContext, TimeProvider timeProvider)
    {
        this.projectContext = projectContext;
        this.timeProvider = timeProvider;
    }

    public ValueTask<LifecycleExecutionStartInvocationPreparation> CreateAsync (
        string? projectPath,
        UnityExecutionMode requestedMode,
        int? timeoutMilliseconds,
        UcliCommand command,
        bool decideMode,
        CancellationToken cancellationToken = default)
    {
        var deadline = ExecutionDeadline.Start(TimeSpan.FromMilliseconds(timeoutMilliseconds!.Value), timeProvider);
        var invocation = new LifecycleExecutionStartInvocation(
            new LifecycleExecutionFixedContext(
                projectContext,
                requestedMode,
                new VerifyHostBinding(projectContext.UnityProject)),
            deadline,
            deadline.CreateCompletionDeadline(LifecycleExecutionTiming.ResponseDeliveryGrace),
            NullLifecycleExecutionStartObserver.Instance);
        return ValueTask.FromResult(LifecycleExecutionStartInvocationPreparation.Success(invocation));
    }

    private sealed class VerifyHostBinding : IUnityExecutionHostBinding
    {
        public VerifyHostBinding (ResolvedUnityProjectContext project)
        {
            Project = project;
        }

        public ResolvedUnityProjectContext Project { get; }

        public UnityExecutionTarget Target => UnityExecutionTarget.Daemon;

        public ValueTask<UnityRequestExecutionResult> StartAsync (UcliCommand command, UnityRequestPayload payload, LifecycleExecutionStartInvocation invocation, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public ValueTask<UnityRequestExecutionResult> ReconnectAsync (UcliCommand command, UnityRequestPayload payload, LifecycleExecutionReconnectInvocation invocation, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }
}
