using System.Text;
using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Metadata;
using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.EnvironmentVariables;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Shared.Identifiers;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Execution;

/// <summary> Executes Unity build assurance runs and persists build artifacts. </summary>
internal sealed class BuildService : IBuildService
{
    private const int BuildMetadataSchemaVersion = 1;

    internal static readonly AssuranceVerifierId VerifierId = new("build");

    private static readonly IReadOnlyList<BuildResidualRiskOutput> EmptyResidualRisks =
        Array.Empty<BuildResidualRiskOutput>();

    private readonly IProjectContextResolver projectContextResolver;

    private readonly IBuildProfileFileReader profileFileReader;

    private readonly IEnvironmentVariableReader environmentVariableReader;

    private readonly IUnityExecutionModeDecisionService executionModeDecisionService;

    private readonly IUnityRequestExecutor unityRequestExecutor;

    private readonly IUnityStreamingRequestExecutor unityStreamingRequestExecutor;

    private readonly IGuidGenerator runIdGenerator;

    private readonly IBuildRunArtifactStore artifactStore;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="BuildService" /> class. </summary>
    public BuildService (
        IProjectContextResolver projectContextResolver,
        IBuildProfileFileReader profileFileReader,
        IEnvironmentVariableReader environmentVariableReader,
        IUnityExecutionModeDecisionService executionModeDecisionService,
        IUnityRequestExecutor unityRequestExecutor,
        IUnityStreamingRequestExecutor unityStreamingRequestExecutor,
        IGuidGenerator runIdGenerator,
        IBuildRunArtifactStore artifactStore,
        TimeProvider timeProvider)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.profileFileReader = profileFileReader ?? throw new ArgumentNullException(nameof(profileFileReader));
        this.environmentVariableReader = environmentVariableReader ?? throw new ArgumentNullException(nameof(environmentVariableReader));
        this.executionModeDecisionService = executionModeDecisionService ?? throw new ArgumentNullException(nameof(executionModeDecisionService));
        this.unityRequestExecutor = unityRequestExecutor ?? throw new ArgumentNullException(nameof(unityRequestExecutor));
        this.unityStreamingRequestExecutor = unityStreamingRequestExecutor ?? throw new ArgumentNullException(nameof(unityStreamingRequestExecutor));
        this.runIdGenerator = runIdGenerator ?? throw new ArgumentNullException(nameof(runIdGenerator));
        this.artifactStore = artifactStore ?? throw new ArgumentNullException(nameof(artifactStore));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
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
            return BuildExecutionResult.Failed(
                contextResult.Error!,
                project: null);
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
            return BuildExecutionResult.Failed(profileReadResult.Error!, project);
        }

        var profileResolutionResult = BuildProfileResolver.ResolveJson(profileReadResult.Json!);
        if (!profileResolutionResult.IsSuccess)
        {
            return BuildExecutionResult.Failed(profileResolutionResult.Error!, project);
        }

        var timeoutResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            UcliCommandIds.BuildRun,
            context.Config);
        if (!timeoutResult.IsSuccess)
        {
            return BuildExecutionResult.Failed(timeoutResult.Error!, project);
        }

        var timeout = timeoutResult.Timeout!.Value;
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        var requestedMode = input.Mode ?? UnityExecutionMode.Auto;
        if (!deadline.TryGetRemainingTimeout(out var modeDecisionTimeout))
        {
            return BuildExecutionResult.Failed(CreateTimeoutFailure(timeout), project);
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
                return BuildExecutionResult.Failed(
                    ApplicationFailure.EnvironmentError(contractError.Message, contractError.Code, instancePath: null),
                    project);
            }

            return BuildExecutionResult.Failed(modeDecisionResult.Error!, project);
        }

        var profile = profileResolutionResult.Profile!;
        var executionTarget = modeDecisionResult.Decision!.Target;
        var runtimePolicyFailure = ValidateRuntimePolicy(profile.Policy.Runtime, executionTarget);
        if (runtimePolicyFailure != null)
        {
            return BuildExecutionResult.Failed(runtimePolicyFailure, project);
        }

        if (!deadline.TryGetRemainingTimeout(out var requestTimeout))
        {
            return BuildExecutionResult.Failed(CreateTimeoutFailure(timeout), project);
        }

        var runId = runIdGenerator.Generate();
        var prepareResult = artifactStore.Prepare(context.UnityProject, runId);
        if (!prepareResult.IsSuccess)
        {
            return BuildExecutionResult.Failed(prepareResult.Error!, project);
        }

        var paths = prepareResult.Paths!;
        BuildPipelineOutputLayout? outputLayout = null;
        if (profile.Inputs is ResolvedBuildInputs.Explicit explicitInputs
            && profile.Runner is ResolvedBuildRunner.BuildPipeline)
        {
            if (!BuildPipelineOutputLayoutResolver.TryResolve(
                paths.RunnerOutputDirectory,
                explicitInputs.BuildTarget,
                androidAppBundle: false,
                out outputLayout))
            {
                return BuildExecutionResult.Failed(ExecutionError.InvalidArgument(
                    $"BuildPipeline output layout could not be resolved for build target: {TextVocabulary.GetText(explicitInputs.BuildTarget)}.",
                    BuildErrorCodes.BuildInputsInvalid), project);
            }

            var outputLayoutPrepareResult = artifactStore.PrepareBuildPipelineOutputLayout(
                paths,
                outputLayout!);
            if (!outputLayoutPrepareResult.IsSuccess)
            {
                return BuildExecutionResult.Failed(outputLayoutPrepareResult.Error!, project);
            }
        }

        var runnerInvocationResult = ResolveRunnerInvocation(
            profile,
            profileReadResult.Path!,
            runId,
            paths.RunnerOutputDirectory,
            context.UnityProject.UnityProjectRoot,
            context.UnityProject.ProjectFingerprint);
        if (!runnerInvocationResult.IsSuccess)
        {
            return BuildExecutionResult.Failed(runnerInvocationResult.Error!, project);
        }

        await EmitStartedAsync(
                resolvedProgressSink,
                runId,
                profile.Digest,
                cancellationToken)
            .ConfigureAwait(false);

        var runnerInvocation = runnerInvocationResult.Invocation!;
        var request = CreateBuildRunRequest(
            profile,
            profileReadResult.Path!,
            paths,
            outputLayout,
            runId,
            runnerInvocation);
        UnityRequestExecutionResult executionResult;
        try
        {
            executionResult = await ExecuteUnityRequestAsync(
                    context,
                    executionTarget,
                    requestTimeout,
                    request,
                    runId,
                    profile.Digest,
                    resolvedProgressSink,
                    useProgressStream: progressSink != null,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (BuildProgressProtocolException exception)
        {
            await EmitDiagnosticAsync(
                    resolvedProgressSink,
                    runId,
                    BuildErrorCodes.BuildRunnerInvocationFailed,
                    UcliDiagnosticSeverity.Error,
                    exception.Message,
                    BuildRunProgressPhase.RunnerInvocation,
                    cancellationToken)
                .ConfigureAwait(false);
            return BuildExecutionResult.Failed(
                ApplicationFailure.ContractViolation(
                    exception.Message,
                    BuildErrorCodes.BuildRunnerInvocationFailed,
                    instancePath: null,
                    startupFailure: null),
                project);
        }

        if (!executionResult.IsSuccess)
        {
            var failureInfo = executionResult.FailureInfo!;
            return BuildExecutionResult.Failed(
                RequestFailureNormalizer.FromUnityRequestFailure(failureInfo),
                project);
        }

        var responseResult = ResolveBuildResponse(
            executionResult.Response!,
            runId,
            context.UnityProject.ProjectFingerprint,
            profile,
            paths.RunnerOutputDirectory,
            outputLayout);
        if (!responseResult.IsSuccess)
        {
            var dirtyState = responseResult.Error!.Code == BuildErrorCodes.BuildDirtyStatePresent
                || responseResult.Error.Code == BuildErrorCodes.BuildDirtyStateIndeterminate
                ? responseResult.ErrorPayload?.DirtyState
                : null;
            return dirtyState == null
                ? BuildExecutionResult.Failed(responseResult.Error!, project)
                : BuildExecutionResult.FailedWithDirtyState(
                    responseResult.Error!,
                    project,
                    dirtyState);
        }

        var buildResponse = responseResult.Response!;
        await EmitProgressAsync(
                resolvedProgressSink,
                BuildRunProgressEventNames.RunnerResultCompleted,
                runId,
                profile.Digest,
                BuildRunProgressPhase.RunnerResult,
                profile.Runner.Kind,
                GetTerminalResult(buildResponse),
                verdict: null,
                reportRefs: [],
                errorCode: null,
                cancellationToken)
            .ConfigureAwait(false);

        if (!deadline.TryGetRemainingTimeout(out var artifactAccountingTimeout))
        {
            return BuildExecutionResult.Failed(CreateTimeoutFailure(timeout), project);
        }

        using var artifactAccountingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        artifactAccountingCancellationTokenSource.CancelAfter(artifactAccountingTimeout);
        var artifactCancellationToken = artifactAccountingCancellationTokenSource.Token;
        try
        {
            var terminalResult = GetTerminalResult(buildResponse);
            var buildReportSource = ResolveBuildReportSource(buildResponse);

            var resolvedOutputLayout = responseResult.OutputLayout ?? outputLayout;
            var outputSourcesResult = ResolveOutputSources(
                buildResponse,
                resolvedOutputLayout,
                profile.Runner.Kind);
            if (outputSourcesResult.Error != null)
            {
                return BuildExecutionResult.Failed(outputSourcesResult.Error, project);
            }

            var accountingResult = await artifactStore.AccountArtifactsAsync(
                    new BuildRunArtifactAccountingRequest(
                        paths,
                        buildResponse.Input.BuildTarget,
                        buildResponse.Input.UnityBuildTarget,
                        buildReportSource,
                        outputSourcesResult.OutputSources!,
                        CanWriteEmptyOutputManifest(terminalResult)),
                    artifactCancellationToken)
                .ConfigureAwait(false);
            if (!accountingResult.IsSuccess)
            {
                return BuildExecutionResult.Failed(accountingResult.Error!, project);
            }

            var accounting = accountingResult.Result!;
            var build = CreateBuildOutput(
                runId,
                profileReadResult.Path!,
                profile,
                buildResponse,
                accounting,
                runnerInvocation);
            var metadata = CreateMetadataDocument(
                build,
                buildResponse,
                profile,
                resolvedOutputLayout,
                accounting);
            var metadataWriteResult = await artifactStore.WriteMetadataAsync(
                    new BuildRunMetadataWriteRequest(
                        paths,
                        metadata,
                        accounting),
                    artifactCancellationToken)
                .ConfigureAwait(false);
            if (!metadataWriteResult.IsSuccess)
            {
                return BuildExecutionResult.Failed(metadataWriteResult.Error!, project);
            }

            if (IsForbiddenProjectMutationViolation(profile.Policy.ProjectMutationMode, buildResponse.ProjectMutation))
            {
                return BuildExecutionResult.Failed(
                    ApplicationFailure.ContractViolation(
                        "Build project mutation policy forbids project changes or incomplete mutation audit coverage during runner invocation.",
                        BuildErrorCodes.BuildProjectMutationForbidden,
                        instancePath: null,
                        startupFailure: null),
                    project);
            }

            var completedOutput = CreateOutput(
                project,
                profile,
                buildResponse,
                accounting,
                build,
                metadataWriteResult.Artifact!);
            await EmitProgressAsync(
                    resolvedProgressSink,
                    BuildRunProgressEventNames.ArtifactsCompleted,
                    runId,
                    profile.Digest,
                    BuildRunProgressPhase.ArtifactAccounting,
                    completedOutput.Build.Runner.Kind,
                    completedOutput.Build.RunnerResult.Status,
                    verdict: null,
                    reportRefs: CreateReportRefs(completedOutput.Reports),
                    errorCode: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await EmitCompletedAsync(
                    resolvedProgressSink,
                    completedOutput,
                    cancellationToken)
                .ConfigureAwait(false);
            return BuildExecutionResult.Completed(completedOutput);
        }
        catch (OperationCanceledException) when (artifactAccountingCancellationTokenSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return BuildExecutionResult.Failed(CreateTimeoutFailure(timeout), project);
        }
    }

    private static ValueTask EmitStartedAsync (
        ICommandProgressSink progressSink,
        Guid runId,
        Sha256Digest profileDigest,
        CancellationToken cancellationToken)
    {
        return EmitProgressAsync(
            progressSink,
            BuildRunProgressEventNames.Started,
            runId,
            profileDigest,
            BuildRunProgressPhase.Started,
            runnerKind: null,
            runnerStatus: null,
            verdict: null,
            reportRefs: [],
            errorCode: null,
            cancellationToken);
    }

    private RunnerInvocationResolutionResult ResolveRunnerInvocation (
        ResolvedBuildProfile profile,
        AbsolutePath profilePath,
        Guid runId,
        AbsolutePath outputDirectory,
        AbsolutePath projectPath,
        ProjectFingerprint projectFingerprint)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(profilePath);
        if (profile.Runner is ResolvedBuildRunner.BuildPipeline)
        {
            ArgumentNullException.ThrowIfNull(outputDirectory);
            ArgumentNullException.ThrowIfNull(projectPath);
            ArgumentNullException.ThrowIfNull(projectFingerprint);
            return RunnerInvocationResolutionResult.Success(ResolvedRunnerInvocationInput.Empty);
        }

        var executeMethodRunner = (ResolvedBuildRunner.ExecuteMethod)profile.Runner;
        var explicitInputs = (ResolvedBuildInputs.Explicit)profile.Inputs;

        ArgumentNullException.ThrowIfNull(projectFingerprint);

        var builtInVariables = CreateBuiltInVariableMap(
            profile,
            explicitInputs.BuildTarget,
            profilePath,
            runId,
            outputDirectory,
            projectPath,
            projectFingerprint);
        var arguments = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var argument in executeMethodRunner.Invocation.Arguments)
        {
            if (!TrySubstituteBuiltInVariables(
                argument.Value,
                builtInVariables,
                out var substituted,
                out var error))
            {
                return RunnerInvocationResolutionResult.Failure(error!);
            }

            arguments.Add(argument.Key, substituted!);
        }

        var requestedEnv = executeMethodRunner.Invocation.Environment;
        var environmentVariables = ResolveRunnerEnvironmentValues(requestedEnv.Variables);
        if (!environmentVariables.IsSuccess)
        {
            return RunnerInvocationResolutionResult.Failure(environmentVariables.Error!);
        }

        var environmentSecrets = ResolveRunnerEnvironmentValues(requestedEnv.Secrets);
        if (!environmentSecrets.IsSuccess)
        {
            return RunnerInvocationResolutionResult.Failure(environmentSecrets.Error!);
        }

        return RunnerInvocationResolutionResult.Success(new ResolvedRunnerInvocationInput(
            arguments,
            requestedEnv.Variables,
            requestedEnv.Secrets,
            environmentVariables.Values!,
            environmentSecrets.Values!));
    }

    private RunnerEnvironmentResolutionResult ResolveRunnerEnvironmentValues (IReadOnlyList<string> environmentNames)
    {
        var environmentValues = new SortedDictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < environmentNames.Count; i++)
        {
            var environmentName = environmentNames[i];
            var value = environmentVariableReader.Get(environmentName);
            if (value == null)
            {
                return RunnerEnvironmentResolutionResult.Failure(ExecutionError.InvalidArgument(
                    $"Build runner environment entry is missing: {environmentName}.",
                    BuildErrorCodes.BuildRunnerEnvironmentMissing));
            }

            environmentValues.Add(environmentName, value);
        }

        return RunnerEnvironmentResolutionResult.Success(environmentValues);
    }

    private static IReadOnlyDictionary<string, string> CreateBuiltInVariableMap (
        ResolvedBuildProfile profile,
        BuildTargetStableName buildTarget,
        AbsolutePath profilePath,
        Guid runId,
        AbsolutePath outputDirectory,
        AbsolutePath projectPath,
        ProjectFingerprint projectFingerprint)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ucli.build.runId"] = runId.ToString("D"),
            ["ucli.build.outputDir"] = outputDirectory.Value,
            ["ucli.build.profilePath"] = profilePath.Value,
            ["ucli.build.profileDigest"] = profile.Digest.ToString(),
            ["project.path"] = projectPath.Value,
            ["project.fingerprint"] = projectFingerprint.ToString(),
            ["build.target"] = TextVocabulary.GetText(buildTarget),
        };
    }

    private static bool TrySubstituteBuiltInVariables (
        string value,
        IReadOnlyDictionary<string, string> variables,
        out string? substituted,
        out ExecutionError? error)
    {
        substituted = null;
        error = null;

        var builder = new StringBuilder(value.Length);
        var index = 0;
        while (index < value.Length)
        {
            var variableStart = value.IndexOf("${", index, StringComparison.Ordinal);
            if (variableStart < 0)
            {
                builder.Append(value, index, value.Length - index);
                substituted = builder.ToString();
                return true;
            }

            builder.Append(value, index, variableStart - index);
            var variableEnd = value.IndexOf('}', variableStart + 2);
            if (variableEnd < 0)
            {
                error = ExecutionError.InvalidArgument(
                    "Build profile runner.invocation.arguments contains an unterminated built-in variable reference.",
                    BuildErrorCodes.BuildProfileInvalid);
                return false;
            }

            var variableName = value.Substring(variableStart + 2, variableEnd - variableStart - 2);
            if (!variables.TryGetValue(variableName, out var variableValue))
            {
                error = ExecutionError.InvalidArgument(
                    $"Build profile runner.invocation.arguments references unknown built-in variable: {variableName}.",
                    BuildErrorCodes.BuildProfileInvalid);
                return false;
            }

            if (RequiresNonEmptyVariableValue(variableName)
                && string.IsNullOrWhiteSpace(variableValue))
            {
                error = ExecutionError.InvalidArgument(
                    $"Build profile runner.invocation.arguments built-in variable resolves to an empty required path: {variableName}.",
                    BuildErrorCodes.BuildProfileInvalid);
                return false;
            }

            builder.Append(variableValue);
            index = variableEnd + 1;
        }

        substituted = builder.ToString();
        return true;
    }

    private static bool RequiresNonEmptyVariableValue (string variableName)
    {
        return string.Equals(variableName, "ucli.build.outputDir", StringComparison.Ordinal)
            || string.Equals(variableName, "ucli.build.profilePath", StringComparison.Ordinal)
            || string.Equals(variableName, "project.path", StringComparison.Ordinal);
    }

    private static ValueTask EmitCompletedAsync (
        ICommandProgressSink progressSink,
        BuildExecutionOutput output,
        CancellationToken cancellationToken)
    {
        return EmitProgressAsync(
            progressSink,
            BuildRunProgressEventNames.Completed,
            output.Build.RunId,
            output.Build.Profile.Digest,
            BuildRunProgressPhase.Completed,
            output.Build.Runner.Kind,
            output.Build.RunnerResult.Status,
            output.Verdict,
            CreateReportRefs(output.Reports),
            errorCode: null,
            cancellationToken);
    }

    private static ValueTask EmitDiagnosticAsync (
        ICommandProgressSink progressSink,
        Guid runId,
        UcliCode code,
        UcliDiagnosticSeverity severity,
        string message,
        BuildRunProgressPhase phase,
        CancellationToken cancellationToken)
    {
        return progressSink.OnEntryAsync(
            BuildRunProgressEventNames.Diagnostic,
            new BuildDiagnosticEntry(runId, code, severity, message, phase),
            cancellationToken);
    }

    private static ValueTask EmitProgressAsync (
        ICommandProgressSink progressSink,
        string eventName,
        Guid runId,
        Sha256Digest profileDigest,
        BuildRunProgressPhase phase,
        BuildRunnerKind? runnerKind,
        IpcBuildReportResult? runnerStatus,
        Verdict? verdict,
        IReadOnlyList<BuildArtifactKind> reportRefs,
        UcliCode? errorCode,
        CancellationToken cancellationToken)
    {
        return progressSink.OnEntryAsync(
            eventName,
            new BuildProgressEntry(
                RunId: runId,
                ProfileDigest: profileDigest,
                Phase: phase,
                RunnerKind: runnerKind,
                RunnerStatus: runnerStatus,
                Verdict: verdict,
                ReportRefs: reportRefs,
                ErrorCode: errorCode),
            cancellationToken);
    }

    private async ValueTask<UnityRequestExecutionResult> ExecuteUnityRequestAsync (
        ProjectContext context,
        UnityExecutionTarget executionTarget,
        TimeSpan requestTimeout,
        UnityRequestPayload.BuildRun request,
        Guid runId,
        Sha256Digest profileDigest,
        ICommandProgressSink progressSink,
        bool useProgressStream,
        CancellationToken cancellationToken)
    {
        if (!useProgressStream)
        {
            return await unityRequestExecutor.ExecuteAsync(
                    UcliCommandIds.BuildRun,
                    UnityExecutionTargetModeMapper.ToExplicitMode(executionTarget),
                    requestTimeout,
                    context.Config,
                    context.UnityProject,
                    request,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await unityStreamingRequestExecutor.ExecuteAsync(
                UcliCommandIds.BuildRun,
                UnityExecutionTargetModeMapper.ToExplicitMode(executionTarget),
                requestTimeout,
                context.Config,
                context.UnityProject,
                request,
                (frame, progressCancellationToken) => ForwardBuildProgressFrameAsync(
                    frame,
                    runId,
                    profileDigest,
                    progressSink,
                    progressCancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask ForwardBuildProgressFrameAsync (
        UnityRequestProgressFrame frame,
        Guid expectedRunId,
        Sha256Digest expectedProfileDigest,
        ICommandProgressSink progressSink,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(expectedProfileDigest);
        ArgumentNullException.ThrowIfNull(progressSink);

        switch (frame.Event)
        {
            case BuildRunProgressEventNames.ReadinessCompleted:
            case BuildRunProgressEventNames.RunnerResolved:
            case BuildRunProgressEventNames.RunnerStarted:
            case BuildRunProgressEventNames.RunnerCompleted:
                await ForwardProgressPayloadAsync<BuildProgressEntry>(frame, expectedRunId, expectedProfileDigest, progressSink, cancellationToken).ConfigureAwait(false);
                return;
            case BuildRunProgressEventNames.LogEntry:
                await ForwardProgressPayloadAsync<BuildLogEntry>(frame, expectedRunId, expectedProfileDigest, progressSink, cancellationToken).ConfigureAwait(false);
                return;
            case BuildRunProgressEventNames.Diagnostic:
                await ForwardProgressPayloadAsync<BuildDiagnosticEntry>(frame, expectedRunId, expectedProfileDigest, progressSink, cancellationToken).ConfigureAwait(false);
                return;
            default:
                throw new BuildProgressProtocolException($"Unity build progress event is not supported: {frame.Event}.");
        }
    }

    private static async ValueTask ForwardProgressPayloadAsync<TPayload> (
        UnityRequestProgressFrame frame,
        Guid expectedRunId,
        Sha256Digest expectedProfileDigest,
        ICommandProgressSink progressSink,
        CancellationToken cancellationToken)
        where TPayload : notnull
    {
        if (!IpcPayloadCodec.TryDeserialize<TPayload>(frame.Payload, out var payload, out var error))
        {
            throw new BuildProgressProtocolException(
                $"Unity build progress payload is invalid for event '{frame.Event}'. {error}");
        }

        BuildProgressPayloadValidator.Validate(frame.Event, payload!, expectedRunId, expectedProfileDigest);
        await progressSink.OnEntryAsync(
                frame.Event,
                payload!,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static UnityRequestPayload.BuildRun CreateBuildRunRequest (
        ResolvedBuildProfile profile,
        AbsolutePath profilePath,
        BuildRunArtifactPaths paths,
        BuildPipelineOutputLayout? outputLayout,
        Guid runId,
        ResolvedRunnerInvocationInput runnerInvocation)
    {
        BuildTargetStableName? buildTarget;
        BuildProfileSceneSource? sceneSource;
        IReadOnlyList<SceneAssetPath> scenePaths;
        bool development;
        IpcUnityBuildProfileInput? unityBuildProfile;
        if (profile.Inputs is ResolvedBuildInputs.Explicit explicitInputs)
        {
            buildTarget = explicitInputs.BuildTarget;
            sceneSource = explicitInputs.Scenes.Source;
            scenePaths = explicitInputs.Scenes is ResolvedBuildScenes.Explicit explicitScenes
                ? explicitScenes.Paths
                : Array.Empty<SceneAssetPath>();
            development = explicitInputs.Options.Development;
            unityBuildProfile = null;
        }
        else
        {
            var unityBuildProfileInputs = (ResolvedBuildInputs.UnityBuildProfile)profile.Inputs;
            buildTarget = null;
            sceneSource = null;
            scenePaths = Array.Empty<SceneAssetPath>();
            development = false;
            unityBuildProfile = new IpcUnityBuildProfileInput(
                Path: unityBuildProfileInputs.Path,
                Digest: null,
                ApplyAudit: null);
        }

        var executeMethodRunner = profile.Runner as ResolvedBuildRunner.ExecuteMethod;
        var request = new IpcBuildRunRequest(
            RunId: runId,
            InputKind: profile.Inputs.Kind,
            BuildTarget: buildTarget,
            SceneSource: sceneSource,
            ScenePaths: scenePaths,
            Development: development,
            OutputPath: paths.RunnerOutputDirectory.Value,
            OutputLayout: outputLayout?.ToContract(),
            BuildReportPath: paths.BuildReportJsonPath.Value,
            BuildLogPath: paths.BuildLogPath.Value,
            AllowedEditorModes: profile.Policy.Runtime.AllowedEditorModes,
            ProjectMutationMode: profile.Policy.ProjectMutationMode,
            RunnerKind: profile.Runner.Kind,
            ProfileDigest: profile.Digest,
            UnityBuildProfile: unityBuildProfile,
            ProfilePath: executeMethodRunner != null ? profilePath.Value : null,
            RunnerMethod: executeMethodRunner?.Method,
            RunnerArguments: runnerInvocation.Arguments,
            RunnerEnvironmentVariables: runnerInvocation.EnvironmentVariables,
            RunnerEnvironmentSecrets: runnerInvocation.EnvironmentSecrets,
            RunnerEnvironmentVariableValues: runnerInvocation.EnvironmentVariableValues,
            RunnerEnvironmentSecretValues: runnerInvocation.EnvironmentSecretValues);
        return new UnityRequestPayload.BuildRun(request);
    }

    private static IpcBuildReportResult GetTerminalResult (IpcBuildRunResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return response.RunnerResult?.Status ?? response.Report!.Result;
    }

    private static OutputSourcesResolutionResult ResolveOutputSources (
        IpcBuildRunResponse response,
        BuildPipelineOutputLayout? outputLayout,
        BuildRunnerKind runnerKind)
    {
        if (runnerKind == BuildRunnerKind.BuildPipeline)
        {
            if (outputLayout == null)
            {
                return OutputSourcesResolutionResult.Failure(ApplicationFailure.InternalError(
                    "Validated BuildPipeline output is missing the layout required for output accounting.", UcliCoreErrorCodes.InternalError,
                    instancePath: null,
                    startupFailure: null));
            }

            return OutputSourcesResolutionResult.Success([
                BuildOutputSourceEntry.FromAbsolutePath(outputLayout.LocationPath),
            ]);
        }

        var runnerResult = response.RunnerResult;
        if (runnerResult == null)
        {
            return OutputSourcesResolutionResult.Failure(ApplicationFailure.InternalError(
                "Validated executeMethod output is missing the runner result required for output accounting.", UcliCoreErrorCodes.InternalError,
                instancePath: null,
                startupFailure: null));
        }

        if (runnerResult.Outputs.Count == 0)
        {
            return OutputSourcesResolutionResult.Success(Array.Empty<BuildOutputSourceEntry>());
        }

        var outputSources = new BuildOutputSourceEntry[runnerResult.Outputs.Count];
        for (var i = 0; i < runnerResult.Outputs.Count; i++)
        {
            outputSources[i] = BuildOutputSourceEntry.FromRunnerOutputRelativePath(runnerResult.Outputs[i]);
        }

        return OutputSourcesResolutionResult.Success(outputSources);
    }

    private static BuildReportSourceEntry? ResolveBuildReportSource (IpcBuildRunResponse response)
    {
        if (response.RunnerResult?.BuildReport == null)
        {
            return response.Report == null
                ? null
                : BuildReportSourceEntry.FromArtifact(response.Report);
        }

        return BuildReportSourceEntry.FromRunnerOutputRelativePath(response.RunnerResult.BuildReport.Path);
    }

    private static BuildResponseResolutionResult ResolveBuildResponse (
        UnityRequestResponse response,
        Guid expectedRunId,
        ProjectFingerprint expectedProjectFingerprint,
        ResolvedBuildProfile expectedProfile,
        AbsolutePath expectedOutputDirectory,
        BuildPipelineOutputLayout? expectedOutputLayout)
    {
        if (response.Errors.Count != 0)
        {
            var firstError = response.Errors[0];
            var failure = RequestFailureNormalizer.FromOperationError(firstError);
            return BuildResponseResolutionResult.Failure(failure, TryReadErrorPayload(response));
        }

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcBuildRunResponse buildResponse, out var payloadError))
        {
            var failure = expectedProfile.Runner is ResolvedBuildRunner.ExecuteMethod
                && JsonObjectPropertyReader.TryGetPropertyIgnoreCase(response.Payload, "runnerResult", out _)
                    ? ApplicationFailure.ContractViolation(
                        $"Unity build response runnerResult is invalid. {payloadError.Message}",
                        BuildErrorCodes.BuildRunnerResultInvalid,
                        instancePath: null,
                        startupFailure: null)
                    : ApplicationFailure.InternalError($"Unity build payload is invalid. {payloadError.Message}", UcliCoreErrorCodes.InternalError, instancePath: null, startupFailure: null);
            return BuildResponseResolutionResult.Failure(failure, errorPayload: null);
        }

        BuildPipelineOutputLayout? outputLayout = null;
        if (buildResponse.OutputLayout is not null
            && !BuildPipelineOutputLayout.TryFromContract(
                buildResponse.OutputLayout,
                out outputLayout,
                out var outputLayoutPathFailure))
        {
            return BuildResponseResolutionResult.Failure(
                ApplicationFailure.InternalError(
                    $"Unity build response outputLayout locationPathName is invalid. {outputLayoutPathFailure.Message}", UcliCoreErrorCodes.InternalError,
                    instancePath: null,
                    startupFailure: null),
                errorPayload: null);
        }

        var validationFailure = ValidateResponse(
            buildResponse,
            outputLayout,
            expectedRunId,
            expectedProjectFingerprint,
            expectedProfile,
            expectedOutputDirectory,
            expectedOutputLayout);
        return validationFailure != null
            ? BuildResponseResolutionResult.Failure(validationFailure, errorPayload: null)
            : BuildResponseResolutionResult.Success(buildResponse, outputLayout);
    }

    private static IpcBuildRunErrorPayload? TryReadErrorPayload (UnityRequestResponse response)
    {
        return IpcPayloadCodec.TryDeserialize(response.Payload, out IpcBuildRunErrorPayload payload, out _)
            ? payload
            : null;
    }

    private static ApplicationFailure? ValidateResponse (
        IpcBuildRunResponse response,
        BuildPipelineOutputLayout? outputLayout,
        Guid expectedRunId,
        ProjectFingerprint expectedProjectFingerprint,
        ResolvedBuildProfile expectedProfile,
        AbsolutePath expectedOutputDirectory,
        BuildPipelineOutputLayout? expectedOutputLayout)
    {
        if (response.RunId != expectedRunId)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response runId mismatch. Requested={expectedRunId}, Actual={response.RunId}.", UcliCoreErrorCodes.InternalError,
                instancePath: null,
                startupFailure: null);
        }

        if (response.ProjectFingerprint != expectedProjectFingerprint)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response projectFingerprint mismatch. Requested={expectedProjectFingerprint}, Actual={response.ProjectFingerprint}.", UcliCoreErrorCodes.InternalError,
                instancePath: null,
                startupFailure: null);
        }

        var inputKind = response.Input.InputKind;
        if (inputKind != expectedProfile.Inputs.Kind)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response input kind mismatch. Requested={TextVocabulary.GetText(expectedProfile.Inputs.Kind)}, Actual={response.Input.InputKind}.", UcliCoreErrorCodes.InternalError,
                instancePath: null,
                startupFailure: null);
        }

        var buildTargetValidationFailure = ValidateResponseInputBuildTarget(response.Input);
        if (buildTargetValidationFailure != null)
        {
            return buildTargetValidationFailure;
        }

        var expectedExplicitInputs = expectedProfile.Inputs as ResolvedBuildInputs.Explicit;
        if (expectedExplicitInputs != null)
        {
            var explicitValidationFailure = ValidateExplicitResponseInputs(response, expectedExplicitInputs);
            if (explicitValidationFailure != null)
            {
                return explicitValidationFailure;
            }
        }
        else
        {
            var expectedUnityBuildProfileInputs = (ResolvedBuildInputs.UnityBuildProfile)expectedProfile.Inputs;
            var unityBuildProfileValidationFailure = ValidateUnityBuildProfileResponseInputs(
                response,
                expectedUnityBuildProfileInputs);
            if (unityBuildProfileValidationFailure != null)
            {
                return unityBuildProfileValidationFailure;
            }
        }

        var outputLayoutValidationFailure = ValidateResponseOutputLayout(
            outputLayout,
            response.Input.BuildTarget,
            expectedOutputDirectory,
            expectedOutputLayout,
            expectedProfile.Inputs.Kind,
            expectedProfile.Runner.Kind);
        if (outputLayoutValidationFailure != null)
        {
            return outputLayoutValidationFailure;
        }

        var sceneSource = response.Input.SceneSource;
        if (expectedExplicitInputs != null)
        {
            if (sceneSource != expectedExplicitInputs.Scenes.Source)
            {
                var expectedSceneSource = TextVocabulary.GetText(expectedExplicitInputs.Scenes.Source);
                return ApplicationFailure.InternalError(
                    $"Unity build response scene source mismatch. Requested={expectedSceneSource}, Actual={response.Input.SceneSource}.", UcliCoreErrorCodes.InternalError,
                    instancePath: null,
                    startupFailure: null);
            }

            if (!HasExpectedDevelopmentBuildOption(
                response.Input.BuildOptions,
                expectedExplicitInputs.Options.Development))
            {
                return ApplicationFailure.InternalError(
                    $"Unity build response build options mismatch. RequestedDevelopment={expectedExplicitInputs.Options.Development}, Actual={response.Input.BuildOptions}.", UcliCoreErrorCodes.InternalError,
                    instancePath: null,
                    startupFailure: null);
            }
        }
        else if (sceneSource != BuildProfileSceneSource.UnityBuildProfile)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response scene source mismatch. Requested={TextVocabulary.GetText(BuildProfileSceneSource.UnityBuildProfile)}, Actual={response.Input.SceneSource}.", UcliCoreErrorCodes.InternalError,
                instancePath: null,
                startupFailure: null);
        }

        IpcBuildReportResult? reportResult = null;
        if (expectedProfile.Runner.Kind == BuildRunnerKind.BuildPipeline)
        {
            if (response.Report == null)
            {
                return ApplicationFailure.ContractViolation(
                    "Unity build response BuildReport is missing for buildPipeline runner.",
                    BuildErrorCodes.BuildReportMissing,
                    instancePath: null,
                    startupFailure: null);
            }

            if (!IsTerminalBuildReportResult(response.Report.Result))
            {
                return ApplicationFailure.InternalError($"Unity build response contains non-terminal report result: {response.Report.Result}.", UcliCoreErrorCodes.InternalError, instancePath: null, startupFailure: null);
            }

            if (!string.Equals(response.Report.UnityBuildTarget, response.Input.UnityBuildTarget, StringComparison.Ordinal))
            {
                return ApplicationFailure.InternalError(
                    $"Unity BuildReport BuildTarget mismatch. Input={response.Input.UnityBuildTarget}, Report={response.Report.UnityBuildTarget}.", UcliCoreErrorCodes.InternalError,
                    instancePath: null,
                    startupFailure: null);
            }

            reportResult = response.Report.Result;
        }
        else if (response.Report != null)
        {
            return ApplicationFailure.InternalError("Unity build response must not include a BuildReport payload for executeMethod runner.", UcliCoreErrorCodes.InternalError, instancePath: null, startupFailure: null);
        }

        var runnerResultValidationFailure = ValidateRunnerResult(
            response.RunnerResult,
            expectedProfile.Runner.Kind,
            response.Report,
            reportResult);
        if (runnerResultValidationFailure != null)
        {
            return runnerResultValidationFailure;
        }

        var terminalResult = GetTerminalResult(response);
        var expectedCompletionReason = IpcBuildLogCompletionReasonResolver.FromReportResult(terminalResult);
        if (response.Logs.CompletionReason != expectedCompletionReason)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response log completionReason mismatch. Expected={TextVocabulary.GetText(expectedCompletionReason)}, Actual={response.Logs.CompletionReason}.", UcliCoreErrorCodes.InternalError,
                instancePath: null,
                startupFailure: null);
        }

        if (response.Input.Scenes.Count == 0)
        {
            return ApplicationFailure.InternalError("Unity build response contains no resolved build scenes.", UcliCoreErrorCodes.InternalError, instancePath: null, startupFailure: null);
        }

        if (expectedExplicitInputs?.Scenes is ResolvedBuildScenes.Explicit expectedExplicitScenes
            && !response.Input.Scenes.SequenceEqual(expectedExplicitScenes.Paths))
        {
            return ApplicationFailure.InternalError("Unity build response resolved scenes do not match the requested explicit build scenes.", UcliCoreErrorCodes.InternalError, instancePath: null, startupFailure: null);
        }

        return ValidateProjectMutationAudit(response.ProjectMutation, expectedProfile.Policy.ProjectMutationMode);
    }

    private static ApplicationFailure? ValidateResponseOutputLayout (
        BuildPipelineOutputLayout? outputLayout,
        BuildTargetStableName buildTarget,
        AbsolutePath expectedOutputDirectory,
        BuildPipelineOutputLayout? requestedOutputLayout,
        BuildProfileInputsKind inputKind,
        BuildRunnerKind runnerKind)
    {
        if (runnerKind == BuildRunnerKind.ExecuteMethod)
        {
            return outputLayout == null
                ? null
                : ApplicationFailure.InternalError("Unity build response outputLayout must be omitted for executeMethod runner.", UcliCoreErrorCodes.InternalError, instancePath: null, startupFailure: null);
        }

        if (outputLayout == null)
        {
            return ApplicationFailure.InternalError("Unity build response outputLayout is missing.", UcliCoreErrorCodes.InternalError, instancePath: null, startupFailure: null);
        }

        if (inputKind == BuildProfileInputsKind.Explicit)
        {
            return requestedOutputLayout is not null
                && IsExpectedOutputLayout(outputLayout, requestedOutputLayout)
                    ? null
                    : ApplicationFailure.InternalError("Unity build response outputLayout does not match the requested output layout.", UcliCoreErrorCodes.InternalError, instancePath: null, startupFailure: null);
        }

        if (!BuildPipelineOutputLayoutResolver.TryResolve(
            expectedOutputDirectory,
            buildTarget,
            androidAppBundle: false,
            out var expectedOutputLayout))
        {
            return ApplicationFailure.InternalError($"Unity build response buildTarget does not have a supported output layout: {buildTarget}.", UcliCoreErrorCodes.InternalError, instancePath: null, startupFailure: null);
        }

        if (IsExpectedOutputLayout(outputLayout, expectedOutputLayout!))
        {
            return null;
        }

        if (buildTarget == BuildTargetStableName.Android
            && BuildPipelineOutputLayoutResolver.TryResolve(
                expectedOutputDirectory,
                buildTarget,
                androidAppBundle: true,
                out var androidAppBundleLayout)
            && IsExpectedOutputLayout(outputLayout, androidAppBundleLayout!))
        {
            return null;
        }

        return ApplicationFailure.InternalError("Unity build response outputLayout does not match the resolved build target.", UcliCoreErrorCodes.InternalError, instancePath: null, startupFailure: null);
    }

    private static bool IsExpectedOutputLayout (
        BuildPipelineOutputLayout actual,
        BuildPipelineOutputLayout expected)
    {
        return actual.Shape == expected.Shape
            && actual.LocationPath.IsSameAs(expected.LocationPath);
    }

    private static ApplicationFailure? ValidateExplicitResponseInputs (
        IpcBuildRunResponse response,
        ResolvedBuildInputs.Explicit expectedInputs)
    {
        if (response.UnityBuildProfile != null)
        {
            return ApplicationFailure.InternalError("Unity build response unityBuildProfile input must be omitted for explicit build inputs.", UcliCoreErrorCodes.InternalError, instancePath: null, startupFailure: null);
        }

        if (response.Input.BuildTarget != expectedInputs.BuildTarget)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response buildTarget mismatch. Requested={TextVocabulary.GetText(expectedInputs.BuildTarget)}, Actual={response.Input.BuildTarget}.", UcliCoreErrorCodes.InternalError,
                instancePath: null,
                startupFailure: null);
        }

        return null;
    }

    private static ApplicationFailure? ValidateResponseInputBuildTarget (IpcBuildInputProbe input)
    {
        if (!BuildTargetStableNameUnityBuildTargetResolver.TryResolve(input.BuildTarget, out var expectedUnityBuildTarget))
        {
            return ApplicationFailure.InternalError(
                $"Unity build response contains an unsupported buildTarget: {input.BuildTarget}.", UcliCoreErrorCodes.InternalError,
                instancePath: null,
                startupFailure: null);
        }

        if (!string.Equals(expectedUnityBuildTarget, input.UnityBuildTarget, StringComparison.Ordinal))
        {
            return ApplicationFailure.InternalError(
                $"Unity build response target mismatch. BuildTarget={TextVocabulary.GetText(input.BuildTarget)}, ExpectedUnityBuildTarget={expectedUnityBuildTarget}, ActualUnityBuildTarget={input.UnityBuildTarget}.", UcliCoreErrorCodes.InternalError,
                instancePath: null,
                startupFailure: null);
        }

        return null;
    }

    private static ApplicationFailure? ValidateUnityBuildProfileResponseInputs (
        IpcBuildRunResponse response,
        ResolvedBuildInputs.UnityBuildProfile expectedInputs)
    {
        if (response.UnityBuildProfile == null)
        {
            return ApplicationFailure.InternalError("Unity build response unityBuildProfile input is missing.", UcliCoreErrorCodes.InternalError, instancePath: null, startupFailure: null);
        }

        if (response.UnityBuildProfile.Path != expectedInputs.Path)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response unityBuildProfile path mismatch. Requested={expectedInputs.Path}, Actual={response.UnityBuildProfile.Path}.", UcliCoreErrorCodes.InternalError,
                instancePath: null,
                startupFailure: null);
        }

        if (response.UnityBuildProfile.Digest == null)
        {
            return ApplicationFailure.InternalError("Unity build response unityBuildProfile digest is missing.", UcliCoreErrorCodes.InternalError, instancePath: null, startupFailure: null);
        }

        if (response.UnityBuildProfile.ApplyAudit == null)
        {
            return ApplicationFailure.InternalError("Unity build response unityBuildProfile applyAudit is missing.", UcliCoreErrorCodes.InternalError, instancePath: null, startupFailure: null);
        }

        if (!response.UnityBuildProfile.ApplyAudit.Applied)
        {
            return ApplicationFailure.InternalError("Unity build response unityBuildProfile applyAudit.applied must be true.", UcliCoreErrorCodes.InternalError, instancePath: null, startupFailure: null);
        }

        return null;
    }

    private static ApplicationFailure? ValidateRunnerResult (
        IpcBuildRunnerResultArtifact? runnerResult,
        BuildRunnerKind expectedRunnerKind,
        IpcBuildReportArtifact? report,
        IpcBuildReportResult? reportResult)
    {
        if (runnerResult == null)
        {
            return expectedRunnerKind == BuildRunnerKind.ExecuteMethod
                ? ApplicationFailure.ContractViolation(
                    "Unity build response runnerResult is missing for executeMethod runner.",
                    BuildErrorCodes.BuildRunnerResultMissing,
                    instancePath: null,
                    startupFailure: null)
                : null;
        }

        var expectedSource = expectedRunnerKind == BuildRunnerKind.ExecuteMethod
            ? IpcBuildRunnerResultSource.UcliBuildRunnerResult
            : IpcBuildRunnerResultSource.BuildPipelineBuildReport;
        if (runnerResult.Source != expectedSource)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response runnerResult source is invalid for {TextVocabulary.GetText(expectedRunnerKind)} runner: {runnerResult.Source}.", UcliCoreErrorCodes.InternalError,
                instancePath: null,
                startupFailure: null);
        }

        if (expectedRunnerKind == BuildRunnerKind.BuildPipeline)
        {
            if (report == null || reportResult == null)
            {
                return ApplicationFailure.ContractViolation(
                    "Unity build response BuildReport is missing for buildPipeline runner.",
                    BuildErrorCodes.BuildReportMissing,
                    instancePath: null,
                    startupFailure: null);
            }

            if (runnerResult.Status != reportResult.Value)
            {
                return ApplicationFailure.InternalError(
                    $"Unity build response runnerResult status mismatch. Report={TextVocabulary.GetText(reportResult.Value)}, RunnerResult={runnerResult.Status}.", UcliCoreErrorCodes.InternalError,
                    instancePath: null,
                    startupFailure: null);
            }

            if (runnerResult.DurationMilliseconds != report.DurationMilliseconds
                || runnerResult.ErrorCount != report.ErrorCount
                || runnerResult.WarningCount != report.WarningCount)
            {
                return ApplicationFailure.InternalError("Unity build response runnerResult summary does not match report summary.", UcliCoreErrorCodes.InternalError, instancePath: null, startupFailure: null);
            }
        }

        return null;
    }

    private static ApplicationFailure? ValidateProjectMutationAudit (
        IpcBuildProjectMutationAudit projectMutation,
        BuildProfileProjectMutationMode expectedMode)
    {
        if (projectMutation.Mode != expectedMode)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response projectMutation mode mismatch. Requested={TextVocabulary.GetText(expectedMode)}, Actual={TextVocabulary.GetText(projectMutation.Mode)}.", UcliCoreErrorCodes.InternalError,
                instancePath: null,
                startupFailure: null);
        }

        return null;
    }

    private static bool CanWriteEmptyOutputManifest (IpcBuildReportResult reportResult)
    {
        return reportResult is IpcBuildReportResult.Failed or IpcBuildReportResult.Canceled;
    }

    private static bool IsTerminalBuildReportResult (IpcBuildReportResult reportResult)
    {
        return reportResult is IpcBuildReportResult.Succeeded or IpcBuildReportResult.Failed or IpcBuildReportResult.Canceled;
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

    private static BuildOutput CreateBuildOutput (
        Guid runId,
        AbsolutePath profilePath,
        ResolvedBuildProfile profile,
        IpcBuildRunResponse response,
        BuildRunArtifactAccountingResult accounting,
        ResolvedRunnerInvocationInput runnerInvocation)
    {
        var generations = CreateGenerations(response.LifecycleBefore, response.LifecycleAfter);
        var executeMethodRunner = profile.Runner as ResolvedBuildRunner.ExecuteMethod;
        BuildArtifactKind? reportRef = accounting.BuildReport == null ? null : BuildArtifactKind.BuildReport;
        var summary = profile.Runner is ResolvedBuildRunner.BuildPipeline
            ? new BuildSummaryOutput(
                Result: response.Report!.Result,
                DurationMilliseconds: response.Report.DurationMilliseconds,
                ErrorCount: response.Report.ErrorCount,
                WarningCount: response.Report.WarningCount,
                ReportRef: reportRef)
            : new BuildSummaryOutput(
                Result: response.RunnerResult!.Status,
                DurationMilliseconds: response.RunnerResult.DurationMilliseconds,
                ErrorCount: response.RunnerResult.ErrorCount,
                WarningCount: response.RunnerResult.WarningCount,
                ReportRef: reportRef);
        var logs = new BuildLogsOutput(
            EntryCount: response.Logs.EntryCount,
            ErrorCount: response.Logs.ErrorCount,
            WarningCount: response.Logs.WarningCount,
            CompletionReason: response.Logs.CompletionReason,
            Window: new BuildLogWindowOutput(
                StartedAtUtc: response.Logs.Window.StartedAtUtc,
                CompletedAtUtc: response.Logs.Window.CompletedAtUtc,
                CursorStart: response.Logs.Window.CursorStart?.Value,
                CursorEnd: response.Logs.Window.CursorEnd?.Value));
        var scenes = new BuildScenesOutput(
            Source: response.Input.SceneSource,
            Paths: response.Input.Scenes);
        var options = new BuildOptionsOutput(ContainsBuildOption(response.Input.BuildOptions, "Development"));
        var unityBuildProfile = CreateUnityBuildProfileOutput(response.UnityBuildProfile);
        var inputs = new BuildInputsOutput(
            InputKind: response.Input.InputKind,
            Target: new BuildTargetOutput(
                StableName: response.Input.BuildTarget,
                UnityBuildTarget: response.Input.UnityBuildTarget),
            Scenes: scenes,
            Options: options,
            UnityBuildProfile: unityBuildProfile);
        return new BuildOutput(
            runId: runId,
            profile: new BuildProfileOutput(profilePath.Value, profile.Digest),
            inputs: inputs,
            runner: new BuildRunnerOutput(
                Kind: profile.Runner.Kind,
                Method: executeMethodRunner?.Method,
                Invocation: new BuildRunnerInvocationOutput(
                    Arguments: runnerInvocation.Arguments,
                    Environment: new BuildRunnerInvocationEnvironmentOutput(
                        Variables: runnerInvocation.EnvironmentVariables,
                        Secrets: runnerInvocation.EnvironmentSecrets))),
            runnerResult: CreateRunnerResultOutput(profile, response),
            output: new BuildArtifactOutput(
                ManifestDigest: accounting.OutputManifest.ManifestDigest,
                EntryCount: accounting.OutputManifest.EntryCount,
                FileCount: accounting.OutputManifest.FileCount,
                TotalBytes: accounting.OutputManifest.TotalBytes),
            generations: generations,
            summary: summary,
            logs: logs);
    }

    private static BuildExecutionOutput CreateOutput (
        ProjectIdentityInfo project,
        ResolvedBuildProfile profile,
        IpcBuildRunResponse response,
        BuildRunArtifactAccountingResult accounting,
        BuildOutput build,
        BuildArtifactRef buildArtifact)
    {
        var residualRisks = CreateResidualRisks(profile.Policy.ProjectMutationMode, response.ProjectMutation);
        var claims = CreateClaims(response, build);
        return new BuildExecutionOutput(
            Project: project,
            Build: build,
            Verifiers:
            [
                new BuildVerifierOutput(
                    Id: VerifierId,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: claims.Where(static claim => claim.Required).Select(static claim => claim.Id).ToArray(),
                    Effects: AssuranceEffectSets.CreateBuild(build.Runner.Kind, accounting.BuildReport != null)),
            ],
            Claims: claims,
            Reports: CreateReports(accounting, buildArtifact),
            ResidualRisks: residualRisks);
    }

    private static BuildUnityBuildProfileOutput? CreateUnityBuildProfileOutput (IpcUnityBuildProfileInput? unityBuildProfile)
    {
        if (unityBuildProfile == null || unityBuildProfile.Digest == null)
        {
            return null;
        }

        return new BuildUnityBuildProfileOutput(
            Path: unityBuildProfile.Path.Value,
            Digest: unityBuildProfile.Digest);
    }

    private static BuildRunUnityBuildProfileInputMetadata? CreateUnityBuildProfileInputMetadata (IpcUnityBuildProfileInput? unityBuildProfile)
    {
        if (unityBuildProfile == null
            || unityBuildProfile.Digest == null
            || unityBuildProfile.ApplyAudit == null)
        {
            return null;
        }

        return new BuildRunUnityBuildProfileInputMetadata(
            Path: unityBuildProfile.Path.Value,
            Digest: unityBuildProfile.Digest,
            ApplyAudit: unityBuildProfile.ApplyAudit);
    }

    private static BuildRunnerResultOutput CreateRunnerResultOutput (
        ResolvedBuildProfile profile,
        IpcBuildRunResponse response)
    {
        var runnerResult = response.RunnerResult;
        if (runnerResult != null)
        {
            return new BuildRunnerResultOutput(
                Source: runnerResult.Source,
                Status: runnerResult.Status);
        }

        var source = profile.Runner.Kind == BuildRunnerKind.ExecuteMethod
            ? IpcBuildRunnerResultSource.UcliBuildRunnerResult
            : IpcBuildRunnerResultSource.BuildPipelineBuildReport;
        return new BuildRunnerResultOutput(
            Source: source,
            Status: GetTerminalResult(response));
    }

    private static BuildReportsOutput CreateReports (
        BuildRunArtifactAccountingResult accounting,
        BuildArtifactRef buildArtifact)
    {
        ArgumentNullException.ThrowIfNull(buildArtifact);
        return new BuildReportsOutput(
            Build: AssuranceReportReference.FromPath(
                buildArtifact.Path,
                buildArtifact.Digest),
            BuildReport: accounting.BuildReport == null
                ? null
                : AssuranceReportReference.FromPath(
                    accounting.BuildReport.Path,
                    accounting.BuildReport.Digest),
            BuildOutputManifest: AssuranceReportReference.FromPath(
                accounting.BuildOutputManifest.Path,
                accounting.BuildOutputManifest.Digest),
            BuildLog: AssuranceReportReference.FromPath(
                accounting.BuildLog.Path,
                accounting.BuildLog.Digest));
    }

    private static IReadOnlyList<BuildArtifactKind> CreateReportRefs (BuildReportsOutput reports)
    {
        ArgumentNullException.ThrowIfNull(reports);
        if (reports.BuildReport != null)
        {
            return
            [
                BuildArtifactKind.Build,
                BuildArtifactKind.BuildReport,
                BuildArtifactKind.BuildOutputManifest,
                BuildArtifactKind.BuildLog,
            ];
        }

        return
        [
            BuildArtifactKind.Build,
            BuildArtifactKind.BuildOutputManifest,
            BuildArtifactKind.BuildLog,
        ];
    }

    private static BuildRunMetadataDocument CreateMetadataDocument (
        BuildOutput build,
        IpcBuildRunResponse response,
        ResolvedBuildProfile profile,
        BuildPipelineOutputLayout? outputLayout,
        BuildRunArtifactAccountingResult accounting)
    {
        var invocationEnv = build.Runner.Invocation.Environment;
        var executeMethodRunner = profile.Runner as ResolvedBuildRunner.ExecuteMethod;
        return new BuildRunMetadataDocument(
            schemaVersion: BuildMetadataSchemaVersion,
            runId: build.RunId,
            profile: SerializeMetadataElement(build.Profile),
            inputs: SerializeMetadataElement(CreateInputMetadata(build.Inputs, response.UnityBuildProfile)),
            runner: SerializeMetadataElement(new BuildRunRunnerMetadata(
                Kind: profile.Runner.Kind,
                Method: executeMethodRunner?.Method,
                Invocation: new BuildRunRunnerInvocationMetadata(
                    Arguments: build.Runner.Invocation.Arguments,
                    Environment: new BuildRunRunnerInvocationEnvironmentMetadata(
                        Variables: invocationEnv.Variables,
                        Secrets: invocationEnv.Secrets)),
                OutputLayout: outputLayout?.ToContract())),
            runnerResult: SerializeMetadataElement(CreateRunnerResultMetadata(build, response, accounting.BuildReport != null)),
            lifecycle: SerializeMetadataElement(new BuildRunLifecycleMetadata(
                Before: response.LifecycleBefore,
                After: response.LifecycleAfter)),
            generations: SerializeMetadataElement(build.Generations),
            summary: SerializeMetadataElement(build.Summary),
            logs: SerializeMetadataElement(build.Logs),
            projectMutation: SerializeMetadataElement(response.ProjectMutation));
    }

    private static BuildRunInputMetadata CreateInputMetadata (
        BuildInputsOutput inputs,
        IpcUnityBuildProfileInput? unityBuildProfile)
    {
        return new BuildRunInputMetadata(
            InputKind: inputs.InputKind,
            Target: new BuildRunTargetMetadata(
                StableName: inputs.Target.StableName,
                UnityBuildTarget: inputs.Target.UnityBuildTarget),
            Scenes: new BuildRunScenesMetadata(
                Source: inputs.Scenes.Source,
                Paths: inputs.Scenes.Paths),
            Options: new BuildRunOptionsMetadata(inputs.Options.Development),
            UnityBuildProfile: CreateUnityBuildProfileInputMetadata(unityBuildProfile));
    }

    private static object CreateRunnerResultMetadata (
        BuildOutput build,
        IpcBuildRunResponse response,
        bool hasBuildReport)
    {
        var runnerResult = response.RunnerResult;
        if (runnerResult != null)
        {
            return new
            {
                runnerResult.Source,
                runnerResult.Status,
                summary = new
                {
                    runnerResult.DurationMilliseconds,
                    runnerResult.ErrorCount,
                    runnerResult.WarningCount,
                },
                runnerResult.Diagnostics,
                buildReportRef = hasBuildReport ? (BuildArtifactKind?)BuildArtifactKind.BuildReport : null,
            };
        }

        return new
        {
            build.RunnerResult.Source,
            build.RunnerResult.Status,
            summary = new
            {
                build.Summary.DurationMilliseconds,
                build.Summary.ErrorCount,
                build.Summary.WarningCount,
            },
            diagnostics = Array.Empty<IpcBuildRunnerDiagnostic>(),
            buildReportRef = hasBuildReport ? (BuildArtifactKind?)BuildArtifactKind.BuildReport : null,
        };
    }

    private static JsonElement SerializeMetadataElement<T> (T value)
    {
        return JsonSerializer.SerializeToElement(value, IpcJsonSerializerOptions.Default);
    }

    private static BuildGenerationsOutput CreateGenerations (
        IpcUnityEditorObservation before,
        IpcUnityEditorObservation after)
    {
        var beforeSnapshot = before.State.Generations;
        var afterSnapshot = after.State.Generations;
        return new BuildGenerationsOutput(
            Before: beforeSnapshot,
            After: afterSnapshot,
            ValidFor: afterSnapshot);
    }

    private static IReadOnlyList<BuildClaimOutput> CreateClaims (
        IpcBuildRunResponse response,
        BuildOutput build)
    {
        var reportResult = build.Summary.Result;
        var succeeded = reportResult == IpcBuildReportResult.Succeeded;
        var knownTerminalResult = reportResult is IpcBuildReportResult.Succeeded or IpcBuildReportResult.Failed or IpcBuildReportResult.Canceled;
        var isExecuteMethod = build.Runner.Kind == BuildRunnerKind.ExecuteMethod;
        var hasBuildReport = build.Summary.ReportRef != null;
        var claims = new List<BuildClaimOutput>
        {
            CreateClaim(
                BuildClaimCodes.UnityBuildProfileResolved,
                AssuranceClaimStatus.Passed,
                "Build profile resolved to a deterministic input digest.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["path"] = build.Profile.Path,
                    ["digest"] = build.Profile.Digest,
                },
                [BuildProfileEvidenceOutput.Create(build.Profile)],
                required: true),
            CreateClaim(
                BuildClaimCodes.UnityReadyForBuild,
                IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(response.LifecycleBefore.State.LifecycleState)
                    ? AssuranceClaimStatus.Passed
                    : AssuranceClaimStatus.Failed,
                "Unity lifecycle was ready before BuildPipeline execution.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["lifecycleState"] = response.LifecycleBefore.State.LifecycleState,
                },
                [BuildLifecycleEvidenceOutput.Create(response.LifecycleBefore)],
                required: true),
            CreateClaim(
                BuildClaimCodes.UnityBuildInputsResolved,
                AssuranceClaimStatus.Passed,
                "Unity resolved BuildPipeline BuildTarget and scenes.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["buildTarget"] = build.Inputs.Target.StableName,
                    ["sceneCount"] = build.Inputs.Scenes.Paths.Count,
                },
                [BuildInputEvidenceOutput.Create(response.Input)],
                required: true),
            CreateClaim(
                BuildClaimCodes.UnityBuildRunnerResolved,
                AssuranceClaimStatus.Passed,
                "Build runner was resolved before invocation.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = build.Runner.Kind,
                },
                [BuildRunnerEvidenceOutput.Create(build.Runner)],
                required: true),
            CreateClaim(
                BuildClaimCodes.UnityBuildCompleted,
                knownTerminalResult ? AssuranceClaimStatus.Passed : AssuranceClaimStatus.Indeterminate,
                "Build runner reached a terminal result.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["result"] = build.Summary.Result,
                },
                [CreateSummaryEvidence(build.Summary)],
                required: true),
            CreateClaim(
                BuildClaimCodes.UnityBuildSucceeded,
                succeeded ? AssuranceClaimStatus.Passed : AssuranceClaimStatus.Failed,
                "Build runner reported a successful result.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["result"] = build.Summary.Result,
                    ["errorCount"] = build.Summary.ErrorCount,
                },
                [CreateSummaryEvidence(build.Summary)],
                required: true),
            CreateClaim(
                BuildClaimCodes.UnityBuildResultAccounted,
                AssuranceClaimStatus.Passed,
                "Build runner terminal result was persisted in build metadata.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["source"] = build.RunnerResult.Source,
                    ["status"] = build.RunnerResult.Status,
                },
                [BuildRunnerResultEvidenceOutput.Create(build.RunnerResult)],
                required: true),
            CreateClaim(
                BuildClaimCodes.UnityBuildArtifactsAccounted,
                AssuranceClaimStatus.Passed,
                "Build output artifacts were counted in the output manifest.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["manifestRef"] = BuildArtifactKind.BuildOutputManifest,
                    ["entryCount"] = build.Output.EntryCount,
                    ["fileCount"] = build.Output.FileCount,
                },
                [BuildOutputAccountingEvidenceOutput.Create(build.Output)],
                required: true),
            CreateClaim(
                BuildClaimCodes.UnityBuildOutputDigested,
                AssuranceClaimStatus.Passed,
                "Build output manifest digest was verified against the written artifact.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["manifestDigest"] = build.Output.ManifestDigest,
                },
                [BuildOutputManifestEvidenceOutput.Create(build.Output)],
                required: true),
            CreateClaim(
                BuildClaimCodes.UnityBuildLogsAccounted,
                AssuranceClaimStatus.Passed,
                "Build log byte range was written and summarized.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["reportRef"] = BuildArtifactKind.BuildLog,
                    ["entryCount"] = build.Logs.EntryCount,
                    ["completionReason"] = build.Logs.CompletionReason,
                },
                [BuildLogEvidenceOutput.Create(build.Logs)],
                required: true),
            CreateClaim(
                BuildClaimCodes.UnityBuildProjectMutationAccounted,
                ResolveProjectMutationClaimStatus(response.ProjectMutation),
                "Project mutation audit was recorded according to build policy.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["mode"] = response.ProjectMutation.Mode,
                    ["coverage"] = TextVocabulary.GetText(response.ProjectMutation.Coverage),
                    ["mutated"] = response.ProjectMutation.Mutated,
                },
                [BuildProjectMutationEvidenceOutput.Create(response.ProjectMutation)],
                required: true),
            CreateClaim(
                BuildClaimCodes.UnityBuildValidForGeneration,
                HasCompleteGenerationSnapshot(build.Generations) ? AssuranceClaimStatus.Passed : AssuranceClaimStatus.Indeterminate,
                "Build artifacts declare the Unity lifecycle generations they are valid for.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["compileGeneration"] = build.Generations.ValidFor?.CompileGeneration,
                    ["domainReloadGeneration"] = build.Generations.ValidFor?.DomainReloadGeneration,
                    ["assetRefreshGeneration"] = build.Generations.ValidFor?.AssetRefreshGeneration,
                    ["playModeGeneration"] = build.Generations.ValidFor?.PlayModeGeneration,
                },
                [BuildGenerationEvidenceOutput.Create(build.Generations)],
                required: true),
        };

        if (hasBuildReport)
        {
            claims.Insert(
                7,
                CreateClaim(
                    BuildClaimCodes.UnityBuildReportAccounted,
                    AssuranceClaimStatus.Passed,
                    "BuildReport artifact was written and digested.",
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["reportRef"] = BuildArtifactKind.BuildReport,
                    },
                    [BuildReportSummaryEvidenceOutput.Create(build.Summary)],
                    required: !isExecuteMethod));
        }

        if (isExecuteMethod)
        {
            claims.InsertRange(
                4,
                [
                    CreateClaim(
                        BuildClaimCodes.UnityBuildExecuteMethodResolved,
                        AssuranceClaimStatus.Passed,
                        "executeMethod runner method resolved before invocation.",
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["method"] = build.Runner.Method,
                        },
                        [BuildRunnerEvidenceOutput.Create(build.Runner)],
                        required: true),
                    CreateClaim(
                        BuildClaimCodes.UnityBuildExecuteMethodInvoked,
                        AssuranceClaimStatus.Passed,
                        "executeMethod runner method invocation started.",
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["method"] = build.Runner.Method,
                        },
                        [BuildRunnerEvidenceOutput.Create(build.Runner)],
                        required: true),
                    CreateClaim(
                        BuildClaimCodes.UnityBuildExecuteMethodCompleted,
                        AssuranceClaimStatus.Passed,
                        "executeMethod runner terminal result was observed.",
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["status"] = build.RunnerResult.Status,
                        },
                        [BuildRunnerResultEvidenceOutput.Create(build.RunnerResult)],
                        required: true),
                ]);
        }

        return claims;
    }

    private static AssuranceClaimStatus ResolveProjectMutationClaimStatus (IpcBuildProjectMutationAudit projectMutation)
    {
        return projectMutation.Coverage == IpcBuildProjectMutationAuditCoverage.Full
            ? AssuranceClaimStatus.Passed
            : AssuranceClaimStatus.Indeterminate;
    }

    private static IReadOnlyList<BuildResidualRiskOutput> CreateResidualRisks (
        BuildProfileProjectMutationMode mode,
        IpcBuildProjectMutationAudit projectMutation)
    {
        var hasMutationRisk = mode == BuildProfileProjectMutationMode.Audit && projectMutation.Mutated;
        var hasCoverageRisk = (mode == BuildProfileProjectMutationMode.Audit || mode == BuildProfileProjectMutationMode.AllowWithAudit)
            && projectMutation.Coverage != IpcBuildProjectMutationAuditCoverage.Full;
        if (hasMutationRisk || hasCoverageRisk)
        {
            return
            [
                CreateProjectMutationRisk(
                    BuildRiskCodes.ProjectMutationDetected,
                    hasCoverageRisk
                        ? "Project mutation audit evidence or incomplete audit coverage should be reviewed for this build run."
                        : "Project mutation audit evidence should be reviewed for this build run."),
            ];
        }

        return EmptyResidualRisks;
    }

    private static BuildResidualRiskOutput CreateProjectMutationRisk (
        UcliCode code,
        string statement)
    {
        return new BuildResidualRiskOutput(
            Code: code,
            Severity: UcliDiagnosticSeverity.Warning,
            Blocking: false,
            Message: statement);
    }

    private static bool IsForbiddenProjectMutationViolation (
        BuildProfileProjectMutationMode mode,
        IpcBuildProjectMutationAudit projectMutation)
    {
        return mode == BuildProfileProjectMutationMode.Forbid
            && (projectMutation.Mutated || projectMutation.Coverage != IpcBuildProjectMutationAuditCoverage.Full);
    }

    private static BuildEvidenceOutput CreateSummaryEvidence (BuildSummaryOutput data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data.ReportRef switch
        {
            BuildArtifactKind.BuildReport => BuildReportSummaryEvidenceOutput.Create(data),
            null => BuildSummaryEvidenceOutput.Create(data),
            _ => throw new ArgumentOutOfRangeException(
                nameof(data),
                data.ReportRef,
                "Build summary evidence must identify the build or BuildReport artifact."),
        };
    }

    private static bool HasCompleteGenerationSnapshot (BuildGenerationsOutput generations)
    {
        return generations.Before is not null
            && generations.After is not null
            && generations.ValidFor is not null;
    }

    private static BuildClaimOutput CreateClaim (
        UcliCode id,
        AssuranceClaimStatus status,
        string statement,
        IReadOnlyDictionary<string, object?> subject,
        IReadOnlyList<BuildEvidenceOutput> evidence,
        bool required)
    {
        return new BuildClaimOutput(
            Id: id,
            Status: status,
            Coverage: status == AssuranceClaimStatus.Indeterminate ? AssuranceCoverage.None : AssuranceCoverage.Full,
            Required: required,
            VerifierRef: VerifierId,
            Statement: statement,
            Subject: subject,
            Evidence: evidence,
            ResidualRisks: EmptyResidualRisks);
    }

    private static ApplicationFailure? ValidateRuntimePolicy (
        ResolvedBuildRuntimePolicy policy,
        UnityExecutionTarget executionTarget)
    {
        var resolvedExecutionMode = ResolveProfileRuntimeExecutionMode(executionTarget);
        if (!policy.AllowedExecutionModes.Contains(resolvedExecutionMode))
        {
            var modeLiteral = TextVocabulary.GetText(resolvedExecutionMode);
            return ApplicationFailure.EnvironmentError(
                $"Build runtime policy does not allow resolved execution mode '{modeLiteral}'.",
                BuildErrorCodes.BuildRuntimePolicyViolation,
                instancePath: null);
        }

        if (executionTarget == UnityExecutionTarget.Oneshot
            && !policy.AllowedEditorModes.Contains(DaemonEditorMode.Batchmode))
        {
            return ApplicationFailure.EnvironmentError(
                "Build runtime policy does not allow oneshot batchmode editor execution.",
                BuildErrorCodes.BuildRuntimePolicyViolation,
                instancePath: null);
        }

        return null;
    }

    private static BuildProfileRuntimeExecutionMode ResolveProfileRuntimeExecutionMode (UnityExecutionTarget executionTarget)
    {
        return executionTarget switch
        {
            UnityExecutionTarget.Daemon => BuildProfileRuntimeExecutionMode.Daemon,
            UnityExecutionTarget.Oneshot => BuildProfileRuntimeExecutionMode.Oneshot,
            _ => throw new ArgumentOutOfRangeException(nameof(executionTarget), executionTarget, "Unsupported execution target."),
        };
    }

    private static ApplicationFailure CreateTimeoutFailure (TimeSpan timeout)
    {
        return ApplicationFailure.Timeout(
            $"Unity build assurance timed out after {timeout.TotalMilliseconds:0} milliseconds.",
            ExecutionErrorCodes.IpcTimeout,
            instancePath: null,
            startupFailure: null);
    }

    private sealed record BuildResponseResolutionResult (
        IpcBuildRunResponse? Response,
        BuildPipelineOutputLayout? OutputLayout,
        ApplicationFailure? Error,
        IpcBuildRunErrorPayload? ErrorPayload)
    {
        public bool IsSuccess => Response != null && Error == null;

        public static BuildResponseResolutionResult Success (
            IpcBuildRunResponse response,
            BuildPipelineOutputLayout? outputLayout)
        {
            ArgumentNullException.ThrowIfNull(response);
            return new BuildResponseResolutionResult(response, outputLayout, null, null);
        }

        public static BuildResponseResolutionResult Failure (
            ApplicationFailure failure,
            IpcBuildRunErrorPayload? errorPayload)
        {
            ArgumentNullException.ThrowIfNull(failure);
            return new BuildResponseResolutionResult(null, null, failure, errorPayload);
        }
    }

    private sealed record OutputSourcesResolutionResult (
        IReadOnlyList<BuildOutputSourceEntry>? OutputSources,
        ApplicationFailure? Error)
    {
        public static OutputSourcesResolutionResult Success (IReadOnlyList<BuildOutputSourceEntry> outputSources)
        {
            ArgumentNullException.ThrowIfNull(outputSources);
            return new OutputSourcesResolutionResult(outputSources, null);
        }

        public static OutputSourcesResolutionResult Failure (ApplicationFailure failure)
        {
            ArgumentNullException.ThrowIfNull(failure);
            return new OutputSourcesResolutionResult(null, failure);
        }
    }

    private sealed record ResolvedRunnerInvocationInput (
        IReadOnlyDictionary<string, string> Arguments,
        IReadOnlyList<string> EnvironmentVariables,
        IReadOnlyList<string> EnvironmentSecrets,
        IReadOnlyDictionary<string, string> EnvironmentVariableValues,
        IReadOnlyDictionary<string, string> EnvironmentSecretValues)
    {
        public static ResolvedRunnerInvocationInput Empty { get; } = new(
            new Dictionary<string, string>(StringComparer.Ordinal),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal));
    }

    private sealed record RunnerEnvironmentResolutionResult (
        IReadOnlyDictionary<string, string>? Values,
        ExecutionError? Error)
    {
        public bool IsSuccess => Values != null && Error == null;

        public static RunnerEnvironmentResolutionResult Success (IReadOnlyDictionary<string, string> values)
        {
            ArgumentNullException.ThrowIfNull(values);
            return new RunnerEnvironmentResolutionResult(values, null);
        }

        public static RunnerEnvironmentResolutionResult Failure (ExecutionError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new RunnerEnvironmentResolutionResult(null, error);
        }
    }

    private sealed record RunnerInvocationResolutionResult (
        ResolvedRunnerInvocationInput? Invocation,
        ExecutionError? Error)
    {
        public bool IsSuccess => Invocation != null && Error == null;

        public static RunnerInvocationResolutionResult Success (ResolvedRunnerInvocationInput invocation)
        {
            ArgumentNullException.ThrowIfNull(invocation);
            return new RunnerInvocationResolutionResult(invocation, null);
        }

        public static RunnerInvocationResolutionResult Failure (ExecutionError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new RunnerInvocationResolutionResult(null, error);
        }
    }
}
