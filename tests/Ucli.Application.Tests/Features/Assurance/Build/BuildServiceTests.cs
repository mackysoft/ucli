using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Execution;
using MackySoft.Ucli.Application.Features.Assurance.Build.Metadata;
using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildServiceTests
{
    private const string RunId = "build-run-1";
    private const string ProjectFingerprint = "project-fingerprint";

    private const string ProfileJson = """
        {
          "schemaVersion": 1,
          "target": "standaloneLinux64",
          "scenes": {
            "source": "explicit",
            "paths": [
              "Assets/Scenes/Main.unity"
            ]
          },
          "output": {
            "kind": "ucliArtifact"
          },
          "options": {
            "development": true
          }
        }
        """;

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithSucceededBuildReport_ReturnsArtifactBackedPayload ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.Path);
        var requestExecutor = new StubUnityRequestExecutor(CreateBuildResponseResult(
            ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
            errorCount: 0));
        var service = CreateService(
            requestExecutor: requestExecutor,
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.True(result.IsSuccess);
        var output = result.Output!;
        Assert.Equal(ContractLiteralCodec.ToValue(BuildVerdict.Pass), output.Verdict);
        Assert.Equal(RunId, output.Build.RunId);
        Assert.Equal("succeeded", output.Build.Summary.Result);
        Assert.Equal("buildReport", output.Build.Summary.ReportRef);
        Assert.Equal("buildLog", output.Build.Logs.ReportRef);
        Assert.Equal("completed", output.Build.Logs.CompletionReason);
        Assert.Equal("asset-before", output.Build.Generations.Before.AssetRefreshGeneration);
        Assert.Equal("asset-after", output.Build.Generations.After.AssetRefreshGeneration);
        Assert.Equal("asset-after", output.Build.Generations.ValidFor.AssetRefreshGeneration);
        Assert.Equal("manifest-digest", output.Build.Output.ManifestDigest);
        Assert.Equal(
            ["build", "buildLog", "buildOutputManifest", "buildReport"],
            output.Reports.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal("metadata-digest", output.Reports["build"].Digest);
        Assert.Equal("build-report-digest", output.Reports["buildReport"].Digest);
        Assert.Equal("manifest-digest", output.Reports["buildOutputManifest"].Digest);
        Assert.Equal("build-log-digest", output.Reports["buildLog"].Digest);
        Assert.Equal(10, output.Claims.Count);
        Assert.All(output.Claims, claim => Assert.True(claim.Required));
        var verifier = Assert.Single(output.Verifiers);
        Assert.Equal("build", verifier.Id);
        Assert.Equal(BuildClaimCodes.All.Select(static code => code.Value).ToArray(), verifier.PrimaryClaims);
        Assert.Equal(ContractLiteralCodec.GetLiterals<BuildEffect>(), verifier.Effects);
        Assert.NotNull(artifactStore.WrittenMetadata);
        Assert.Equal("succeeded", artifactStore.WrittenMetadata!.Summary.Result);

        var payload = Assert.IsType<UnityRequestPayload.BuildRun>(requestExecutor.CapturedPayload);
        Assert.Equal(RunId, payload.RunId);
        Assert.Equal("standaloneLinux64", payload.TargetStableName);
        Assert.Equal("StandaloneLinux64", payload.UnityBuildTarget);
        Assert.Equal("explicit", payload.SceneSource);
        Assert.Equal(["Assets/Scenes/Main.unity"], payload.ScenePaths);
        Assert.True(payload.Development);
        Assert.Equal(artifactStore.PreparedPaths!.OutputDirectory, payload.OutputPath);
        Assert.Equal(artifactStore.PreparedPaths.BuildReportPath, payload.BuildReportPath);
        Assert.Equal(artifactStore.PreparedPaths.BuildLogPath, payload.BuildLogPath);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("failed", "failed")]
    [InlineData("canceled", "canceled")]
    public async Task Execute_WithUnsuccessfulBuildReport_ReturnsCompletedFailVerdict (
        string reportResult,
        string completionReason)
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var service = CreateService(
            requestExecutor: new StubUnityRequestExecutor(CreateBuildResponseResult(
                reportResult,
                completionReason,
                errorCount: 1)),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.True(result.IsSuccess);
        var output = result.Output!;
        Assert.Equal(ContractLiteralCodec.ToValue(BuildVerdict.Fail), output.Verdict);
        Assert.Equal(reportResult, output.Build.Summary.Result);
        Assert.Equal(completionReason, output.Build.Logs.CompletionReason);
        Assert.Equal(ContractLiteralCodec.ToValue(BuildClaimStatus.Passed), FindClaim(output, BuildClaimCodes.UnityBuildCompleted).Status);
        Assert.Equal(ContractLiteralCodec.ToValue(BuildClaimStatus.Failed), FindClaim(output, BuildClaimCodes.UnityBuildSucceeded).Status);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithDirtySceneResponse_ReturnsCommandFailureWithProbePayload ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var dirtyState = new IpcBuildDirtyState(
            Checked: true,
            Dirty: true,
            Items:
            [
                new IpcBuildDirtyStateItem(
                    IpcBuildDirtyStateItemKindNames.Scene,
                    "Assets/Scenes/Main.unity"),
            ]);
        var input = CreateInputProbe();
        var errorPayload = new IpcBuildRunErrorPayload(
            Project: new IpcProjectIdentity("/workspace/UnityProject", ProjectFingerprint, "6000.1.4f1"),
            LifecycleBefore: CreateLifecycleSnapshot("before", canAcceptExecutionRequests: true),
            DirtyState: dirtyState,
            Input: input);
        var response = new UnityRequestResponse(
            IpcPayloadCodec.SerializeToElement(errorPayload),
            [new OperationExecutionError(BuildErrorCodes.BuildDirtyStatePresent, "Dirty scene state is present.", null)],
            HasFailureStatus: true,
            FailureStatus: IpcProtocol.StatusError);
        var service = CreateService(
            requestExecutor: new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(response)),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildDirtyStatePresent, error.Code);
        Assert.NotNull(result.DirtyState);
        Assert.True(result.DirtyState!.Checked);
        Assert.True(result.DirtyState.Dirty);
        var item = Assert.Single(result.DirtyState.Items);
        Assert.Equal(IpcBuildDirtyStateItemKindNames.Scene, item.Kind);
        Assert.Equal("Assets/Scenes/Main.unity", item.Path);
        Assert.NotNull(result.Input);
        Assert.Equal("standaloneLinux64", result.Input!.TargetStableName);
        Assert.Equal("StandaloneLinux64", result.Input.UnityBuildTarget);
    }

    private static BuildService CreateService (
        IProjectContextResolver? projectContextResolver = null,
        IBuildProfileFileReader? profileFileReader = null,
        IUnityExecutionModeDecisionService? modeDecisionService = null,
        IUnityRequestExecutor? requestExecutor = null,
        IBuildRunIdFactory? runIdFactory = null,
        IBuildRunArtifactStore? artifactStore = null)
    {
        return new BuildService(
            projectContextResolver ?? new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateProjectContext())),
            profileFileReader ?? new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(ProfileJson, "/workspace/build.ucli.json")),
            modeDecisionService ?? new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(new UnityExecutionModeDecision(
                UnityExecutionMode.Auto,
                DaemonRunning: false,
                UnityExecutionTarget.Oneshot,
                TimeSpan.FromSeconds(10)))),
            requestExecutor ?? new StubUnityRequestExecutor(CreateBuildResponseResult(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0)),
            runIdFactory ?? new StubBuildRunIdFactory(RunId),
            artifactStore ?? new StubBuildRunArtifactStore(TemporaryDirectory.Create().Path));
    }

    private static BuildCommandInput CreateInput ()
    {
        return new BuildCommandInput(
            ProfilePath: null,
            ProjectPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000);
    }

    private static ProjectContext CreateProjectContext ()
    {
        var unityProject = new ResolvedUnityProjectContext(
            UnityProjectRoot: "/workspace/UnityProject",
            RepositoryRoot: "/workspace",
            ProjectFingerprint: ProjectFingerprint,
            PathSource: UnityProjectPathSource.CommandOption,
            PathSourceLabel: "--projectPath",
            UnityVersion: "6000.1.4f1");
        return new ProjectContext(
            unityProject,
            UcliConfig.CreateDefault(),
            ConfigSource.Default);
    }

    private static UnityRequestExecutionResult CreateBuildResponseResult (
        string reportResult,
        string completionReason,
        int errorCount)
    {
        return UnityRequestExecutionResult.Success(new UnityRequestResponse(
            IpcPayloadCodec.SerializeToElement(new IpcBuildRunResponse(
                RunId: RunId,
                ProjectFingerprint: ProjectFingerprint,
                LifecycleBefore: CreateLifecycleSnapshot("before", canAcceptExecutionRequests: true),
                LifecycleAfter: CreateLifecycleSnapshot("after", canAcceptExecutionRequests: true),
                DirtyState: new IpcBuildDirtyState(Checked: true, Dirty: false, Items: []),
                Input: CreateInputProbe(),
                Report: new IpcBuildReportArtifact(
                    SchemaVersion: 1,
                    Result: reportResult,
                    Target: "StandaloneLinux64",
                    OutputPath: "/workspace/.ucli/output/build",
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
                    ]),
                Logs: new IpcBuildLogSummary(
                    EntryCount: errorCount == 0 ? 3 : 4,
                    ErrorCount: errorCount,
                    WarningCount: 1,
                    CompletionReason: completionReason,
                    Window: new IpcBuildLogWindow(
                        StartedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
                        CompletedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:03+00:00"))))),
            [],
            HasFailureStatus: false));
    }

    private static IpcBuildLifecycleSnapshot CreateLifecycleSnapshot (
        string generationSuffix,
        bool canAcceptExecutionRequests)
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
            AssetRefreshGeneration: $"asset-{generationSuffix}");
    }

    private static IpcBuildInputProbe CreateInputProbe ()
    {
        return new IpcBuildInputProbe(
            TargetStableName: "standaloneLinux64",
            UnityBuildTarget: "StandaloneLinux64",
            UnityBuildTargetGroup: "Standalone",
            SceneSource: ContractLiteralCodec.ToValue(BuildProfileSceneSource.Explicit),
            Scenes: ["Assets/Scenes/Main.unity"],
            BuildOptions: "Development");
    }

    private static BuildClaimOutput FindClaim (
        BuildExecutionOutput output,
        UcliCode code)
    {
        return output.Claims.Single(claim => string.Equals(claim.Id, code.Value, StringComparison.Ordinal));
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

    private sealed class StubBuildProfileFileReader : IBuildProfileFileReader
    {
        private readonly BuildProfileFileReadResult result;

        public StubBuildProfileFileReader (BuildProfileFileReadResult result)
        {
            this.result = result;
        }

        public ValueTask<BuildProfileFileReadResult> ReadAsync (
            string? profilePath,
            ResolvedUnityProjectContext unityProject,
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

    private sealed class StubBuildRunIdFactory : IBuildRunIdFactory
    {
        private readonly string runId;

        public StubBuildRunIdFactory (string runId)
        {
            this.runId = runId;
        }

        public string Create ()
        {
            return runId;
        }
    }

    private sealed class StubBuildRunArtifactStore : IBuildRunArtifactStore
    {
        private readonly string rootPath;

        public StubBuildRunArtifactStore (string rootPath)
        {
            this.rootPath = rootPath;
        }

        public BuildRunArtifactPaths? PreparedPaths { get; private set; }

        public BuildRunMetadata? WrittenMetadata { get; private set; }

        public ValueTask<BuildRunArtifactPrepareResult> PrepareAsync (
            ResolvedUnityProjectContext unityProject,
            string runId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var runDirectory = Path.Combine(rootPath, runId);
            var outputDirectory = Path.Combine(runDirectory, "output");
            Directory.CreateDirectory(outputDirectory);
            PreparedPaths = new BuildRunArtifactPaths(
                RunDirectory: runDirectory,
                BuildJsonPath: Path.Combine(runDirectory, "build.json"),
                BuildReportPath: Path.Combine(runDirectory, "build-report.json"),
                BuildLogPath: Path.Combine(runDirectory, "build.log"),
                OutputManifestPath: Path.Combine(runDirectory, "output-manifest.json"),
                OutputDirectory: outputDirectory);
            File.WriteAllText(PreparedPaths.BuildReportPath, "{}");
            return ValueTask.FromResult(BuildRunArtifactPrepareResult.Success(PreparedPaths));
        }

        public ValueTask<BuildOutputManifestResult> WriteOutputManifestAsync (
            BuildRunArtifactPaths paths,
            string target,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var manifest = new BuildOutputManifest(
                SchemaVersion: 1,
                OutputRoot: paths.OutputDirectory,
                Target: target,
                FileCount: 1,
                TotalBytes: 12,
                Files:
                [
                    new BuildOutputManifestFile(
                        Path: "Game.x86_64",
                        SizeBytes: 12,
                        Sha256: "file-digest"),
                ],
                ManifestDigest: "manifest-digest");
            return ValueTask.FromResult(BuildOutputManifestResult.Success(manifest));
        }

        public ValueTask<BuildArtifactWriteResult> WriteMetadataAsync (
            BuildRunArtifactPaths paths,
            BuildRunMetadata metadata,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WrittenMetadata = metadata;
            return ValueTask.FromResult(BuildArtifactWriteResult.Success("metadata-digest"));
        }

        public ValueTask<BuildArtifactWriteResult> CalculateDigestAsync (
            string path,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var digest = Path.GetFileName(path) switch
            {
                "build-report.json" => "build-report-digest",
                "build.log" => "build-log-digest",
                "output-manifest.json" => "manifest-digest",
                _ => throw new InvalidOperationException($"Unexpected digest path: {path}"),
            };
            return ValueTask.FromResult(BuildArtifactWriteResult.Success(digest));
        }

        public ValueTask<BuildArtifactWriteResult> CalculateRequiredDigestAsync (
            string path,
            UcliCode missingCode,
            CancellationToken cancellationToken = default)
        {
            return CalculateDigestAsync(path, cancellationToken);
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory (string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create ()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ucli-build-service-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose ()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
