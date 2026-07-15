using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Metadata;
using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.EnvironmentVariables;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Shared.Identifiers;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Contracts.Text;

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

        var profile = profileResolutionResult.Profile!;
        var executionTarget = modeDecisionResult.Decision!.Target;
        var runtimePolicyFailure = ValidateRuntimePolicy(profile.Policy.Runtime, executionTarget);
        if (runtimePolicyFailure != null)
        {
            return BuildExecutionResult.Failure(runtimePolicyFailure, project);
        }

        if (!deadline.TryGetRemainingTimeout(out var requestTimeout))
        {
            return BuildExecutionResult.Failure(CreateTimeoutFailure(timeout), project);
        }

        var runId = runIdGenerator.Generate();
        var prepareResult = artifactStore.Prepare(context.UnityProject, runId);
        if (!prepareResult.IsSuccess)
        {
            return BuildExecutionResult.Failure(prepareResult.Error!, project);
        }

        var paths = prepareResult.Paths!;
        IpcBuildOutputLayout? outputLayout = null;
        if (profile.Inputs is ResolvedBuildInputs.Explicit explicitInputs
            && profile.Runner is ResolvedBuildRunner.BuildPipeline)
        {
            if (!IpcBuildOutputLayoutResolver.TryResolve(
                paths.RunnerOutputDirectory,
                explicitInputs.BuildTarget,
                androidAppBundle: false,
                out outputLayout))
            {
                return BuildExecutionResult.Failure(ExecutionError.InvalidArgument(
                    $"BuildPipeline output layout could not be resolved for build target: {ContractLiteralCodec.ToValue(explicitInputs.BuildTarget)}.",
                    BuildErrorCodes.BuildInputsInvalid), project);
            }

            var outputLayoutPrepareResult = artifactStore.PrepareBuildPipelineOutputLayout(
                paths,
                explicitInputs.BuildTarget,
                outputLayout!);
            if (!outputLayoutPrepareResult.IsSuccess)
            {
                return BuildExecutionResult.Failure(outputLayoutPrepareResult.Error!, project);
            }
        }

        var runnerInvocationResult = ResolveRunnerInvocation(
            profile,
            profileReadResult.DisplayPath!,
            runId,
            paths.RunnerOutputDirectory,
            context.UnityProject.UnityProjectRoot,
            context.UnityProject.ProjectFingerprint);
        if (!runnerInvocationResult.IsSuccess)
        {
            return BuildExecutionResult.Failure(runnerInvocationResult.Error!, project);
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
            profileReadResult.DisplayPath!,
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
            return BuildExecutionResult.Failure(
                ApplicationFailure.FromCode(
                    BuildErrorCodes.BuildRunnerInvocationFailed,
                    exception.Message),
                project);
        }

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
            profile,
            paths.RunnerOutputDirectory);
        if (!responseResult.IsSuccess)
        {
            var dirtyState = responseResult.Error!.Code == BuildErrorCodes.BuildDirtyStatePresent
                || responseResult.Error.Code == BuildErrorCodes.BuildDirtyStateIndeterminate
                ? responseResult.ErrorPayload?.DirtyState
                : null;
            return BuildExecutionResult.Failure(
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
            return BuildExecutionResult.Failure(CreateTimeoutFailure(timeout), project);
        }

        using var artifactAccountingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        artifactAccountingCancellationTokenSource.CancelAfter(artifactAccountingTimeout);
        var artifactCancellationToken = artifactAccountingCancellationTokenSource.Token;
        try
        {
            var terminalResult = GetTerminalResult(buildResponse);
            var buildReportResult = ResolveBuildReportSource(buildResponse);
            if (buildReportResult.Error != null)
            {
                return BuildExecutionResult.Failure(buildReportResult.Error, project);
            }

            var resolvedOutputLayout = buildResponse.OutputLayout ?? outputLayout;
            var outputSourcesResult = ResolveOutputSources(
                buildResponse,
                resolvedOutputLayout,
                profile.Runner.Kind,
                terminalResult);
            if (outputSourcesResult.Error != null)
            {
                return BuildExecutionResult.Failure(outputSourcesResult.Error, project);
            }

            var accountingResult = await artifactStore.AccountArtifactsAsync(
                    new BuildRunArtifactAccountingRequest(
                        paths,
                        buildResponse.Input.BuildTarget,
                        buildResponse.Input.UnityBuildTarget,
                        buildReportResult.BuildReport,
                        outputSourcesResult.OutputSources!,
                        CanWriteEmptyOutputManifest(terminalResult)),
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
                runnerInvocation);
            var metadata = CreateMetadataDocument(
                output,
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
                return BuildExecutionResult.Failure(metadataWriteResult.Error!, project);
            }

            if (IsForbiddenProjectMutationViolation(profile.Policy.ProjectMutationMode, buildResponse.ProjectMutation))
            {
                return BuildExecutionResult.Failure(
                    ApplicationFailure.FromCode(
                        BuildErrorCodes.BuildProjectMutationForbidden,
                        "Build project mutation policy forbids project changes or incomplete mutation audit coverage during runner invocation."),
                    project);
            }

            var completedOutput = new BuildExecutionOutput(
                Verdict: output.Verdict,
                Project: output.Project,
                Build: output.Build,
                Verifiers: output.Verifiers,
                Claims: output.Claims,
                Reports: CreateReports(
                    accounting,
                    metadataWriteResult.Artifact!),
                ResidualRisks: output.ResidualRisks);
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
            return BuildExecutionResult.Success(completedOutput);
        }
        catch (OperationCanceledException) when (artifactAccountingCancellationTokenSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return BuildExecutionResult.Failure(CreateTimeoutFailure(timeout), project);
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
        string profilePath,
        Guid runId,
        string outputDirectory,
        string projectPath,
        ProjectFingerprint projectFingerprint)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Runner is ResolvedBuildRunner.BuildPipeline)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(profilePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
            ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
            ArgumentNullException.ThrowIfNull(projectFingerprint);
            return RunnerInvocationResolutionResult.Success(ResolvedRunnerInvocationInput.Empty);
        }

        var executeMethodRunner = (ResolvedBuildRunner.ExecuteMethod)profile.Runner;
        var explicitInputs = (ResolvedBuildInputs.Explicit)profile.Inputs;

        if (!TryValidateRequiredPathVariable("ucli.build.profilePath", profilePath, out var pathError)
            || !TryValidateRequiredPathVariable("ucli.build.outputDir", outputDirectory, out pathError)
            || !TryValidateRequiredPathVariable("project.path", projectPath, out pathError))
        {
            return RunnerInvocationResolutionResult.Failure(pathError!);
        }

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
        string profilePath,
        Guid runId,
        string outputDirectory,
        string projectPath,
        ProjectFingerprint projectFingerprint)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ucli.build.runId"] = runId.ToString("D"),
            ["ucli.build.outputDir"] = outputDirectory,
            ["ucli.build.profilePath"] = profilePath,
            ["ucli.build.profileDigest"] = profile.Digest.ToString(),
            ["project.path"] = projectPath,
            ["project.fingerprint"] = projectFingerprint.ToString(),
            ["build.target"] = ContractLiteralCodec.ToValue(buildTarget),
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

    private static bool TryValidateRequiredPathVariable (
        string variableName,
        string value,
        out ExecutionError? error)
    {
        error = null;
        if (!string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        error = ExecutionError.InvalidArgument(
            $"Build profile runner.invocation.arguments built-in variable resolves to an empty required path: {variableName}.",
            BuildErrorCodes.BuildProfileInvalid);
        return false;
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
        AssuranceVerdict? verdict,
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
        string profilePath,
        BuildRunArtifactPaths paths,
        IpcBuildOutputLayout? outputLayout,
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
            OutputPath: paths.RunnerOutputDirectory,
            OutputLayout: outputLayout,
            BuildReportPath: paths.BuildReportJsonPath,
            BuildLogPath: paths.BuildLogPath,
            AllowedEditorModes: profile.Policy.Runtime.AllowedEditorModes,
            ProjectMutationMode: profile.Policy.ProjectMutationMode,
            RunnerKind: profile.Runner.Kind,
            ProfileDigest: profile.Digest,
            UnityBuildProfile: unityBuildProfile,
            ProfilePath: executeMethodRunner != null ? profilePath : null,
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
        IpcBuildOutputLayout? outputLayout,
        BuildRunnerKind runnerKind,
        IpcBuildReportResult terminalResult)
    {
        if (runnerKind == BuildRunnerKind.BuildPipeline)
        {
            if (outputLayout == null)
            {
                return OutputSourcesResolutionResult.Failure(ApplicationFailure.FromCode(
                    BuildErrorCodes.BuildInputsInvalid,
                    "BuildPipeline output layout is required for output accounting."));
            }

            return OutputSourcesResolutionResult.Success([BuildOutputSourceEntry.FromAbsolutePath(outputLayout.LocationPathName)]);
        }

        var runnerResult = response.RunnerResult;
        if (runnerResult == null)
        {
            return OutputSourcesResolutionResult.Failure(ApplicationFailure.FromCode(
                BuildErrorCodes.BuildRunnerResultMissing,
                "executeMethod runner result is missing."));
        }

        if (terminalResult == IpcBuildReportResult.Succeeded && runnerResult.Outputs.Count == 0)
        {
            return OutputSourcesResolutionResult.Failure(ApplicationFailure.FromCode(
                BuildErrorCodes.BuildRunnerResultInvalid,
                "executeMethod runner result requires at least one output when status is succeeded."));
        }

        if (runnerResult.Outputs.Count == 0)
        {
            return OutputSourcesResolutionResult.Success(Array.Empty<BuildOutputSourceEntry>());
        }

        var outputSources = new BuildOutputSourceEntry[runnerResult.Outputs.Count];
        for (var i = 0; i < runnerResult.Outputs.Count; i++)
        {
            var output = runnerResult.Outputs[i];
            if (!RelativePathContract.TryNormalize(output, out var normalizedOutputPath))
            {
                return OutputSourcesResolutionResult.Failure(ApplicationFailure.FromCode(
                    BuildErrorCodes.BuildOutputPathInvalid,
                    "executeMethod runner result output path is invalid."));
            }

            outputSources[i] = BuildOutputSourceEntry.FromRunnerOutputRelativePath(normalizedOutputPath);
        }

        return OutputSourcesResolutionResult.Success(outputSources);
    }

    private static BuildReportArtifactResolutionResult ResolveBuildReportSource (IpcBuildRunResponse response)
    {
        if (response.RunnerResult?.BuildReport == null)
        {
            return BuildReportArtifactResolutionResult.Success(response.Report == null
                ? null
                : BuildReportSourceEntry.FromArtifact(response.Report));
        }

        var buildReportPath = response.RunnerResult.BuildReport.Path;
        if (!RelativePathContract.TryNormalize(buildReportPath, out var normalizedBuildReportPath))
        {
            return BuildReportArtifactResolutionResult.Failure(ApplicationFailure.FromCode(
                BuildErrorCodes.BuildRunnerResultInvalid,
                "executeMethod runner result buildReport.path is invalid."));
        }

        return BuildReportArtifactResolutionResult.Success(
            BuildReportSourceEntry.FromRunnerOutputRelativePath(normalizedBuildReportPath));
    }

    private static BuildResponseResolutionResult ResolveBuildResponse (
        UnityRequestResponse response,
        Guid expectedRunId,
        ProjectFingerprint expectedProjectFingerprint,
        ResolvedBuildProfile expectedProfile,
        string expectedOutputDirectory)
    {
        if (response.Errors.Count != 0)
        {
            var firstError = response.Errors[0];
            var failure = ApplicationFailure.FromCode(
                firstError.Code,
                firstError.Message,
                firstError.OpId);
            return BuildResponseResolutionResult.Failure(failure, TryReadErrorPayload(response));
        }

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcBuildRunResponse buildResponse, out var payloadError))
        {
            var failure = expectedProfile.Runner is ResolvedBuildRunner.ExecuteMethod
                && JsonObjectPropertyReader.TryGetPropertyIgnoreCase(response.Payload, "runnerResult", out _)
                    ? ApplicationFailure.FromCode(
                        BuildErrorCodes.BuildRunnerResultInvalid,
                        $"Unity build response runnerResult is invalid. {payloadError.Message}")
                    : ApplicationFailure.InternalError($"Unity build payload is invalid. {payloadError.Message}");
            return BuildResponseResolutionResult.Failure(failure);
        }

        var validationFailure = ValidateResponse(
            buildResponse,
            expectedRunId,
            expectedProjectFingerprint,
            expectedProfile,
            expectedOutputDirectory);
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
        Guid expectedRunId,
        ProjectFingerprint expectedProjectFingerprint,
        ResolvedBuildProfile expectedProfile,
        string expectedOutputDirectory)
    {
        if (response.RunId != expectedRunId)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response runId mismatch. Requested={expectedRunId}, Actual={response.RunId}.");
        }

        if (response.ProjectFingerprint != expectedProjectFingerprint)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response projectFingerprint mismatch. Requested={expectedProjectFingerprint}, Actual={response.ProjectFingerprint}.");
        }

        if (response.LifecycleBefore is null
            || response.LifecycleAfter is null)
        {
            return ApplicationFailure.InternalError("Unity build response contains a missing or invalid Unity Editor state snapshot.");
        }

        var inputKind = response.Input.InputKind;
        if (inputKind != expectedProfile.Inputs.Kind)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response input kind mismatch. Requested={ContractLiteralCodec.ToValue(expectedProfile.Inputs.Kind)}, Actual={response.Input.InputKind}.");
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
            response.OutputLayout,
            response.Input.BuildTarget,
            expectedOutputDirectory,
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
                var expectedSceneSource = ContractLiteralCodec.ToValue(expectedExplicitInputs.Scenes.Source);
                return ApplicationFailure.InternalError(
                    $"Unity build response scene source mismatch. Requested={expectedSceneSource}, Actual={response.Input.SceneSource}.");
            }

            if (!HasExpectedDevelopmentBuildOption(
                response.Input.BuildOptions,
                expectedExplicitInputs.Options.Development))
            {
                return ApplicationFailure.InternalError(
                    $"Unity build response build options mismatch. RequestedDevelopment={expectedExplicitInputs.Options.Development}, Actual={response.Input.BuildOptions}.");
            }
        }
        else if (sceneSource != BuildProfileSceneSource.UnityBuildProfile)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response scene source mismatch. Requested={ContractLiteralCodec.ToValue(BuildProfileSceneSource.UnityBuildProfile)}, Actual={response.Input.SceneSource}.");
        }

        IpcBuildReportResult? reportResult = null;
        if (expectedProfile.Runner.Kind == BuildRunnerKind.BuildPipeline)
        {
            if (response.Report == null)
            {
                return ApplicationFailure.FromCode(
                    BuildErrorCodes.BuildReportMissing,
                    "Unity build response BuildReport is missing for buildPipeline runner.");
            }

            if (!IsTerminalBuildReportResult(response.Report.Result))
            {
                return ApplicationFailure.InternalError($"Unity build response contains non-terminal report result: {response.Report.Result}.");
            }

            if (!string.Equals(response.Report.UnityBuildTarget, response.Input.UnityBuildTarget, StringComparison.Ordinal))
            {
                return ApplicationFailure.InternalError(
                    $"Unity BuildReport BuildTarget mismatch. Input={response.Input.UnityBuildTarget}, Report={response.Report.UnityBuildTarget}.");
            }

            reportResult = response.Report.Result;
        }
        else if (response.Report != null)
        {
            return ApplicationFailure.InternalError("Unity build response must not include a BuildReport payload for executeMethod runner.");
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
                $"Unity build response log completionReason mismatch. Expected={ContractLiteralCodec.ToValue(expectedCompletionReason)}, Actual={response.Logs.CompletionReason}.");
        }

        if (response.Input.Scenes.Count == 0)
        {
            return ApplicationFailure.InternalError("Unity build response contains no resolved build scenes.");
        }

        if (expectedExplicitInputs?.Scenes is ResolvedBuildScenes.Explicit expectedExplicitScenes
            && !response.Input.Scenes.SequenceEqual(expectedExplicitScenes.Paths))
        {
            return ApplicationFailure.InternalError("Unity build response resolved scenes do not match the requested explicit build scenes.");
        }

        return ValidateProjectMutationAudit(response.ProjectMutation, expectedProfile.Policy.ProjectMutationMode);
    }

    private static ApplicationFailure? ValidateResponseOutputLayout (
        IpcBuildOutputLayout? outputLayout,
        BuildTargetStableName buildTarget,
        string expectedOutputDirectory,
        BuildProfileInputsKind inputKind,
        BuildRunnerKind runnerKind)
    {
        if (runnerKind == BuildRunnerKind.ExecuteMethod)
        {
            return outputLayout == null
                ? null
                : ApplicationFailure.InternalError("Unity build response outputLayout must be omitted for executeMethod runner.");
        }

        if (outputLayout == null)
        {
            return ApplicationFailure.InternalError("Unity build response outputLayout is missing.");
        }

        if (string.IsNullOrWhiteSpace(outputLayout.LocationPathName))
        {
            return ApplicationFailure.InternalError("Unity build response outputLayout is invalid.");
        }

        if (!IpcBuildOutputLayoutResolver.TryResolve(
            expectedOutputDirectory,
            buildTarget,
            androidAppBundle: false,
            out var expectedOutputLayout))
        {
            return ApplicationFailure.InternalError($"Unity build response buildTarget does not have a supported output layout: {buildTarget}.");
        }

        if (IsExpectedOutputLayout(outputLayout, expectedOutputLayout!))
        {
            return null;
        }

        if (inputKind == BuildProfileInputsKind.UnityBuildProfile
            && buildTarget == BuildTargetStableName.Android
            && IpcBuildOutputLayoutResolver.TryResolve(
                expectedOutputDirectory,
                buildTarget,
                androidAppBundle: true,
                out var androidAppBundleLayout)
            && IsExpectedOutputLayout(outputLayout, androidAppBundleLayout!))
        {
            return null;
        }

        return ApplicationFailure.InternalError("Unity build response outputLayout does not match the resolved build target.");
    }

    private static bool IsExpectedOutputLayout (
        IpcBuildOutputLayout actual,
        IpcBuildOutputLayout expected)
    {
        return actual.Shape == expected.Shape
            && string.Equals(actual.LocationPathName, expected.LocationPathName, StringComparison.Ordinal);
    }

    private static ApplicationFailure? ValidateExplicitResponseInputs (
        IpcBuildRunResponse response,
        ResolvedBuildInputs.Explicit expectedInputs)
    {
        if (response.UnityBuildProfile != null)
        {
            return ApplicationFailure.InternalError("Unity build response unityBuildProfile input must be omitted for explicit build inputs.");
        }

        if (response.Input.BuildTarget != expectedInputs.BuildTarget)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response buildTarget mismatch. Requested={ContractLiteralCodec.ToValue(expectedInputs.BuildTarget)}, Actual={response.Input.BuildTarget}.");
        }

        return null;
    }

    private static ApplicationFailure? ValidateResponseInputBuildTarget (IpcBuildInputProbe input)
    {
        if (!BuildTargetStableNameUnityBuildTargetResolver.TryResolve(input.BuildTarget, out var expectedUnityBuildTarget))
        {
            return ApplicationFailure.InternalError(
                $"Unity build response contains an unsupported buildTarget: {input.BuildTarget}.");
        }

        if (!string.Equals(expectedUnityBuildTarget, input.UnityBuildTarget, StringComparison.Ordinal))
        {
            return ApplicationFailure.InternalError(
                $"Unity build response target mismatch. BuildTarget={ContractLiteralCodec.ToValue(input.BuildTarget)}, ExpectedUnityBuildTarget={expectedUnityBuildTarget}, ActualUnityBuildTarget={input.UnityBuildTarget}.");
        }

        return null;
    }

    private static ApplicationFailure? ValidateUnityBuildProfileResponseInputs (
        IpcBuildRunResponse response,
        ResolvedBuildInputs.UnityBuildProfile expectedInputs)
    {
        if (response.UnityBuildProfile == null)
        {
            return ApplicationFailure.InternalError("Unity build response unityBuildProfile input is missing.");
        }

        if (response.UnityBuildProfile.Path != expectedInputs.Path)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response unityBuildProfile path mismatch. Requested={expectedInputs.Path}, Actual={response.UnityBuildProfile.Path}.");
        }

        if (response.UnityBuildProfile.Digest == null)
        {
            return ApplicationFailure.InternalError("Unity build response unityBuildProfile digest is missing.");
        }

        if (response.UnityBuildProfile.ApplyAudit == null)
        {
            return ApplicationFailure.InternalError("Unity build response unityBuildProfile applyAudit is missing.");
        }

        if (!response.UnityBuildProfile.ApplyAudit.Applied)
        {
            return ApplicationFailure.InternalError("Unity build response unityBuildProfile applyAudit.applied must be true.");
        }

        return ValidateUnityBuildProfileApplyAudit(response.UnityBuildProfile.ApplyAudit);
    }

    private static ApplicationFailure? ValidateUnityBuildProfileApplyAudit (IpcUnityBuildProfileApplyAudit applyAudit)
    {
        if (applyAudit.LifecycleBefore == null)
        {
            return ApplicationFailure.InternalError("Unity build response unityBuildProfile applyAudit.lifecycleBefore is missing.");
        }

        if (applyAudit.LifecycleAfter == null)
        {
            return ApplicationFailure.InternalError("Unity build response unityBuildProfile applyAudit.lifecycleAfter is missing.");
        }

        if (applyAudit.DirtyStateAfter == null)
        {
            return ApplicationFailure.InternalError("Unity build response unityBuildProfile applyAudit.dirtyStateAfter is missing.");
        }

        return ValidateUnityBuildProfileDirtyStateAfter(applyAudit.DirtyStateAfter);
    }

    private static ApplicationFailure? ValidateUnityBuildProfileDirtyStateAfter (IpcBuildDirtyState dirtyState)
    {
        string? previousPath = null;
        for (var i = 0; i < dirtyState.Items.Count; i++)
        {
            var item = dirtyState.Items[i];
            if (!IsAuditedProjectMutationPath(item.Path))
            {
                return ApplicationFailure.InternalError(
                    $"Unity build response unityBuildProfile applyAudit.dirtyStateAfter item at index {i} contains invalid path: {item.Path}.");
            }

            if (previousPath != null
                && string.CompareOrdinal(previousPath, item.Path) >= 0)
            {
                return ApplicationFailure.InternalError("Unity build response unityBuildProfile applyAudit.dirtyStateAfter items must be ordered by unique project-relative path.");
            }

            previousPath = item.Path;
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
                ? ApplicationFailure.FromCode(
                    BuildErrorCodes.BuildRunnerResultMissing,
                    "Unity build response runnerResult is missing for executeMethod runner.")
                : null;
        }

        var expectedSource = expectedRunnerKind == BuildRunnerKind.ExecuteMethod
            ? IpcBuildRunnerResultSource.UcliBuildRunnerResult
            : IpcBuildRunnerResultSource.BuildPipelineBuildReport;
        if (runnerResult.Source != expectedSource)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response runnerResult source is invalid for {ContractLiteralCodec.ToValue(expectedRunnerKind)} runner: {runnerResult.Source}.");
        }

        if (runnerResult.DurationMilliseconds < 0
            || runnerResult.ErrorCount < 0
            || runnerResult.WarningCount < 0)
        {
            return ApplicationFailure.FromCode(
                BuildErrorCodes.BuildRunnerResultInvalid,
                "Unity build response runnerResult summary is invalid.");
        }

        if (expectedRunnerKind == BuildRunnerKind.BuildPipeline)
        {
            if (report == null || reportResult == null)
            {
                return ApplicationFailure.FromCode(
                    BuildErrorCodes.BuildReportMissing,
                    "Unity build response BuildReport is missing for buildPipeline runner.");
            }

            if (runnerResult.Status != reportResult.Value)
            {
                return ApplicationFailure.InternalError(
                    $"Unity build response runnerResult status mismatch. Report={ContractLiteralCodec.ToValue(reportResult.Value)}, RunnerResult={runnerResult.Status}.");
            }

            if (runnerResult.DurationMilliseconds != report.DurationMilliseconds
                || runnerResult.ErrorCount != report.ErrorCount
                || runnerResult.WarningCount != report.WarningCount)
            {
                return ApplicationFailure.InternalError("Unity build response runnerResult summary does not match report summary.");
            }
        }

        return null;
    }

    private static ApplicationFailure? ValidateProjectMutationAudit (
        IpcBuildProjectMutationAudit projectMutation,
        BuildProfileProjectMutationMode expectedMode)
    {
        if (projectMutation == null)
        {
            return ApplicationFailure.InternalError("Unity build response projectMutation audit is missing.");
        }

        if (projectMutation.Mode != expectedMode)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response projectMutation mode mismatch. Requested={ContractLiteralCodec.ToValue(expectedMode)}, Actual={ContractLiteralCodec.ToValue(projectMutation.Mode)}.");
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

    private static bool IsAuditedProjectMutationPath (string path)
    {
        return path.StartsWith("Assets/", StringComparison.Ordinal)
            || path.StartsWith("ProjectSettings/", StringComparison.Ordinal)
            || path.StartsWith("Packages/", StringComparison.Ordinal);
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
        Guid runId,
        string profilePath,
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
            ReportRef: BuildArtifactKind.BuildLog,
            EntryCount: response.Logs.EntryCount,
            ErrorCount: response.Logs.ErrorCount,
            WarningCount: response.Logs.WarningCount,
            CompletionReason: response.Logs.CompletionReason,
            Window: new BuildLogWindowOutput(
                StartedAtUtc: response.Logs.Window.StartedAtUtc,
                CompletedAtUtc: response.Logs.Window.CompletedAtUtc,
                CursorStart: response.Logs.Window.CursorStart,
                CursorEnd: response.Logs.Window.CursorEnd));
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
        var build = new BuildOutput(
            runId: runId,
            profile: new BuildProfileOutput(profilePath, profile.Digest),
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
                ManifestRef: BuildArtifactKind.BuildOutputManifest,
                ManifestDigest: accounting.OutputManifest.ManifestDigest,
                EntryCount: accounting.OutputManifest.EntryCount,
                FileCount: accounting.OutputManifest.FileCount,
                TotalBytes: accounting.OutputManifest.TotalBytes),
            generations: generations,
            summary: summary,
            logs: logs);
        var residualRisks = CreateResidualRisks(profile.Policy.ProjectMutationMode, response.ProjectMutation);
        var claims = CreateClaims(response, build);
        return new BuildExecutionOutput(
            Verdict: RecalculateVerdict(claims, residualRisks),
            Project: project,
            Build: build,
            Verifiers:
            [
                new BuildVerifierOutput(
                    Id: VerifierId,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: claims.Where(static claim => claim.Required).Select(static claim => claim.Id).ToArray(),
                    Effects: AssuranceEffectSets.CreateBuild(build.Runner.Kind, accounting.BuildReport != null),
                    ReportRef: BuildArtifactKind.Build),
            ],
            Claims: claims,
            Reports: CreateReports(accounting, buildArtifact: null),
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

    private static IReadOnlyDictionary<BuildArtifactKind, AssuranceReportReference> CreateReports (
        BuildRunArtifactAccountingResult accounting,
        BuildArtifactRef? buildArtifact)
    {
        var reports = new Dictionary<BuildArtifactKind, AssuranceReportReference>
        {
            [BuildArtifactKind.Build] = AssuranceReportReference.FromPath(buildArtifact?.Path ?? "build.json", buildArtifact?.Digest),
            [BuildArtifactKind.BuildOutputManifest] = AssuranceReportReference.FromPath(accounting.BuildOutputManifest.Path, accounting.BuildOutputManifest.Digest),
            [BuildArtifactKind.BuildLog] = AssuranceReportReference.FromPath(accounting.BuildLog.Path, accounting.BuildLog.Digest),
        };
        if (accounting.BuildReport != null)
        {
            reports.Add(
                BuildArtifactKind.BuildReport,
                AssuranceReportReference.FromPath(accounting.BuildReport.Path, accounting.BuildReport.Digest));
        }

        return reports;
    }

    private static IReadOnlyList<BuildArtifactKind> CreateReportRefs (IReadOnlyDictionary<BuildArtifactKind, AssuranceReportReference> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);
        var refs = new List<BuildArtifactKind>(capacity: 4);
        if (reports.ContainsKey(BuildArtifactKind.Build))
        {
            refs.Add(BuildArtifactKind.Build);
        }

        if (reports.ContainsKey(BuildArtifactKind.BuildReport))
        {
            refs.Add(BuildArtifactKind.BuildReport);
        }

        if (reports.ContainsKey(BuildArtifactKind.BuildOutputManifest))
        {
            refs.Add(BuildArtifactKind.BuildOutputManifest);
        }

        if (reports.ContainsKey(BuildArtifactKind.BuildLog))
        {
            refs.Add(BuildArtifactKind.BuildLog);
        }

        return refs.ToArray();
    }

    private static BuildRunMetadataDocument CreateMetadataDocument (
        BuildExecutionOutput output,
        IpcBuildRunResponse response,
        ResolvedBuildProfile profile,
        IpcBuildOutputLayout? outputLayout,
        BuildRunArtifactAccountingResult accounting)
    {
        var invocationEnv = output.Build.Runner.Invocation.Environment;
        var executeMethodRunner = profile.Runner as ResolvedBuildRunner.ExecuteMethod;
        return new BuildRunMetadataDocument(
            schemaVersion: BuildMetadataSchemaVersion,
            runId: output.Build.RunId,
            profile: SerializeMetadataElement(output.Build.Profile),
            inputs: SerializeMetadataElement(CreateInputMetadata(output.Build.Inputs, response.UnityBuildProfile)),
            runner: SerializeMetadataElement(new BuildRunRunnerMetadata(
                Kind: profile.Runner.Kind,
                Method: executeMethodRunner?.Method,
                Invocation: new BuildRunRunnerInvocationMetadata(
                    Arguments: output.Build.Runner.Invocation.Arguments,
                    Environment: new BuildRunRunnerInvocationEnvironmentMetadata(
                        Variables: invocationEnv.Variables,
                        Secrets: invocationEnv.Secrets)),
                OutputLayout: outputLayout)),
            runnerResult: SerializeMetadataElement(CreateRunnerResultMetadata(output, response, accounting.BuildReport != null)),
            lifecycle: SerializeMetadataElement(new BuildRunLifecycleMetadata(
                Before: response.LifecycleBefore,
                After: response.LifecycleAfter)),
            generations: SerializeMetadataElement(output.Build.Generations),
            summary: SerializeMetadataElement(output.Build.Summary),
            logs: SerializeMetadataElement(output.Build.Logs),
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
        BuildExecutionOutput output,
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
            output.Build.RunnerResult.Source,
            output.Build.RunnerResult.Status,
            summary = new
            {
                output.Build.Summary.DurationMilliseconds,
                output.Build.Summary.ErrorCount,
                output.Build.Summary.WarningCount,
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
        var terminalEvidenceKind = ContractLiteralCodec.ToValue(
            isExecuteMethod ? AssuranceEffect.UnityExecuteMethod : AssuranceEffect.UnityBuildPipeline);
        var terminalEvidenceRef = isExecuteMethod ? BuildArtifactKind.Build : BuildArtifactKind.BuildReport;

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
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEvidenceKind.BuildProfile), EvidenceRef: BuildArtifactKind.Build, Data: build.Profile)]),
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
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(AssuranceEffect.UnityLifecycleRead), EvidenceRef: null, Data: response.LifecycleBefore)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildInputsResolved,
                AssuranceClaimStatus.Passed,
                "Unity resolved BuildPipeline BuildTarget and scenes.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["buildTarget"] = build.Inputs.Target.StableName,
                    ["sceneCount"] = build.Inputs.Scenes.Paths.Count,
                },
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEvidenceKind.BuildInput), EvidenceRef: BuildArtifactKind.Build, Data: response.Input)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildRunnerResolved,
                AssuranceClaimStatus.Passed,
                "Build runner was resolved before invocation.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = build.Runner.Kind,
                },
                [new BuildEvidenceOutput(Kind: ResolveRunnerEffect(build.Runner.Kind), EvidenceRef: BuildArtifactKind.Build, Data: null)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildCompleted,
                knownTerminalResult ? AssuranceClaimStatus.Passed : AssuranceClaimStatus.Indeterminate,
                "Build runner reached a terminal result.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["result"] = build.Summary.Result,
                },
                [new BuildEvidenceOutput(Kind: terminalEvidenceKind, EvidenceRef: terminalEvidenceRef, Data: build.Summary)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildSucceeded,
                succeeded ? AssuranceClaimStatus.Passed : AssuranceClaimStatus.Failed,
                "Build runner reported a successful result.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["result"] = build.Summary.Result,
                    ["errorCount"] = build.Summary.ErrorCount,
                },
                [new BuildEvidenceOutput(Kind: terminalEvidenceKind, EvidenceRef: terminalEvidenceRef, Data: build.Summary)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildResultAccounted,
                AssuranceClaimStatus.Passed,
                "Build runner terminal result was persisted in build metadata.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["source"] = build.RunnerResult.Source,
                    ["status"] = build.RunnerResult.Status,
                },
                [new BuildEvidenceOutput(Kind: terminalEvidenceKind, EvidenceRef: BuildArtifactKind.Build, Data: build.RunnerResult)]),
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
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(AssuranceEffect.OutputManifestWrite), EvidenceRef: BuildArtifactKind.Build, Data: build.Output)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildOutputDigested,
                AssuranceClaimStatus.Passed,
                "Build output manifest digest was verified against the written artifact.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["manifestDigest"] = build.Output.ManifestDigest,
                },
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(AssuranceEffect.OutputManifestWrite), EvidenceRef: BuildArtifactKind.BuildOutputManifest, Data: null)]),
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
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(AssuranceEffect.UnityLogWindowRead), EvidenceRef: BuildArtifactKind.BuildLog, Data: build.Logs)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildProjectMutationAccounted,
                ResolveProjectMutationClaimStatus(response.ProjectMutation),
                "Project mutation audit was recorded according to build policy.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["mode"] = response.ProjectMutation.Mode,
                    ["coverage"] = ContractLiteralCodec.ToValue(response.ProjectMutation.Coverage),
                    ["mutated"] = response.ProjectMutation.Mutated,
                },
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(AssuranceEffect.ProjectMutationAudit), EvidenceRef: BuildArtifactKind.Build, Data: response.ProjectMutation)]),
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
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(AssuranceEffect.GenerationSnapshot), EvidenceRef: BuildArtifactKind.Build, Data: build.Generations)]),
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
                    [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(AssuranceEffect.UnityBuildReportRead), EvidenceRef: BuildArtifactKind.BuildReport, Data: null)],
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
                        [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(AssuranceEffect.UnityExecuteMethod), EvidenceRef: BuildArtifactKind.Build, Data: null)]),
                    CreateClaim(
                        BuildClaimCodes.UnityBuildExecuteMethodInvoked,
                        AssuranceClaimStatus.Passed,
                        "executeMethod runner method invocation started.",
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["method"] = build.Runner.Method,
                        },
                        [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(AssuranceEffect.UnityExecuteMethod), EvidenceRef: BuildArtifactKind.Build, Data: null)]),
                    CreateClaim(
                        BuildClaimCodes.UnityBuildExecuteMethodCompleted,
                        AssuranceClaimStatus.Passed,
                        "executeMethod runner terminal result was observed.",
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["status"] = build.RunnerResult.Status,
                        },
                        [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(AssuranceEffect.UnityExecuteMethod), EvidenceRef: BuildArtifactKind.Build, Data: build.RunnerResult)]),
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
            Code: code.Value,
            Severity: UcliDiagnosticSeverity.Warning,
            Blocking: false,
            Statement: statement);
    }

    private static bool IsForbiddenProjectMutationViolation (
        BuildProfileProjectMutationMode mode,
        IpcBuildProjectMutationAudit projectMutation)
    {
        return mode == BuildProfileProjectMutationMode.Forbid
            && (projectMutation.Mutated || projectMutation.Coverage != IpcBuildProjectMutationAuditCoverage.Full);
    }

    private static string ResolveRunnerEffect (BuildRunnerKind runnerKind)
    {
        return runnerKind == BuildRunnerKind.ExecuteMethod
            ? ContractLiteralCodec.ToValue(AssuranceEffect.UnityExecuteMethod)
            : ContractLiteralCodec.ToValue(AssuranceEffect.UnityBuildPipeline);
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
        bool required = true)
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

    private static AssuranceVerdict RecalculateVerdict (
        IReadOnlyList<BuildClaimOutput> claims,
        IReadOnlyList<BuildResidualRiskOutput> residualRisks)
    {
        return AssuranceVerdictCalculator.Calculate(
            claims
                .Select(static claim => new AssuranceVerdictClaimState(
                    Status: claim.Status,
                    Coverage: claim.Coverage,
                    Required: claim.Required,
                    HasBlockingResidualRisk: claim.ResidualRisks.Any(static risk => risk.Blocking)))
                .ToArray(),
            residualRisks
                .Select(static risk => new AssuranceVerdictResidualRiskState(risk.Blocking))
                .ToArray());
    }

    private static ApplicationFailure? ValidateRuntimePolicy (
        ResolvedBuildRuntimePolicy policy,
        UnityExecutionTarget executionTarget)
    {
        var resolvedExecutionMode = ResolveProfileRuntimeExecutionMode(executionTarget);
        if (!policy.AllowedExecutionModes.Contains(resolvedExecutionMode))
        {
            var modeLiteral = ContractLiteralCodec.ToValue(resolvedExecutionMode);
            return ApplicationFailure.FromCode(
                BuildErrorCodes.BuildRuntimePolicyViolation,
                $"Build runtime policy does not allow resolved execution mode '{modeLiteral}'.");
        }

        if (executionTarget == UnityExecutionTarget.Oneshot
            && !policy.AllowedEditorModes.Contains(DaemonEditorMode.Batchmode))
        {
            return ApplicationFailure.FromCode(
                BuildErrorCodes.BuildRuntimePolicyViolation,
                "Build runtime policy does not allow oneshot batchmode editor execution.");
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

    private sealed record BuildReportArtifactResolutionResult (
        BuildReportSourceEntry? BuildReport,
        ApplicationFailure? Error)
    {
        public static BuildReportArtifactResolutionResult Success (BuildReportSourceEntry? buildReport)
        {
            return new BuildReportArtifactResolutionResult(buildReport, null);
        }

        public static BuildReportArtifactResolutionResult Failure (ApplicationFailure failure)
        {
            ArgumentNullException.ThrowIfNull(failure);
            return new BuildReportArtifactResolutionResult(null, failure);
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
