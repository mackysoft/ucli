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
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Execution;

/// <summary> Executes Unity build assurance runs and persists build artifacts. </summary>
internal sealed class BuildService : IBuildService
{
    private const int BuildMetadataSchemaVersion = 1;
    private const string BuildReportRef = "buildReport";
    private const string BuildLogRef = "buildLog";
    private const string BuildMetadataRef = "build";
    private const string BuildOutputManifestRef = "buildOutputManifest";
    private const string BuildVerifierId = "build";
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
            return BuildExecutionResult.Failure(
                responseResult.Error!,
                project,
                responseResult.ErrorPayload?.DirtyState,
                responseResult.ErrorPayload?.Input);
        }

        var buildResponse = responseResult.Response!;
        var outputManifestResult = await artifactStore.WriteOutputManifestAsync(
                paths,
                profile.Target.StableName,
                cancellationToken)
            .ConfigureAwait(false);
        if (!outputManifestResult.IsSuccess)
        {
            return BuildExecutionResult.Failure(outputManifestResult.Error!, project);
        }

        var buildReportDigestResult = await artifactStore.CalculateRequiredDigestAsync(paths.BuildReportPath, BuildErrorCodes.BuildReportMissing, cancellationToken).ConfigureAwait(false);
        if (!buildReportDigestResult.IsSuccess)
        {
            return BuildExecutionResult.Failure(buildReportDigestResult.Error!, project);
        }

        var buildLogDigestResult = await artifactStore.CalculateDigestAsync(paths.BuildLogPath, cancellationToken).ConfigureAwait(false);
        if (!buildLogDigestResult.IsSuccess)
        {
            return BuildExecutionResult.Failure(buildLogDigestResult.Error!, project);
        }

        var outputManifestDigestResult = await artifactStore.CalculateDigestAsync(paths.OutputManifestPath, cancellationToken).ConfigureAwait(false);
        if (!outputManifestDigestResult.IsSuccess)
        {
            return BuildExecutionResult.Failure(outputManifestDigestResult.Error!, project);
        }

        if (!string.Equals(outputManifestDigestResult.Digest, outputManifestResult.Manifest!.ManifestDigest, StringComparison.Ordinal))
        {
            return BuildExecutionResult.Failure(ExecutionError.InternalError(
                "Build output manifest digest did not match the written manifest content.",
                BuildErrorCodes.BuildOutputDigestMismatch), project);
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
            Generations: output.Build.Generations,
            Summary: output.Build.Summary,
            Logs: output.Build.Logs,
            Output: output.Build.Output,
            Artifacts: metadataReports,
            DirtyState: buildResponse.DirtyState);
        var metadataWriteResult = await artifactStore.WriteMetadataAsync(paths, metadata, cancellationToken).ConfigureAwait(false);
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
            ReportRef: BuildReportRef);
        var logs = new BuildLogsOutput(
            ReportRef: BuildLogRef,
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
                Source: ContractLiteralCodec.ToValue(profile.Scenes.Source),
                Paths: profile.Scenes.Paths),
            Options: new BuildOptionsOutput(profile.Options.Development),
            Output: new BuildArtifactOutput(
                RootPath: paths.OutputDirectory,
                ManifestRef: BuildOutputManifestRef,
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
                    Id: BuildVerifierId,
                    Kind: BuildVerifierId,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: BuildClaimCodes.All.Select(static code => code.Value).ToArray(),
                    Effects: ContractLiteralCodec.GetLiterals<BuildEffect>(),
                    ReportRef: BuildMetadataRef),
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
            [BuildMetadataRef] = new BuildReportOutput("build.metadata", paths.BuildJsonPath, buildDigest),
            [BuildReportRef] = new BuildReportOutput("build.report", paths.BuildReportPath, buildReportDigest),
            [BuildOutputManifestRef] = new BuildReportOutput("build.outputManifest", paths.OutputManifestPath, outputManifestDigest),
            [BuildLogRef] = new BuildReportOutput("build.log", paths.BuildLogPath, buildLogDigest),
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
                [new BuildEvidenceOutput(Kind: "buildProfile", Data: build.Profile)]),
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
                [new BuildEvidenceOutput(Kind: "buildInput", Data: response.Input)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildCompleted,
                knownTerminalResult ? BuildClaimStatus.Passed : BuildClaimStatus.Indeterminate,
                "Unity BuildPipeline reached a terminal BuildReport result.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["result"] = build.Summary.Result,
                },
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEffect.UnityBuildPipeline), EvidenceRef: BuildReportRef, Data: build.Summary)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildSucceeded,
                succeeded ? BuildClaimStatus.Passed : BuildClaimStatus.Failed,
                "Unity BuildPipeline reported a successful build result.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["result"] = build.Summary.Result,
                    ["errorCount"] = build.Summary.ErrorCount,
                },
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEffect.UnityBuildReportRead), EvidenceRef: BuildReportRef, Data: build.Summary)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildReportAccounted,
                BuildClaimStatus.Passed,
                "BuildReport artifact was written and digested.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["reportRef"] = BuildReportRef,
                },
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEffect.UnityBuildReportRead), EvidenceRef: BuildReportRef)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildArtifactsAccounted,
                BuildClaimStatus.Passed,
                "Build output artifacts were counted in the output manifest.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["manifestRef"] = BuildOutputManifestRef,
                    ["fileCount"] = build.Output.FileCount,
                },
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEffect.OutputManifestWrite), EvidenceRef: BuildOutputManifestRef, Data: build.Output)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildOutputDigested,
                BuildClaimStatus.Passed,
                "Build output manifest digest was verified against the written artifact.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["manifestDigest"] = build.Output.ManifestDigest,
                },
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEffect.OutputManifestWrite), EvidenceRef: BuildOutputManifestRef)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildLogsAccounted,
                BuildClaimStatus.Passed,
                "Build log byte range was written and summarized.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["reportRef"] = BuildLogRef,
                    ["entryCount"] = build.Logs.EntryCount,
                    ["completionReason"] = build.Logs.CompletionReason,
                },
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEffect.UnityLogWindowRead), EvidenceRef: BuildLogRef, Data: build.Logs)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildValidForGeneration,
                BuildClaimStatus.Passed,
                "Build artifacts declare the Unity lifecycle generations they are valid for.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["compileGeneration"] = build.Generations.ValidFor.CompileGeneration,
                    ["domainReloadGeneration"] = build.Generations.ValidFor.DomainReloadGeneration,
                    ["assetRefreshGeneration"] = build.Generations.ValidFor.AssetRefreshGeneration,
                },
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEffect.GenerationSnapshot), Data: build.Generations)]),
        ];
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
            VerifierRef: BuildVerifierId,
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
