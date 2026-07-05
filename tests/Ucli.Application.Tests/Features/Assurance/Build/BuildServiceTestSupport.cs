using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Catalog;
using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Execution;
using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Application.Features.Assurance.Build.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.EnvironmentVariables;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;
using CodeCatalogModel = MackySoft.Ucli.Application.Features.CodeCatalog.Catalog.CodeCatalog;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

internal static class BuildServiceTestSupport
{
    public const string RunId = "build-run-1";
    public const string ProjectFingerprint = "project-fingerprint";

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
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static readonly string[] BuildPipelineEffectValues =
    [
        ContractLiteralCodec.ToValue(BuildEffect.UnityLifecycleRead),
        ContractLiteralCodec.ToValue(BuildEffect.UnityBuildPipeline),
        ContractLiteralCodec.ToValue(BuildEffect.UnityBuildReportRead),
        ContractLiteralCodec.ToValue(BuildEffect.UnityLogWindowRead),
        ContractLiteralCodec.ToValue(BuildEffect.UcliArtifactWrite),
        ContractLiteralCodec.ToValue(BuildEffect.OutputManifestWrite),
        ContractLiteralCodec.ToValue(BuildEffect.GenerationSnapshot),
        ContractLiteralCodec.ToValue(BuildEffect.ProjectMutationAudit),
    ];

    public static BuildService CreateService (
        IBuildRunArtifactStore artifactStore,
        IProjectContextResolver? projectContextResolver = null,
        IBuildProfileFileReader? profileFileReader = null,
        IEnvironmentVariableReader? environmentVariableReader = null,
        IUnityExecutionModeDecisionService? modeDecisionService = null,
        IUnityRequestExecutor? requestExecutor = null,
        IUnityStreamingRequestExecutor? streamingRequestExecutor = null,
        IBuildRunIdFactory? runIdFactory = null,
        TimeProvider? timeProvider = null)
    {
        var resolvedRequestExecutor = requestExecutor ?? CreateBuildResponseExecutor(
            ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
            errorCount: 0);
        var resolvedStreamingRequestExecutor = streamingRequestExecutor
            ?? resolvedRequestExecutor as IUnityStreamingRequestExecutor
            ?? throw new InvalidOperationException("BuildService tests require a streaming request executor.");
        return new BuildService(
            projectContextResolver ?? new StaticProjectContextResolver(ProjectContextResolutionResult.Success(ProjectContextTestFactory.Create(
                projectFingerprint: ProjectFingerprint))),
            profileFileReader ?? new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(ProfileJson, "/workspace/build.ucli.json")),
            environmentVariableReader ?? new StubEnvironmentVariableReader(),
            modeDecisionService ?? new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(new UnityExecutionModeDecision(
                UnityExecutionMode.Auto,
                DaemonRunning: false,
                UnityExecutionTarget.Oneshot,
                TimeSpan.FromSeconds(10)))),
            resolvedRequestExecutor,
            resolvedStreamingRequestExecutor,
            runIdFactory ?? new StubBuildRunIdFactory(RunId),
            artifactStore,
            timeProvider);
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

    public static string ResolveProfileDigest ()
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
        string reportResult,
        string completionReason,
        int errorCount,
        string? sceneSource = null,
        IReadOnlyList<string>? scenes = null,
        string? buildTarget = null,
        string? unityBuildTarget = null,
        string? buildOptions = null,
        IpcBuildLifecycleSnapshot? lifecycleBefore = null,
        IpcBuildLifecycleSnapshot? lifecycleAfter = null,
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
                    reportOutputPath: reportOutputPath ?? buildRunPayload.OutputLayout?.LocationPathName ?? buildRunPayload.OutputPath,
                    outputLayout: buildRunPayload.OutputLayout,
                    useDefaultOutputLayout: buildRunPayload.OutputLayout != null,
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
                    ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                    ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
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
        string reportResult,
        string completionReason,
        int errorCount,
        string? inputKind = null,
        string? sceneSource = null,
        IReadOnlyList<string>? scenes = null,
        string? buildTarget = null,
        string? unityBuildTarget = null,
        string? buildOptions = null,
        IpcBuildLifecycleSnapshot? lifecycleBefore = null,
        IpcBuildLifecycleSnapshot? lifecycleAfter = null,
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
                ProjectFingerprint: ProjectFingerprint,
                LifecycleBefore: lifecycleBefore ?? CreateLifecycleSnapshot("before", canAcceptExecutionRequests: true),
                LifecycleAfter: lifecycleAfter ?? CreateLifecycleSnapshot("after", canAcceptExecutionRequests: true),
                DirtyState: new IpcBuildDirtyState(
                    Checked: true,
                    Dirty: false,
                    Coverage: ContractLiteralCodec.ToValue(IpcBuildDirtyStateCoverage.Full),
                    Items: []),
                Input: CreateInputProbe(inputKind, sceneSource, scenes, buildTarget, unityBuildTarget, buildOptions),
                OutputLayout: outputLayout ?? (useDefaultOutputLayout
                    ? new IpcBuildOutputLayout(
                        Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
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
            [],
            HasFailureStatus: false));
    }

    public static IpcUnityBuildProfileInput CreateUnityBuildProfileInput (
        string path,
        string digest,
        Func<IpcUnityBuildProfileApplyAudit, IpcUnityBuildProfileApplyAudit>? configureApplyAudit = null)
    {
        var lifecycleBefore = CreateLifecycleSnapshot("profile-before", canAcceptExecutionRequests: true);
        var lifecycleAfter = CreateLifecycleSnapshot("profile-after", canAcceptExecutionRequests: true);
        var applyAudit = new IpcUnityBuildProfileApplyAudit(
            Applied: true,
            LifecycleBefore: lifecycleBefore,
            LifecycleAfter: lifecycleAfter,
            GenerationsBefore: new IpcBuildGenerationSnapshot(
                lifecycleBefore.CompileGeneration,
                lifecycleBefore.DomainReloadGeneration,
                lifecycleBefore.AssetRefreshGeneration),
            GenerationsAfter: new IpcBuildGenerationSnapshot(
                lifecycleAfter.CompileGeneration,
                lifecycleAfter.DomainReloadGeneration,
                lifecycleAfter.AssetRefreshGeneration),
            DirtyStateAfter: new IpcBuildDirtyState(
                Checked: true,
                Dirty: false,
                Coverage: ContractLiteralCodec.ToValue(IpcBuildDirtyStateCoverage.Full),
                Items: []));
        return new IpcUnityBuildProfileInput(
            Path: path,
            Digest: digest,
            ApplyAudit: configureApplyAudit == null ? applyAudit : configureApplyAudit(applyAudit));
    }

    public static void WriteRunnerResultFiles (
        UnityRequestPayload.BuildRun buildRunPayload,
        IpcBuildRunnerResultArtifact runnerResult,
        string reportResult,
        int errorCount,
        bool writeOutputs,
        bool writeBuildReportSource,
        string? buildReportSourceJson)
    {
        if (writeOutputs)
        {
            foreach (var output in runnerResult.Outputs)
            {
                var outputPath = Path.Combine(buildRunPayload.OutputPath, output);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                File.WriteAllText(outputPath, "runner output");
            }
        }

        if (writeBuildReportSource && runnerResult.BuildReport != null)
        {
            var buildReportSourcePath = Path.Combine(buildRunPayload.OutputPath, runnerResult.BuildReport.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(buildReportSourcePath)!);
            if (buildReportSourceJson != null)
            {
                File.WriteAllText(buildReportSourcePath, buildReportSourceJson);
            }
            else
            {
                var unityBuildTarget = buildRunPayload.UnityBuildTarget
                    ?? throw new InvalidOperationException("Test build report source requires a Unity build target.");
                var buildReport = CreateBuildReportArtifact(
                    reportResult,
                    unityBuildTarget,
                    buildRunPayload.OutputPath,
                    errorCount);
                File.WriteAllText(buildReportSourcePath, IpcPayloadCodec.SerializeToElement(buildReport).GetRawText());
            }
        }
    }

    public static IpcBuildReportArtifact CreateBuildReportArtifact (
        string reportResult,
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

    public static IpcBuildLifecycleSnapshot CreateLifecycleSnapshot (
        string generationSuffix,
        bool canAcceptExecutionRequests,
        bool omitAssetRefreshGeneration = false)
    {
        return new IpcBuildLifecycleSnapshot(
            ServerVersion: "0.5.0",
            EditorMode: "batchmode",
            UnityVersion: "6000.1.4f1",
            ProjectFingerprint: ProjectFingerprint,
            LifecycleState: "ready",
            BlockingReason: null,
            CompileState: "idle",
            CompileGeneration: $"compile-{generationSuffix}",
            DomainReloadGeneration: $"domain-{generationSuffix}",
            CanAcceptExecutionRequests: canAcceptExecutionRequests,
            ObservedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: new IpcPlayModeSnapshot(
                State: "stopped",
                Transition: "none",
                IsPlaying: false,
                IsPlayingOrWillChangePlaymode: false,
                Generation: $"play-{generationSuffix}"),
            AssetRefreshGeneration: omitAssetRefreshGeneration ? null : $"asset-{generationSuffix}");
    }

    public static IpcBuildProjectMutationAudit CreateProjectMutation (
        bool mutated,
        string mode = "forbid",
        string? coverage = null)
    {
        var resolvedCoverage = coverage ?? ContractLiteralCodec.ToValue(IpcBuildProjectMutationAuditCoverage.Full);
        var beforeDigest = new string('1', 64);
        var afterDigest = mutated ? new string('2', 64) : beforeDigest;
        return new IpcBuildProjectMutationAudit(
            Mode: mode,
            Coverage: resolvedCoverage,
            Mutated: mutated,
            BeforeDigest: beforeDigest,
            AfterDigest: afterDigest,
            Items: mutated
                ?
                [
                    new IpcBuildProjectMutationAuditItem(
                        Path: "Assets/Generated.asset",
                        ChangeKind: ContractLiteralCodec.ToValue(IpcBuildProjectMutationChangeKind.Added),
                        BeforeSha256: null,
                        AfterSha256: new string('2', 64)),
                ]
                : []);
    }

    public static IpcBuildInputProbe CreateInputProbe (
        string? inputKind = null,
        string? sceneSource = null,
        IReadOnlyList<string>? scenes = null,
        string? buildTarget = null,
        string? unityBuildTarget = null,
        string? buildOptions = null)
    {
        return new IpcBuildInputProbe(
            InputKind: inputKind ?? ContractLiteralCodec.ToValue(BuildProfileInputsKind.Explicit),
            BuildTarget: buildTarget ?? "standaloneLinux64",
            UnityBuildTarget: unityBuildTarget ?? "StandaloneLinux64",
            UnityBuildTargetGroup: "Standalone",
            SceneSource: sceneSource ?? ContractLiteralCodec.ToValue(BuildProfileSceneSource.Explicit),
            Scenes: scenes ?? ["Assets/Scenes/Main.unity"],
            BuildOptions: buildOptions ?? "Development");
    }

    public static BuildClaimOutput FindClaim (
        BuildExecutionOutput output,
        UcliCode code)
    {
        return output.Claims.Single(claim => string.Equals(claim.Id, code.Value, StringComparison.Ordinal));
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
                    output.Reports.ContainsKey(evidence.EvidenceRef),
                    $"Claim {claim.Id} references missing report '{evidence.EvidenceRef}'.");
            }
        }
    }

    public static AssuranceSemanticInvariantValidator CreateBuildSemanticInvariantValidator ()
    {
        return new AssuranceSemanticInvariantValidator(
            new CodeCatalogModel([new BuildCodeCatalogContributor()]),
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
        var runnerKind = request.RunnerKind ?? "buildPipeline";
        return
        [
            CreateProgressFrame(
                BuildRunProgressEventNames.ReadinessCompleted,
                request,
                "readiness",
                runnerKind: null,
                runnerStatus: null),
            CreateProgressFrame(
                BuildRunProgressEventNames.RunnerResolved,
                request,
                "runnerResolution",
                runnerKind,
                runnerStatus: null),
            CreateProgressFrame(
                BuildRunProgressEventNames.RunnerStarted,
                request,
                "runnerInvocation",
                runnerKind,
                runnerStatus: null),
            CreateProgressFrame(
                BuildRunProgressEventNames.RunnerCompleted,
                request,
                "runnerResult",
                runnerKind,
                "succeeded"),
        ];
    }

    public static UnityRequestProgressFrame CreateProgressFrame (
        string eventName,
        UnityRequestPayload.BuildRun request,
        string phase,
        string? runnerKind,
        string? runnerStatus)
    {
        return new UnityRequestProgressFrame(
            eventName,
            IpcPayloadCodec.SerializeToElement(new BuildProgressEntry(
                RunId: request.RunId,
                ProfileDigest: request.ProfileDigest!,
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
