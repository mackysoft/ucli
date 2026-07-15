using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Catalog;
using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Execution;
using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Application.Features.Assurance.Build.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.EnvironmentVariables;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using CodeCatalogModel = MackySoft.Ucli.Application.Features.CodeCatalog.Catalog.CodeCatalog;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

internal static class BuildServiceTestSupport
{
    public static readonly Guid RunId = Guid.Parse("b7516435-a107-4dc1-a10b-f72ec743d297");
    public static readonly ProjectFingerprint DefaultProjectFingerprint = ProjectFingerprintTestFactory.Create("project-fingerprint");

    public const string ProfileJson = """
        {
          "schemaVersion": 1,
          "inputs": {
            "kind": "explicit",
            "buildTarget": "standaloneLinux64",
            "scenes": {
              "source": "explicit",
              "paths": [
                "Assets/Scenes/Main.unity"
              ]
            },
            "options": {
              "development": true
            }
          },
          "runner": {
            "kind": "buildPipeline"
          },
          "policy": {
            "runtime": {
              "allowedExecutionModes": [
                "daemon",
                "oneshot"
              ],
              "allowedEditorModes": [
                "batchmode",
                "gui"
              ]
            },
            "projectMutationMode": "forbid"
          }
        }
        """;

    public const string UnityBuildProfileJson = """
        {
          "schemaVersion": 1,
          "inputs": {
            "kind": "unityBuildProfile",
            "path": "Assets/BuildProfiles/Linux.asset"
          },
          "runner": {
            "kind": "buildPipeline"
          },
          "policy": {
            "runtime": {
              "allowedExecutionModes": [
                "daemon",
                "oneshot"
              ],
              "allowedEditorModes": [
                "batchmode",
                "gui"
              ]
            },
            "projectMutationMode": "forbid"
          }
        }
        """;

    public static readonly JsonSerializerOptions PayloadSerializerOptions = new()
    {
        Converters =
        {
            new ContractLiteralJsonConverterFactory(),
        },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static readonly AssuranceEffect[] BuildPipelineEffectValues =
    [
        AssuranceEffect.UnityLifecycleRead,
        AssuranceEffect.UnityBuildPipeline,
        AssuranceEffect.UnityBuildReportRead,
        AssuranceEffect.UnityLogWindowRead,
        AssuranceEffect.UcliArtifactWrite,
        AssuranceEffect.OutputManifestWrite,
        AssuranceEffect.GenerationSnapshot,
        AssuranceEffect.ProjectMutationAudit,
    ];

    public static BuildService CreateService (
        IBuildRunArtifactStore artifactStore,
        IProjectContextResolver? projectContextResolver = null,
        IBuildProfileFileReader? profileFileReader = null,
        IEnvironmentVariableReader? environmentVariableReader = null,
        IUnityExecutionModeDecisionService? modeDecisionService = null,
        IUnityRequestExecutor? requestExecutor = null,
        IUnityStreamingRequestExecutor? streamingRequestExecutor = null,
        IGuidGenerator? runIdGenerator = null,
        TimeProvider? timeProvider = null)
    {
        var resolvedRequestExecutor = requestExecutor ?? CreateBuildResponseExecutor(
            IpcBuildReportResult.Succeeded,
            IpcBuildLogCompletionReason.Completed,
            errorCount: 0);
        var resolvedStreamingRequestExecutor = streamingRequestExecutor
            ?? resolvedRequestExecutor as IUnityStreamingRequestExecutor
            ?? throw new InvalidOperationException("BuildService tests require a streaming request executor.");
        return new BuildService(
            projectContextResolver ?? new StaticProjectContextResolver(ProjectContextResolutionResult.Success(ProjectContextTestFactory.Create(
                projectFingerprint: DefaultProjectFingerprint))),
            profileFileReader ?? new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(ProfileJson, "/workspace/build.ucli.json")),
            environmentVariableReader ?? new StubEnvironmentVariableReader(),
            modeDecisionService ?? new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(new UnityExecutionModeDecision(
                UnityExecutionMode.Auto,
                DaemonRunning: false,
                UnityExecutionTarget.Oneshot,
                TimeSpan.FromSeconds(10)))),
            resolvedRequestExecutor,
            resolvedStreamingRequestExecutor,
            runIdGenerator ?? new StaticGuidGenerator(RunId),
            artifactStore,
            timeProvider ?? TimeProvider.System);
    }

    public static BuildCommandInput CreateInput (
        int? timeoutMilliseconds = 10000)
    {
        return new BuildCommandInput(
            ProfilePath: "/workspace/build.ucli.json",
            ProjectPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: timeoutMilliseconds);
    }

    public static string CreateProfileJson (
        IReadOnlyList<string> allowedExecutionModes,
        IReadOnlyList<string> allowedEditorModes,
        string projectMutationMode)
    {
        var executionModesJson = string.Join(",\n                  ", allowedExecutionModes.Select(static mode => $"\"{mode}\""));
        var editorModesJson = string.Join(",\n                  ", allowedEditorModes.Select(static mode => $"\"{mode}\""));
        return $$"""
            {
              "schemaVersion": 1,
              "inputs": {
                "kind": "explicit",
                "buildTarget": "standaloneLinux64",
                "scenes": {
                  "source": "explicit",
                  "paths": [
                    "Assets/Scenes/Main.unity"
                  ]
                },
                "options": {
                  "development": true
                }
              },
              "runner": {
                "kind": "buildPipeline"
              },
              "policy": {
                "runtime": {
                  "allowedExecutionModes": [
                  {{executionModesJson}}
                  ],
                  "allowedEditorModes": [
                  {{editorModesJson}}
                  ]
                },
                "projectMutationMode": "{{projectMutationMode}}"
              }
            }
            """;
    }

    public static Sha256Digest ResolveProfileDigest ()
    {
        return BuildProfileResolver.ResolveJson(ProfileJson).Profile!.Digest;
    }

    public static string CreateExecuteMethodProfileJson (
        string method,
        string arguments,
        string environment,
        string environmentVariables = "")
    {
        return $$"""
            {
              "schemaVersion": 1,
              "inputs": {
                "kind": "explicit",
                "buildTarget": "standaloneLinux64",
                "scenes": {
                  "source": "explicit",
                  "paths": [
                    "Assets/Scenes/Main.unity"
                  ]
                },
                "options": {
                  "development": true
                }
              },
              "runner": {
                "kind": "executeMethod",
                "method": "{{method}}",
                "invocation": {
                  "arguments": {
                {{arguments}}
                  },
                  "environment": {
                    "variables": [
                {{environmentVariables}}
                    ],
                    "secrets": [
                {{environment}}
                    ]
                  }
                }
              },
              "policy": {
                "runtime": {
                  "allowedExecutionModes": [
                    "daemon",
                    "oneshot"
                  ],
                  "allowedEditorModes": [
                    "batchmode",
                    "gui"
                  ]
                },
                "projectMutationMode": "forbid"
              }
            }
            """;
    }

    public static RecordingUnityRequestExecutor CreateBuildResponseExecutor (
        IpcBuildReportResult reportResult,
        IpcBuildLogCompletionReason completionReason,
        int errorCount,
        BuildProfileSceneSource? sceneSource = null,
        IReadOnlyList<SceneAssetPath>? scenes = null,
        BuildTargetStableName? buildTarget = null,
        string? unityBuildTarget = null,
        string? buildOptions = null,
        IpcUnityEditorObservation? lifecycleBefore = null,
        IpcUnityEditorObservation? lifecycleAfter = null,
        string? reportOutputPath = null,
        IpcBuildProjectMutationAudit? projectMutation = null,
        IpcBuildRunnerResultArtifact? runnerResult = null,
        bool omitReport = false,
        bool writeRunnerResultOutputs = true,
        bool writeRunnerBuildReportSource = true,
        string? runnerBuildReportSourceJson = null)
    {
        return new RecordingUnityRequestExecutor(
            payload =>
            {
                var buildRunPayload = (UnityRequestPayload.BuildRun)payload;
                var buildRunRequest = buildRunPayload.Request;
                if (runnerResult != null)
                {
                    WriteRunnerResultFiles(
                        buildRunPayload,
                        runnerResult,
                        reportResult,
                        errorCount,
                        writeRunnerResultOutputs,
                        writeRunnerBuildReportSource,
                        runnerBuildReportSourceJson);
                }

                return CreateBuildResponseResult(
                    reportResult,
                    completionReason,
                    errorCount,
                    sceneSource: sceneSource,
                    scenes: scenes,
                    buildTarget: buildTarget,
                    unityBuildTarget: unityBuildTarget,
                    buildOptions: buildOptions,
                    lifecycleBefore: lifecycleBefore,
                    lifecycleAfter: lifecycleAfter,
                    reportOutputPath: reportOutputPath ?? buildRunRequest.OutputLayout?.LocationPathName ?? buildRunRequest.OutputPath,
                    outputLayout: buildRunRequest.OutputLayout,
                    useDefaultOutputLayout: buildRunRequest.OutputLayout != null,
                    projectMutation: projectMutation,
                    runnerResult: runnerResult,
                    omitReport: omitReport);
            },
            static payload => CreateDefaultProgressFrames((UnityRequestPayload.BuildRun)payload));
    }

    public static async Task AssertInvalidUnityProgressFrameReturnsRunnerInvocationFailedAsync (
        string artifactStoreRoot,
        UnityRequestProgressFrame invalidProgressFrame)
    {
        var requestExecutor = new RecordingUnityRequestExecutor(
            _ =>
                CreateBuildResponseResult(
                    IpcBuildReportResult.Succeeded,
                    IpcBuildLogCompletionReason.Completed,
                    errorCount: 0),
            _ => [invalidProgressFrame]);
        var progressSink = new CollectingCommandProgressSink();
        var service = CreateService(
            requestExecutor: requestExecutor,
            artifactStore: new StubBuildRunArtifactStore(artifactStoreRoot));

        var result = await service.ExecuteAsync(CreateInput(), progressSink);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerInvocationFailed, error.Code);
        EventSequenceAssert.EmittedEventsInOrder(
            progressSink.Entries,
            BuildRunProgressEventNames.Started,
            BuildRunProgressEventNames.Diagnostic);
        BuildProgressAssert.RunnerInvocationFailureDiagnosticEmitted(progressSink, RunId);
    }

    public static UnityRequestExecutionResult CreateBuildResponseResult (
        IpcBuildReportResult reportResult,
        IpcBuildLogCompletionReason completionReason,
        int errorCount,
        BuildProfileInputsKind? inputKind = null,
        BuildProfileSceneSource? sceneSource = null,
        IReadOnlyList<SceneAssetPath>? scenes = null,
        BuildTargetStableName? buildTarget = null,
        string? unityBuildTarget = null,
        string? buildOptions = null,
        IpcUnityEditorObservation? lifecycleBefore = null,
        IpcUnityEditorObservation? lifecycleAfter = null,
        string? reportOutputPath = null,
        IpcBuildOutputLayout? outputLayout = null,
        bool useDefaultOutputLayout = true,
        IpcBuildProjectMutationAudit? projectMutation = null,
        IpcUnityBuildProfileInput? unityBuildProfile = null,
        IpcBuildRunnerResultArtifact? runnerResult = null,
        bool omitReport = false)
    {
        return UnityRequestExecutionResult.Success(new UnityRequestResponse(
            IpcPayloadCodec.SerializeToElement(new IpcBuildRunResponse(
                RunId: RunId,
                ProjectFingerprint: DefaultProjectFingerprint,
                LifecycleBefore: lifecycleBefore ?? CreateLifecycleSnapshot(10),
                LifecycleAfter: lifecycleAfter ?? CreateLifecycleSnapshot(11),
                DirtyState: new IpcBuildDirtyState(
                    Checked: true,
                    Dirty: false,
                    Coverage: IpcBuildDirtyStateCoverage.Full,
                    Items: []),
                Input: CreateInputProbe(inputKind, sceneSource, scenes, buildTarget, unityBuildTarget, buildOptions),
                OutputLayout: outputLayout ?? (useDefaultOutputLayout
                    ? new IpcBuildOutputLayout(
                        Shape: IpcBuildOutputLayoutShape.File,
                        LocationPathName: "/workspace/.ucli/output/player/Player")
                    : null),
                UnityBuildProfile: unityBuildProfile,
                Report: omitReport
                    ? null
                    : CreateBuildReportArtifact(
                        reportResult,
                        unityBuildTarget ?? "StandaloneLinux64",
                        reportOutputPath ?? "/workspace/.ucli/output/player/Player",
                        errorCount),
                Logs: new IpcBuildLogSummary(
                    EntryCount: errorCount == 0 ? 3 : 4,
                    ErrorCount: errorCount,
                    WarningCount: 1,
                    CompletionReason: completionReason,
                    Window: new IpcBuildLogWindow(
                        StartedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
                        CompletedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:03+00:00"))),
                ProjectMutation: projectMutation ?? CreateProjectMutation(mutated: false))
            {
                RunnerResult = runnerResult,
            }),
            []));
    }

    public static IpcUnityBuildProfileInput CreateUnityBuildProfileInput (
        string path,
        Sha256Digest digest,
        Func<IpcUnityBuildProfileApplyAudit, IpcUnityBuildProfileApplyAudit>? configureApplyAudit = null)
    {
        var lifecycleBefore = CreateLifecycleSnapshot(20);
        var lifecycleAfter = CreateLifecycleSnapshot(21);
        var applyAudit = new IpcUnityBuildProfileApplyAudit(
            Applied: true,
            LifecycleBefore: lifecycleBefore,
            LifecycleAfter: lifecycleAfter,
                DirtyStateAfter: new IpcBuildDirtyState(
                Checked: true,
                Dirty: false,
                Coverage: IpcBuildDirtyStateCoverage.Full,
                Items: []));
        return new IpcUnityBuildProfileInput(
            Path: new UnityBuildProfileAssetPath(path),
            Digest: digest,
            ApplyAudit: configureApplyAudit == null ? applyAudit : configureApplyAudit(applyAudit));
    }

    public static void WriteRunnerResultFiles (
        UnityRequestPayload.BuildRun buildRunPayload,
        IpcBuildRunnerResultArtifact runnerResult,
        IpcBuildReportResult reportResult,
        int errorCount,
        bool writeOutputs,
        bool writeBuildReportSource,
        string? buildReportSourceJson)
    {
        var request = buildRunPayload.Request;
        if (writeOutputs)
        {
            foreach (var output in runnerResult.Outputs)
            {
                var outputPath = Path.Combine(request.OutputPath, output);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                File.WriteAllText(outputPath, "runner output");
            }
        }

        if (writeBuildReportSource && runnerResult.BuildReport != null)
        {
            var buildReportSourcePath = Path.Combine(request.OutputPath, runnerResult.BuildReport.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(buildReportSourcePath)!);
            if (buildReportSourceJson != null)
            {
                File.WriteAllText(buildReportSourcePath, buildReportSourceJson);
            }
            else
            {
                var buildReport = CreateBuildReportArtifact(
                    reportResult,
                    "StandaloneLinux64",
                    request.OutputPath,
                    errorCount);
                File.WriteAllText(buildReportSourcePath, IpcPayloadCodec.SerializeToElement(buildReport).GetRawText());
            }
        }
    }

    public static IpcBuildReportArtifact CreateBuildReportArtifact (
        IpcBuildReportResult reportResult,
        string unityBuildTarget,
        string outputPath,
        int errorCount)
    {
        return new IpcBuildReportArtifact(
            SchemaVersion: 1,
            Result: reportResult,
            UnityBuildTarget: unityBuildTarget,
            OutputPath: outputPath,
            DurationMilliseconds: 2500,
            TotalSizeBytes: 4096,
            ErrorCount: errorCount,
            WarningCount: 1,
            Steps:
            [
                new IpcBuildReportStep(
                    Name: "Build player",
                    DurationMilliseconds: 2500,
                    Depth: 0,
                    MessageCount: 1),
            ],
            Messages:
            [
                new IpcBuildReportMessage(
                    Type: errorCount == 0 ? "warning" : "error",
                    Content: errorCount == 0 ? "Sample warning" : "Sample error"),
            ]);
    }

    public static IpcUnityEditorObservation CreateLifecycleSnapshot (long generation)
    {
        return new IpcUnityEditorObservation(
            serverVersion: "0.5.0",
            unityVersion: "6000.1.4f1",
            projectFingerprint: DefaultProjectFingerprint,
            state: new UnityEditorStateSnapshot(
                editorMode: DaemonEditorMode.Batchmode,
                lifecycleState: IpcEditorLifecycleState.Ready,
                compileState: IpcCompileState.Ready,
                generations: new IpcUnityGenerationSnapshot(
                    generation,
                    generation,
                    generation,
                    generation),
                playMode: new IpcPlayModeSnapshot(
                    State: IpcPlayModeState.Stopped,
                    Transition: IpcPlayModeTransition.None,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false)),
            observedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
            actionRequired: null,
            primaryDiagnostic: null);
    }

    public static IpcBuildProjectMutationAudit CreateProjectMutation (
        bool mutated,
        BuildProfileProjectMutationMode mode = BuildProfileProjectMutationMode.Forbid,
        IpcBuildProjectMutationAuditCoverage coverage = IpcBuildProjectMutationAuditCoverage.Full)
    {
        var beforeDigest = Sha256Digest.Parse(new string('1', 64));
        var afterDigest = mutated ? Sha256Digest.Parse(new string('2', 64)) : beforeDigest;
        return new IpcBuildProjectMutationAudit(
            Mode: mode,
            Coverage: coverage,
            Mutated: mutated,
            BeforeDigest: beforeDigest,
            AfterDigest: afterDigest,
            Items: mutated
                ?
                [
                    new IpcBuildProjectMutationAuditItem(
                        Path: "Assets/Generated.asset",
                        ChangeKind: IpcBuildProjectMutationChangeKind.Added,
                        BeforeSha256: null,
                        AfterSha256: Sha256Digest.Parse(new string('2', 64))),
                ]
                : []);
    }

    public static IpcBuildInputProbe CreateInputProbe (
        BuildProfileInputsKind? inputKind = null,
        BuildProfileSceneSource? sceneSource = null,
        IReadOnlyList<SceneAssetPath>? scenes = null,
        BuildTargetStableName? buildTarget = null,
        string? unityBuildTarget = null,
        string? buildOptions = null)
    {
        return new IpcBuildInputProbe(
            InputKind: inputKind ?? BuildProfileInputsKind.Explicit,
            BuildTarget: buildTarget ?? BuildTargetStableName.StandaloneLinux64,
            UnityBuildTarget: unityBuildTarget ?? "StandaloneLinux64",
            UnityBuildTargetGroup: "Standalone",
            SceneSource: sceneSource ?? BuildProfileSceneSource.Explicit,
            Scenes: scenes ?? [new SceneAssetPath("Assets/Scenes/Main.unity")],
            BuildOptions: buildOptions ?? "Development");
    }

    public static BuildClaimOutput FindClaim (
        BuildExecutionOutput output,
        UcliCode code)
    {
        return output.Claims.Single(claim => claim.Id == code);
    }

    public static void AssertEvidenceRefsResolveToReports (BuildExecutionOutput output)
    {
        foreach (var claim in output.Claims)
        {
            foreach (var evidence in claim.Evidence)
            {
                if (evidence.EvidenceRef == null)
                {
                    continue;
                }

                Assert.True(
                    output.Reports.ContainsKey(evidence.EvidenceRef.Value),
                    $"Claim {claim.Id} references missing report '{evidence.EvidenceRef}'.");
            }
        }
    }

    public static AssuranceSemanticInvariantValidator CreateBuildSemanticInvariantValidator ()
    {
        return new AssuranceSemanticInvariantValidator(
            new CodeCatalogModel([new BuildCodeCatalogContributor()]),
            [new BuildAssuranceSemanticInvariantRule()],
            [new BuildAssuranceSemanticInvariantRule()]);
    }

    public static string CreateExpectedPlayerLocationPathName (
        string outputDirectory,
        string fileName = "Player")
    {
        return string.Concat(
            outputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            "/player/",
            fileName);
    }

    public static IReadOnlyList<UnityRequestProgressFrame> CreateDefaultProgressFrames (UnityRequestPayload.BuildRun request)
    {
        var runnerKind = request.Request.RunnerKind;
        return
        [
            CreateProgressFrame(
                BuildRunProgressEventNames.ReadinessCompleted,
                request,
                BuildRunProgressPhase.Readiness,
                runnerKind: null,
                runnerStatus: null),
            CreateProgressFrame(
                BuildRunProgressEventNames.RunnerResolved,
                request,
                BuildRunProgressPhase.RunnerResolution,
                runnerKind,
                runnerStatus: null),
            CreateProgressFrame(
                BuildRunProgressEventNames.RunnerStarted,
                request,
                BuildRunProgressPhase.RunnerInvocation,
                runnerKind,
                runnerStatus: null),
            CreateProgressFrame(
                BuildRunProgressEventNames.RunnerCompleted,
                request,
                BuildRunProgressPhase.RunnerResult,
                runnerKind,
                IpcBuildReportResult.Succeeded),
        ];
    }

    public static UnityRequestProgressFrame CreateProgressFrame (
        string eventName,
        UnityRequestPayload.BuildRun request,
        BuildRunProgressPhase phase,
        BuildRunnerKind? runnerKind,
        IpcBuildReportResult? runnerStatus)
    {
        return new UnityRequestProgressFrame(
            eventName,
            IpcPayloadCodec.SerializeToElement(new BuildProgressEntry(
                RunId: request.Request.RunId,
                ProfileDigest: request.Request.ProfileDigest,
                Phase: phase,
                RunnerKind: runnerKind,
                RunnerStatus: runnerStatus,
                Verdict: null,
                ReportRefs: [],
                ErrorCode: null)));
    }

    public static TestDirectoryScope CreateArtifactDirectoryScope ()
    {
        return TestDirectories.CreateTempScope(
            "build-service",
            "artifacts",
            DirectoryCleanupMode.BestEffort);
    }
}
