using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Metadata;
using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Execution;

/// <summary> Executes Unity build assurance runs and persists build artifacts. </summary>
internal sealed class BuildService : IBuildService
{
    private const int BuildMetadataSchemaVersion = 1;
    private const string UnknownGeneration = "unknown";

    private static readonly IReadOnlyList<BuildResidualRiskOutput> EmptyResidualRisks =
        Array.Empty<BuildResidualRiskOutput>();

    private readonly IProjectContextResolver projectContextResolver;

    private readonly IBuildProfileFileReader profileFileReader;

    private readonly IUnityExecutionModeDecisionService executionModeDecisionService;

    private readonly IUnityRequestExecutor unityRequestExecutor;

    private readonly IBuildRunIdFactory runIdFactory;

    private readonly IBuildRunArtifactStore artifactStore;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="BuildService" /> class. </summary>
    public BuildService (
        IProjectContextResolver projectContextResolver,
        IBuildProfileFileReader profileFileReader,
        IUnityExecutionModeDecisionService executionModeDecisionService,
        IUnityRequestExecutor unityRequestExecutor,
        IBuildRunIdFactory runIdFactory,
        IBuildRunArtifactStore artifactStore,
        TimeProvider? timeProvider = null)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.profileFileReader = profileFileReader ?? throw new ArgumentNullException(nameof(profileFileReader));
        this.executionModeDecisionService = executionModeDecisionService ?? throw new ArgumentNullException(nameof(executionModeDecisionService));
        this.unityRequestExecutor = unityRequestExecutor ?? throw new ArgumentNullException(nameof(unityRequestExecutor));
        this.runIdFactory = runIdFactory ?? throw new ArgumentNullException(nameof(runIdFactory));
        this.artifactStore = artifactStore ?? throw new ArgumentNullException(nameof(artifactStore));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async ValueTask<BuildExecutionResult> ExecuteAsync (
        BuildCommandInput input,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        _ = progressSink ?? NullCommandProgressSink.Instance;
        var contextResult = await projectContextResolver.ResolveAsync(input.ProjectPath, cancellationToken).ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return BuildExecutionResult.Failure(contextResult.Error!);
        }

        var context = contextResult.Context!;
        var project = ProjectIdentityInfo.From(context.UnityProject);
        var profileReadResult = await profileFileReader.ReadAsync(
                input.ProfilePath,
                context.UnityProject,
                cancellationToken)
            .ConfigureAwait(false);
        if (!profileReadResult.IsSuccess)
        {
            return BuildExecutionResult.Failure(profileReadResult.Error!, project);
        }

        var profileResolutionResult = BuildProfileResolver.ResolveJson(profileReadResult.Json!);
        if (!profileResolutionResult.IsSuccess)
        {
            return BuildExecutionResult.Failure(profileResolutionResult.Error!, project);
        }

        var timeoutResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            UcliCommandIds.BuildRun,
            context.Config);
        if (!timeoutResult.IsSuccess)
        {
            return BuildExecutionResult.Failure(timeoutResult.Error!, project);
        }

        var timeout = timeoutResult.Timeout!.Value;
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        var requestedMode = input.Mode ?? UnityExecutionMode.Auto;
        if (!deadline.TryGetRemainingTimeout(out var modeDecisionTimeout))
        {
            return BuildExecutionResult.Failure(CreateTimeoutFailure(timeout), project);
        }

        var modeDecisionResult = await executionModeDecisionService.DecideAsync(
                requestedMode,
                context.UnityProject,
                modeDecisionTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!modeDecisionResult.IsSuccess)
        {
            if (modeDecisionResult.HasContractError)
            {
                var contractError = modeDecisionResult.ContractError!;
                return BuildExecutionResult.Failure(
                    ApplicationFailure.FromCode(contractError.Code, contractError.Message),
                    project);
            }

            return BuildExecutionResult.Failure(modeDecisionResult.Error!, project);
        }

        if (!deadline.TryGetRemainingTimeout(out var requestTimeout))
        {
            return BuildExecutionResult.Failure(CreateTimeoutFailure(timeout), project);
        }

        var runId = runIdFactory.Create();
        var prepareResult = await artifactStore.PrepareAsync(context.UnityProject, runId, cancellationToken).ConfigureAwait(false);
        if (!prepareResult.IsSuccess)
        {
            return BuildExecutionResult.Failure(prepareResult.Error!, project);
        }

        var profile = profileResolutionResult.Profile!;
        var paths = prepareResult.Paths!;
        var request = CreateBuildRunRequest(profile, paths, runId);
        var executionResult = await unityRequestExecutor.ExecuteAsync(
                UcliCommandIds.BuildRun,
                ResolveExecutionMode(modeDecisionResult.Decision!.Target),
                requestTimeout,
                context.Config,
                context.UnityProject,
                request,
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            var failureInfo = executionResult.FailureInfo!;
            return BuildExecutionResult.Failure(
                ApplicationFailure.FromCode(
                    failureInfo.Code,
                    failureInfo.Message,
                    startupFailure: failureInfo.StartupFailure),
                project);
        }

        var responseResult = ResolveBuildResponse(executionResult.Response!, runId, context.UnityProject.ProjectFingerprint);
        if (!responseResult.IsSuccess)
        {
            var dirtyState = responseResult.Error!.Code == BuildErrorCodes.BuildDirtyStatePresent
                ? responseResult.ErrorPayload?.DirtyState
                : null;
            return BuildExecutionResult.Failure(
                responseResult.Error!,
                project,
                dirtyState);
        }

        var buildResponse = responseResult.Response!;
        if (!deadline.TryGetRemainingTimeout(out var artifactAccountingTimeout))
        {
            return BuildExecutionResult.Failure(CreateTimeoutFailure(timeout), project);
        }

        using var artifactAccountingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        artifactAccountingCancellationTokenSource.CancelAfter(artifactAccountingTimeout);
        var artifactCancellationToken = artifactAccountingCancellationTokenSource.Token;
        try
        {
            var outputManifestResult = await artifactStore.WriteOutputManifestAsync(
                    paths,
                    profile.Target.StableName,
                    artifactCancellationToken)
                .ConfigureAwait(false);
            if (!outputManifestResult.IsSuccess)
            {
                return BuildExecutionResult.Failure(outputManifestResult.Error!, project);
            }

            var buildReportDigestResult = await artifactStore.CalculateRequiredDigestAsync(paths.BuildReportPath, BuildErrorCodes.BuildReportMissing, artifactCancellationToken).ConfigureAwait(false);
            if (!buildReportDigestResult.IsSuccess)
            {
                return BuildExecutionResult.Failure(buildReportDigestResult.Error!, project);
            }

            var buildLogDigestResult = await artifactStore.CalculateDigestAsync(paths.BuildLogPath, artifactCancellationToken).ConfigureAwait(false);
            if (!buildLogDigestResult.IsSuccess)
            {
                return BuildExecutionResult.Failure(buildLogDigestResult.Error!, project);
            }

            var outputManifestContentDigestResult = await artifactStore.CalculateOutputManifestContentDigestAsync(paths.OutputManifestPath, artifactCancellationToken).ConfigureAwait(false);
            if (!outputManifestContentDigestResult.IsSuccess)
            {
                return BuildExecutionResult.Failure(outputManifestContentDigestResult.Error!, project);
            }

            if (!string.Equals(outputManifestContentDigestResult.Digest, outputManifestResult.Manifest!.ManifestDigest, StringComparison.Ordinal))
            {
                return BuildExecutionResult.Failure(ExecutionError.InternalError(
                    "Build output manifest digest did not match the written manifest content.",
                    BuildErrorCodes.BuildOutputDigestMismatch), project);
            }

            var outputManifestDigestResult = await artifactStore.CalculateDigestAsync(paths.OutputManifestPath, artifactCancellationToken).ConfigureAwait(false);
            if (!outputManifestDigestResult.IsSuccess)
            {
                return BuildExecutionResult.Failure(outputManifestDigestResult.Error!, project);
            }

            var output = CreateOutput(
                project,
                runId,
                profileReadResult.DisplayPath!,
                profile,
                buildResponse,
                outputManifestResult.Manifest,
                paths,
                buildReportDigestResult.Digest!,
                buildLogDigestResult.Digest!,
                outputManifestDigestResult.Digest!);
            var metadataReports = CreateReports(
                paths,
                buildReportDigestResult.Digest!,
                buildLogDigestResult.Digest!,
                outputManifestDigestResult.Digest!,
                buildDigest: null);
            var metadata = new BuildRunMetadata(
                SchemaVersion: BuildMetadataSchemaVersion,
                RunId: runId,
                Project: project,
                Profile: output.Build.Profile,
                Input: new BuildRunInputMetadata(
                    Target: output.Build.Target,
                    UnityBuildTarget: buildResponse.Input.UnityBuildTarget,
                    Scenes: output.Build.Scenes,
                    Options: output.Build.Options),
                Lifecycle: new BuildRunLifecycleMetadata(
                    Before: buildResponse.LifecycleBefore,
                    After: buildResponse.LifecycleAfter),
                Generations: output.Build.Generations,
                Summary: output.Build.Summary,
                Logs: output.Build.Logs,
                Output: output.Build.Output,
                Artifacts: metadataReports,
                DirtyState: buildResponse.DirtyState);
            var metadataWriteResult = await artifactStore.WriteMetadataAsync(paths, metadata, artifactCancellationToken).ConfigureAwait(false);
            if (!metadataWriteResult.IsSuccess)
            {
                return BuildExecutionResult.Failure(metadataWriteResult.Error!, project);
            }

            var reports = CreateReports(
                paths,
                buildReportDigestResult.Digest!,
                buildLogDigestResult.Digest!,
                outputManifestDigestResult.Digest!,
                metadataWriteResult.Digest);
            return BuildExecutionResult.Success(output with
            {
                Reports = reports,
            });
        }
        catch (OperationCanceledException) when (artifactAccountingCancellationTokenSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return BuildExecutionResult.Failure(CreateTimeoutFailure(timeout), project);
        }
    }

    private static UnityRequestPayload.BuildRun CreateBuildRunRequest (
        ResolvedBuildProfile profile,
        BuildRunArtifactPaths paths,
        string runId)
    {
        return new UnityRequestPayload.BuildRun(
            RunId: runId,
            TargetStableName: profile.Target.StableName,
            UnityBuildTarget: profile.Target.UnityBuildTargetLiteral,
            SceneSource: ContractLiteralCodec.ToValue(profile.Scenes.Source),
            ScenePaths: profile.Scenes.Paths,
            Development: profile.Options.Development,
            OutputPath: paths.OutputDirectory,
            BuildReportPath: paths.BuildReportPath,
            BuildLogPath: paths.BuildLogPath);
    }

    private static BuildResponseResolutionResult ResolveBuildResponse (
        UnityRequestResponse response,
        string expectedRunId,
        string expectedProjectFingerprint)
    {
        if (response.HasFailureStatus || response.Errors.Count != 0)
        {
            var firstError = response.Errors.FirstOrDefault();
            var failure = ApplicationFailure.FromCode(
                firstError?.Code,
                firstError?.Message ?? $"Unity build IPC failed with status '{response.FailureStatus}'.",
                firstError?.OpId);
            return BuildResponseResolutionResult.Failure(failure, TryReadErrorPayload(response));
        }

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcBuildRunResponse buildResponse, out var payloadError))
        {
            return BuildResponseResolutionResult.Failure(ApplicationFailure.InternalError(
                $"Unity build payload is invalid. {payloadError.Message}"));
        }

        var validationFailure = ValidateResponse(buildResponse, expectedRunId, expectedProjectFingerprint);
        return validationFailure != null
            ? BuildResponseResolutionResult.Failure(validationFailure)
            : BuildResponseResolutionResult.Success(buildResponse);
    }

    private static IpcBuildRunErrorPayload? TryReadErrorPayload (UnityRequestResponse response)
    {
        return IpcPayloadCodec.TryDeserialize(response.Payload, out IpcBuildRunErrorPayload payload, out _)
            ? payload
            : null;
    }

    private static ApplicationFailure? ValidateResponse (
        IpcBuildRunResponse response,
        string expectedRunId,
        string expectedProjectFingerprint)
    {
        if (!string.Equals(response.RunId, expectedRunId, StringComparison.Ordinal))
        {
            return ApplicationFailure.InternalError(
                $"Unity build response runId mismatch. Requested={expectedRunId}, Actual={response.RunId}.");
        }

        if (!string.Equals(response.ProjectFingerprint, expectedProjectFingerprint, StringComparison.Ordinal))
        {
            return ApplicationFailure.InternalError(
                $"Unity build response projectFingerprint mismatch. Requested={expectedProjectFingerprint}, Actual={response.ProjectFingerprint}.");
        }

        if (!ContractLiteralCodec.TryParse<IpcBuildReportResult>(response.Report.Result, out _))
        {
            return ApplicationFailure.InternalError($"Unity build response contains unsupported report result: {response.Report.Result}.");
        }

        if (!ContractLiteralCodec.TryParse<IpcBuildLogCompletionReason>(response.Logs.CompletionReason, out _))
        {
            return ApplicationFailure.InternalError($"Unity build response contains unsupported log completionReason: {response.Logs.CompletionReason}.");
        }

        if (!ContractLiteralCodec.TryParse<BuildProfileSceneSource>(response.Input.SceneSource, out _))
        {
            return ApplicationFailure.InternalError($"Unity build response contains unsupported scene source: {response.Input.SceneSource}.");
        }

        if (response.Input.Scenes.Count == 0)
        {
            return ApplicationFailure.InternalError("Unity build response contains no resolved build scenes.");
        }

        for (var i = 0; i < response.Input.Scenes.Count; i++)
        {
            if (!UnityAssetPathContract.IsNormalizedSceneAssetPath(response.Input.Scenes[i]))
            {
                return ApplicationFailure.InternalError(
                    $"Unity build response contains invalid resolved scene path at index {i}: {response.Input.Scenes[i]}.");
            }
        }

        return null;
    }

    private static BuildExecutionOutput CreateOutput (
        ProjectIdentityInfo project,
        string runId,
        string profilePath,
        ResolvedBuildProfile profile,
        IpcBuildRunResponse response,
        BuildOutputManifest outputManifest,
        BuildRunArtifactPaths paths,
        string buildReportDigest,
        string buildLogDigest,
        string outputManifestDigest)
    {
        var generations = CreateGenerations(response.LifecycleBefore, response.LifecycleAfter);
        var summary = new BuildSummaryOutput(
            Result: response.Report.Result,
            DurationMilliseconds: response.Report.DurationMilliseconds,
            ErrorCount: response.Report.ErrorCount,
            WarningCount: response.Report.WarningCount,
            ReportRef: BuildReportRefs.BuildReport);
        var logs = new BuildLogsOutput(
            ReportRef: BuildReportRefs.BuildLog,
            EntryCount: response.Logs.EntryCount,
            ErrorCount: response.Logs.ErrorCount,
            WarningCount: response.Logs.WarningCount,
            CompletionReason: response.Logs.CompletionReason,
            Window: new BuildLogWindowOutput(
                StartedAtUtc: response.Logs.Window.StartedAtUtc,
                CompletedAtUtc: response.Logs.Window.CompletedAtUtc));
        var build = new BuildOutput(
            RunId: runId,
            Profile: new BuildProfileOutput(profilePath, profile.Digest),
            Target: profile.Target.StableName,
            Scenes: new BuildScenesOutput(
                Source: response.Input.SceneSource,
                Paths: response.Input.Scenes),
            Options: new BuildOptionsOutput(profile.Options.Development),
            Output: new BuildArtifactOutput(
                Kind: ContractLiteralCodec.ToValue(profile.Output.Kind),
                ArtifactRoot: paths.RunDirectory,
                OutputRoot: paths.OutputDirectory,
                ManifestRef: BuildReportRefs.BuildOutputManifest,
                ManifestDigest: outputManifest.ManifestDigest,
                FileCount: outputManifest.FileCount,
                TotalBytes: outputManifest.TotalBytes),
            Generations: generations,
            Summary: summary,
            Logs: logs);
        var reports = CreateReports(
            paths,
            buildReportDigest,
            buildLogDigest,
            outputManifestDigest,
            buildDigest: null);
        var claims = CreateClaims(response, build);
        return new BuildExecutionOutput(
            Verdict: RecalculateVerdict(claims),
            Project: project,
            Build: build,
            Verifiers:
            [
                new BuildVerifierOutput(
                    Id: BuildReportRefs.Build,
                    Kind: BuildReportRefs.Build,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: BuildClaimCodes.All.Select(static code => code.Value).ToArray(),
                    Effects: ContractLiteralCodec.GetLiterals<BuildEffect>(),
                    ReportRef: BuildReportRefs.Build),
            ],
            Claims: claims,
            Reports: reports,
            ResidualRisks: EmptyResidualRisks);
    }

    private static IReadOnlyDictionary<string, BuildReportOutput> CreateReports (
        BuildRunArtifactPaths paths,
        string buildReportDigest,
        string buildLogDigest,
        string outputManifestDigest,
        string? buildDigest)
    {
        return new Dictionary<string, BuildReportOutput>(StringComparer.Ordinal)
        {
            [BuildReportRefs.Build] = new BuildReportOutput(BuildReportRefs.Build, paths.BuildJsonPath, buildDigest),
            [BuildReportRefs.BuildReport] = new BuildReportOutput(BuildReportRefs.BuildReport, paths.BuildReportPath, buildReportDigest),
            [BuildReportRefs.BuildOutputManifest] = new BuildReportOutput(BuildReportRefs.BuildOutputManifest, paths.OutputManifestPath, outputManifestDigest),
            [BuildReportRefs.BuildLog] = new BuildReportOutput(BuildReportRefs.BuildLog, paths.BuildLogPath, buildLogDigest),
        };
    }

    private static BuildGenerationsOutput CreateGenerations (
        IpcBuildLifecycleSnapshot before,
        IpcBuildLifecycleSnapshot after)
    {
        var beforeSnapshot = CreateGenerationSnapshot(before);
        var afterSnapshot = CreateGenerationSnapshot(after);
        return new BuildGenerationsOutput(
            Before: beforeSnapshot,
            After: afterSnapshot,
            ValidFor: afterSnapshot);
    }

    private static BuildGenerationSnapshotOutput CreateGenerationSnapshot (IpcBuildLifecycleSnapshot snapshot)
    {
        return new BuildGenerationSnapshotOutput(
            CompileGeneration: NormalizeGeneration(snapshot.CompileGeneration),
            DomainReloadGeneration: NormalizeGeneration(snapshot.DomainReloadGeneration),
            AssetRefreshGeneration: NormalizeGeneration(snapshot.AssetRefreshGeneration));
    }

    private static string NormalizeGeneration (string? generation)
    {
        return string.IsNullOrWhiteSpace(generation) ? UnknownGeneration : generation;
    }

    private static IReadOnlyList<BuildClaimOutput> CreateClaims (
        IpcBuildRunResponse response,
        BuildOutput build)
    {
        var reportResult = ContractLiteralCodec.TryParse<IpcBuildReportResult>(response.Report.Result, out var parsedResult)
            ? parsedResult
            : IpcBuildReportResult.Unknown;
        var succeeded = reportResult == IpcBuildReportResult.Succeeded;
        var knownTerminalResult = reportResult is IpcBuildReportResult.Succeeded or IpcBuildReportResult.Failed or IpcBuildReportResult.Canceled;

        return
        [
            CreateClaim(
                BuildClaimCodes.UnityBuildProfileResolved,
                BuildClaimStatus.Passed,
                "Build profile resolved to a deterministic input digest.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["path"] = build.Profile.Path,
                    ["digest"] = build.Profile.Digest,
                },
                [new BuildEvidenceOutput(Kind: "buildProfile", EvidenceRef: BuildReportRefs.Build, Data: build.Profile)]),
            CreateClaim(
                BuildClaimCodes.UnityReadyForBuild,
                response.LifecycleBefore.CanAcceptExecutionRequests ? BuildClaimStatus.Passed : BuildClaimStatus.Failed,
                "Unity lifecycle was ready before BuildPipeline execution.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["lifecycleState"] = response.LifecycleBefore.LifecycleState,
                },
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEffect.UnityLifecycleRead), Data: response.LifecycleBefore)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildInputsResolved,
                BuildClaimStatus.Passed,
                "Unity resolved BuildPipeline target and scenes.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["target"] = build.Target,
                    ["sceneCount"] = build.Scenes.Paths.Count,
                },
                [new BuildEvidenceOutput(Kind: "buildInput", EvidenceRef: BuildReportRefs.Build, Data: response.Input)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildCompleted,
                knownTerminalResult ? BuildClaimStatus.Passed : BuildClaimStatus.Indeterminate,
                "Unity BuildPipeline reached a terminal BuildReport result.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["result"] = build.Summary.Result,
                },
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEffect.UnityBuildPipeline), EvidenceRef: BuildReportRefs.BuildReport, Data: build.Summary)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildSucceeded,
                succeeded ? BuildClaimStatus.Passed : BuildClaimStatus.Failed,
                "Unity BuildPipeline reported a successful build result.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["result"] = build.Summary.Result,
                    ["errorCount"] = build.Summary.ErrorCount,
                },
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEffect.UnityBuildReportRead), EvidenceRef: BuildReportRefs.BuildReport, Data: build.Summary)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildReportAccounted,
                BuildClaimStatus.Passed,
                "BuildReport artifact was written and digested.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["reportRef"] = BuildReportRefs.BuildReport,
                },
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEffect.UnityBuildReportRead), EvidenceRef: BuildReportRefs.BuildReport)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildArtifactsAccounted,
                BuildClaimStatus.Passed,
                "Build output artifacts were counted in the output manifest.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["manifestRef"] = BuildReportRefs.BuildOutputManifest,
                    ["fileCount"] = build.Output.FileCount,
                },
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEffect.OutputManifestWrite), EvidenceRef: BuildReportRefs.Build, Data: build.Output)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildOutputDigested,
                BuildClaimStatus.Passed,
                "Build output manifest digest was verified against the written artifact.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["manifestDigest"] = build.Output.ManifestDigest,
                },
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEffect.OutputManifestWrite), EvidenceRef: BuildReportRefs.BuildOutputManifest)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildLogsAccounted,
                BuildClaimStatus.Passed,
                "Build log byte range was written and summarized.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["reportRef"] = BuildReportRefs.BuildLog,
                    ["entryCount"] = build.Logs.EntryCount,
                    ["completionReason"] = build.Logs.CompletionReason,
                },
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEffect.UnityLogWindowRead), EvidenceRef: BuildReportRefs.BuildLog, Data: build.Logs)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildValidForGeneration,
                HasCompleteGenerationSnapshot(build.Generations) ? BuildClaimStatus.Passed : BuildClaimStatus.Indeterminate,
                "Build artifacts declare the Unity lifecycle generations they are valid for.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["compileGeneration"] = build.Generations.ValidFor.CompileGeneration,
                    ["domainReloadGeneration"] = build.Generations.ValidFor.DomainReloadGeneration,
                    ["assetRefreshGeneration"] = build.Generations.ValidFor.AssetRefreshGeneration,
                },
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEffect.GenerationSnapshot), EvidenceRef: BuildReportRefs.Build, Data: build.Generations)]),
        ];
    }

    private static bool HasCompleteGenerationSnapshot (BuildGenerationsOutput generations)
    {
        return IsKnownGeneration(generations.Before.CompileGeneration)
            && IsKnownGeneration(generations.Before.DomainReloadGeneration)
            && IsKnownGeneration(generations.Before.AssetRefreshGeneration)
            && IsKnownGeneration(generations.After.CompileGeneration)
            && IsKnownGeneration(generations.After.DomainReloadGeneration)
            && IsKnownGeneration(generations.After.AssetRefreshGeneration)
            && IsKnownGeneration(generations.ValidFor.CompileGeneration)
            && IsKnownGeneration(generations.ValidFor.DomainReloadGeneration)
            && IsKnownGeneration(generations.ValidFor.AssetRefreshGeneration);
    }

    private static bool IsKnownGeneration (string generation)
    {
        return !string.IsNullOrWhiteSpace(generation)
            && !string.Equals(generation, UnknownGeneration, StringComparison.Ordinal);
    }

    private static BuildClaimOutput CreateClaim (
        UcliCode id,
        BuildClaimStatus status,
        string statement,
        IReadOnlyDictionary<string, object?> subject,
        IReadOnlyList<BuildEvidenceOutput> evidence)
    {
        return new BuildClaimOutput(
            Id: id.Value,
            Status: ContractLiteralCodec.ToValue(status),
            Coverage: ContractLiteralCodec.ToValue(status == BuildClaimStatus.Indeterminate ? BuildCoverage.None : BuildCoverage.Full),
            Required: true,
            VerifierRef: BuildReportRefs.Build,
            Statement: statement,
            Subject: subject,
            Evidence: evidence,
            ResidualRisks: EmptyResidualRisks);
    }

    private static string RecalculateVerdict (IReadOnlyList<BuildClaimOutput> claims)
    {
        return AssuranceVerdictCalculator.Calculate(
            claims
                .Select(static claim => new AssuranceVerdictClaimState(
                    Status: claim.Status,
                    Coverage: claim.Coverage,
                    Required: claim.Required,
                    HasBlockingResidualRisk: claim.ResidualRisks.Any(static risk => risk.Blocking)))
                .ToArray(),
            EmptyResidualRisks
                .Select(static risk => new AssuranceVerdictResidualRiskState(risk.Blocking))
                .ToArray());
    }

    private static UnityExecutionMode ResolveExecutionMode (UnityExecutionTarget executionTarget)
    {
        return executionTarget switch
        {
            UnityExecutionTarget.Daemon => UnityExecutionMode.Daemon,
            UnityExecutionTarget.Oneshot => UnityExecutionMode.Oneshot,
            _ => throw new ArgumentOutOfRangeException(nameof(executionTarget), executionTarget, "Unsupported execution target."),
        };
    }

    private static ApplicationFailure CreateTimeoutFailure (TimeSpan timeout)
    {
        return ApplicationFailure.Timeout(
            $"Unity build assurance timed out after {timeout.TotalMilliseconds:0} milliseconds.",
            ExecutionErrorCodes.IpcTimeout);
    }

    private sealed record BuildResponseResolutionResult (
        IpcBuildRunResponse? Response,
        ApplicationFailure? Error,
        IpcBuildRunErrorPayload? ErrorPayload)
    {
        public bool IsSuccess => Response != null && Error == null;

        public static BuildResponseResolutionResult Success (IpcBuildRunResponse response)
        {
            ArgumentNullException.ThrowIfNull(response);
            return new BuildResponseResolutionResult(response, null, null);
        }

        public static BuildResponseResolutionResult Failure (
            ApplicationFailure failure,
            IpcBuildRunErrorPayload? errorPayload = null)
        {
            ArgumentNullException.ThrowIfNull(failure);
            return new BuildResponseResolutionResult(null, failure, errorPayload);
        }
    }
}
