using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Metadata;
using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Execution;

/// <summary> Executes Unity build assurance runs and persists build artifacts. </summary>
internal sealed class BuildService : IBuildService
{
    private const int BuildMetadataSchemaVersion = 1;
    private const string UnknownGeneration = "unknown";
    private const string UcliArtifactOutputKind = "ucliArtifact";

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

        var resolvedProgressSink = progressSink ?? NullCommandProgressSink.Instance;
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

        var executionTarget = modeDecisionResult.Decision!.Target;
        if (!deadline.TryGetRemainingTimeout(out var requestTimeout))
        {
            return BuildExecutionResult.Failure(CreateTimeoutFailure(timeout), project);
        }

        var runId = runIdFactory.Create();
        var prepareResult = artifactStore.Prepare(context.UnityProject, runId);
        if (!prepareResult.IsSuccess)
        {
            return BuildExecutionResult.Failure(prepareResult.Error!, project);
        }

        var profile = profileResolutionResult.Profile!;
        var paths = prepareResult.Paths!;
        await EmitStartedAsync(
                resolvedProgressSink,
                runId,
                project,
                requestedMode,
                executionTarget,
                timeout,
                profile.BuildTarget.StableName,
                paths.OutputDirectory,
                cancellationToken)
            .ConfigureAwait(false);

        var request = CreateBuildRunRequest(profile, paths, runId);
        var executionResult = await unityRequestExecutor.ExecuteAsync(
                UcliCommandIds.BuildRun,
                ResolveExecutionMode(executionTarget),
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

        var responseResult = ResolveBuildResponse(
            executionResult.Response!,
            runId,
            context.UnityProject.ProjectFingerprint,
            profile);
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
            var accountingResult = await artifactStore.AccountArtifactsAsync(
                    new BuildRunArtifactAccountingRequest(
                        paths,
                        buildResponse.Report.OutputPath,
                        profile.BuildTarget.StableName),
                    artifactCancellationToken)
                .ConfigureAwait(false);
            if (!accountingResult.IsSuccess)
            {
                return BuildExecutionResult.Failure(accountingResult.Error!, project);
            }

            var accounting = accountingResult.Result!;
            var output = CreateOutput(
                project,
                runId,
                profileReadResult.DisplayPath!,
                profile,
                buildResponse,
                accounting,
                paths);
            var metadata = CreateMetadataDocument(
                project,
                output,
                buildResponse);
            var metadataWriteResult = await artifactStore.WriteMetadataAsync(
                    new BuildRunMetadataWriteRequest(
                        paths,
                        metadata,
                        accounting),
                    artifactCancellationToken)
                .ConfigureAwait(false);
            if (!metadataWriteResult.IsSuccess)
            {
                return BuildExecutionResult.Failure(metadataWriteResult.Error!, project);
            }

            var completedOutput = output with
            {
                Reports = CreateReports(
                    paths,
                    accounting,
                    metadataWriteResult.Artifact!),
            };
            await EmitCompletedAsync(
                    resolvedProgressSink,
                    completedOutput,
                    paths,
                    cancellationToken)
                .ConfigureAwait(false);
            return BuildExecutionResult.Success(completedOutput);
        }
        catch (OperationCanceledException) when (artifactAccountingCancellationTokenSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return BuildExecutionResult.Failure(CreateTimeoutFailure(timeout), project);
        }
    }

    private static ValueTask EmitStartedAsync (
        ICommandProgressSink progressSink,
        string runId,
        ProjectIdentityInfo project,
        UnityExecutionMode requestedMode,
        UnityExecutionTarget executionTarget,
        TimeSpan timeout,
        string buildTarget,
        string outputPath,
        CancellationToken cancellationToken)
    {
        return progressSink.OnEntryAsync(
            BuildRunProgressEventNames.Started,
            new BuildRunStartedEntry(
                RunId: runId,
                ProjectFingerprint: project.ProjectFingerprint,
                RequestedMode: AssuranceExecutionModeCodec.ToRequestedModeValue(requestedMode),
                ResolvedMode: AssuranceExecutionModeCodec.ToResolvedModeValue(executionTarget),
                SessionKind: AssuranceExecutionModeCodec.ToSessionKindValue(executionTarget),
                TimeoutMilliseconds: checked((int)timeout.TotalMilliseconds),
                BuildTarget: buildTarget,
                OutputPath: outputPath),
            cancellationToken);
    }

    private static ValueTask EmitCompletedAsync (
        ICommandProgressSink progressSink,
        BuildExecutionOutput output,
        BuildRunArtifactPaths paths,
        CancellationToken cancellationToken)
    {
        return progressSink.OnEntryAsync(
            BuildRunProgressEventNames.Completed,
            new BuildRunCompletedEntry(
                RunId: output.Build.RunId,
                Verdict: output.Verdict,
                Result: output.Build.Summary.Result,
                CompletionReason: output.Build.Logs.CompletionReason,
                ErrorCount: output.Build.Summary.ErrorCount,
                WarningCount: output.Build.Summary.WarningCount,
                BuildJsonPath: paths.BuildJsonPath,
                BuildReportPath: paths.BuildReportJsonPath,
                BuildLogPath: paths.BuildLogPath,
                OutputManifestPath: paths.OutputManifestJsonPath),
            cancellationToken);
    }

    private static UnityRequestPayload.BuildRun CreateBuildRunRequest (
        ResolvedBuildProfile profile,
        BuildRunArtifactPaths paths,
        string runId)
    {
        return new UnityRequestPayload.BuildRun(
            RunId: runId,
            BuildTarget: profile.BuildTarget.StableName,
            UnityBuildTarget: profile.BuildTarget.UnityBuildTargetLiteral,
            SceneSource: ContractLiteralCodec.ToValue(profile.Scenes.Source),
            ScenePaths: profile.Scenes.Paths,
            Development: profile.Options.Development,
            OutputPath: paths.OutputDirectory,
            BuildReportPath: paths.BuildReportJsonPath,
            BuildLogPath: paths.BuildLogPath);
    }

    private static BuildResponseResolutionResult ResolveBuildResponse (
        UnityRequestResponse response,
        string expectedRunId,
        string expectedProjectFingerprint,
        ResolvedBuildProfile expectedProfile)
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

        var validationFailure = ValidateResponse(
            buildResponse,
            expectedRunId,
            expectedProjectFingerprint,
            expectedProfile);
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
        string expectedProjectFingerprint,
        ResolvedBuildProfile expectedProfile)
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

        if (!string.Equals(response.Input.BuildTarget, expectedProfile.BuildTarget.StableName, StringComparison.Ordinal))
        {
            return ApplicationFailure.InternalError(
                $"Unity build response buildTarget mismatch. Requested={expectedProfile.BuildTarget.StableName}, Actual={response.Input.BuildTarget}.");
        }

        if (!string.Equals(response.Input.UnityBuildTarget, expectedProfile.BuildTarget.UnityBuildTargetLiteral, StringComparison.Ordinal))
        {
            return ApplicationFailure.InternalError(
                $"Unity build response Unity BuildTarget mismatch. Requested={expectedProfile.BuildTarget.UnityBuildTargetLiteral}, Actual={response.Input.UnityBuildTarget}.");
        }

        var expectedSceneSource = ContractLiteralCodec.ToValue(expectedProfile.Scenes.Source);
        if (!ContractLiteralCodec.TryParse<BuildProfileSceneSource>(response.Input.SceneSource, out var sceneSource))
        {
            return ApplicationFailure.InternalError($"Unity build response contains unsupported scene source: {response.Input.SceneSource}.");
        }

        if (sceneSource != expectedProfile.Scenes.Source)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response scene source mismatch. Requested={expectedSceneSource}, Actual={response.Input.SceneSource}.");
        }

        if (!HasExpectedDevelopmentBuildOption(response.Input.BuildOptions, expectedProfile.Options.Development))
        {
            return ApplicationFailure.InternalError(
                $"Unity build response build options mismatch. RequestedDevelopment={expectedProfile.Options.Development}, Actual={response.Input.BuildOptions}.");
        }

        if (!ContractLiteralCodec.TryParse<IpcBuildReportResult>(response.Report.Result, out var reportResult))
        {
            return ApplicationFailure.InternalError($"Unity build response contains unsupported report result: {response.Report.Result}.");
        }

        if (!ContractLiteralCodec.TryParse<IpcBuildLogCompletionReason>(response.Logs.CompletionReason, out var completionReason))
        {
            return ApplicationFailure.InternalError($"Unity build response contains unsupported log completionReason: {response.Logs.CompletionReason}.");
        }

        var expectedCompletionReason = IpcBuildLogCompletionReasonResolver.FromReportResult(reportResult);
        if (completionReason != expectedCompletionReason)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response log completionReason mismatch. Expected={ContractLiteralCodec.ToValue(expectedCompletionReason)}, Actual={response.Logs.CompletionReason}.");
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

        if (expectedProfile.Scenes.Source == BuildProfileSceneSource.Explicit
            && !response.Input.Scenes.SequenceEqual(expectedProfile.Scenes.Paths, StringComparer.Ordinal))
        {
            return ApplicationFailure.InternalError("Unity build response resolved scenes do not match the requested explicit build scenes.");
        }

        if (!string.Equals(response.Report.UnityBuildTarget, response.Input.UnityBuildTarget, StringComparison.Ordinal))
        {
            return ApplicationFailure.InternalError(
                $"Unity BuildReport BuildTarget mismatch. Input={response.Input.UnityBuildTarget}, Report={response.Report.UnityBuildTarget}.");
        }

        return null;
    }

    private static bool HasExpectedDevelopmentBuildOption (
        string buildOptions,
        bool expectedDevelopment)
    {
        if (string.IsNullOrWhiteSpace(buildOptions))
        {
            return false;
        }

        return ContainsBuildOption(buildOptions, "Development") == expectedDevelopment;
    }

    private static bool ContainsBuildOption (
        string buildOptions,
        string option)
    {
        var remaining = buildOptions.AsSpan();
        while (!remaining.IsEmpty)
        {
            var separatorIndex = remaining.IndexOf(',');
            var part = separatorIndex < 0
                ? remaining
                : remaining[..separatorIndex];
            if (part.Trim().SequenceEqual(option.AsSpan()))
            {
                return true;
            }

            if (separatorIndex < 0)
            {
                return false;
            }

            remaining = remaining[(separatorIndex + 1)..];
        }

        return false;
    }

    private static BuildExecutionOutput CreateOutput (
        ProjectIdentityInfo project,
        string runId,
        string profilePath,
        ResolvedBuildProfile profile,
        IpcBuildRunResponse response,
        BuildRunArtifactAccountingResult accounting,
        BuildRunArtifactPaths paths)
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
            BuildTarget: profile.BuildTarget.StableName,
            Scenes: new BuildScenesOutput(
                Source: response.Input.SceneSource,
                Paths: response.Input.Scenes),
            Options: new BuildOptionsOutput(profile.Options.Development),
            Output: new BuildArtifactOutput(
                Kind: UcliArtifactOutputKind,
                ArtifactRoot: paths.ArtifactsDirectory,
                OutputRoot: paths.OutputDirectory,
                ManifestRef: BuildReportRefs.BuildOutputManifest,
                ManifestDigest: accounting.OutputManifest.ManifestDigest,
                FileCount: accounting.OutputManifest.FileCount,
                TotalBytes: accounting.OutputManifest.TotalBytes),
            Generations: generations,
            Summary: summary,
            Logs: logs);
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
            Reports: CreateReports(paths, accounting, buildArtifact: null),
            ResidualRisks: EmptyResidualRisks);
    }

    private static IReadOnlyDictionary<string, BuildReportOutput> CreateReports (
        BuildRunArtifactPaths paths,
        BuildRunArtifactAccountingResult accounting,
        BuildArtifactRef? buildArtifact)
    {
        return new Dictionary<string, BuildReportOutput>(StringComparer.Ordinal)
        {
            [BuildReportRefs.Build] = new BuildReportOutput(BuildReportRefs.Build, paths.BuildJsonPath, buildArtifact?.Digest),
            [BuildReportRefs.BuildReport] = new BuildReportOutput(BuildReportRefs.BuildReport, paths.BuildReportJsonPath, accounting.BuildReport.Digest),
            [BuildReportRefs.BuildOutputManifest] = new BuildReportOutput(BuildReportRefs.BuildOutputManifest, paths.OutputManifestJsonPath, accounting.BuildOutputManifest.Digest),
            [BuildReportRefs.BuildLog] = new BuildReportOutput(BuildReportRefs.BuildLog, paths.BuildLogPath, accounting.BuildLog.Digest),
        };
    }

    private static BuildRunMetadataDocument CreateMetadataDocument (
        ProjectIdentityInfo project,
        BuildExecutionOutput output,
        IpcBuildRunResponse response)
    {
        return new BuildRunMetadataDocument(
            SchemaVersion: BuildMetadataSchemaVersion,
            RunId: output.Build.RunId,
            Project: SerializeMetadataElement(project),
            Profile: SerializeMetadataElement(output.Build.Profile),
            Input: SerializeMetadataElement(new BuildRunInputMetadata(
                BuildTarget: output.Build.BuildTarget,
                UnityBuildTarget: response.Input.UnityBuildTarget,
                Scenes: output.Build.Scenes,
                Options: output.Build.Options)),
            Lifecycle: SerializeMetadataElement(new BuildRunLifecycleMetadata(
                Before: response.LifecycleBefore,
                After: response.LifecycleAfter)),
            Generations: SerializeMetadataElement(output.Build.Generations),
            Summary: SerializeMetadataElement(output.Build.Summary),
            Logs: SerializeMetadataElement(output.Build.Logs),
            Output: SerializeMetadataElement(output.Build.Output),
            DirtyState: SerializeMetadataElement(response.DirtyState));
    }

    private static JsonElement SerializeMetadataElement<T> (T value)
    {
        return JsonSerializer.SerializeToElement(value, IpcJsonSerializerOptions.Default);
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
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEvidenceKind.BuildProfile), EvidenceRef: BuildReportRefs.Build, Data: build.Profile)]),
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
                "Unity resolved BuildPipeline BuildTarget and scenes.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["buildTarget"] = build.BuildTarget,
                    ["sceneCount"] = build.Scenes.Paths.Count,
                },
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEvidenceKind.BuildInput), EvidenceRef: BuildReportRefs.Build, Data: response.Input)]),
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
