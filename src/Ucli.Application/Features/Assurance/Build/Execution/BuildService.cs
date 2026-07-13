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
using MackySoft.Ucli.Contracts.Assurance;
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

    private static readonly IReadOnlyList<BuildResidualRiskOutput> EmptyResidualRisks =
        Array.Empty<BuildResidualRiskOutput>();

    private readonly IProjectContextResolver projectContextResolver;

    private readonly IBuildProfileFileReader profileFileReader;

    private readonly IEnvironmentVariableReader environmentVariableReader;

    private readonly IUnityExecutionModeDecisionService executionModeDecisionService;

    private readonly IUnityRequestExecutor unityRequestExecutor;

    private readonly IUnityStreamingRequestExecutor unityStreamingRequestExecutor;

    private readonly IRunIdGenerator runIdGenerator;

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
        IRunIdGenerator runIdGenerator,
        IBuildRunArtifactStore artifactStore,
        TimeProvider? timeProvider = null)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.profileFileReader = profileFileReader ?? throw new ArgumentNullException(nameof(profileFileReader));
        this.environmentVariableReader = environmentVariableReader ?? throw new ArgumentNullException(nameof(environmentVariableReader));
        this.executionModeDecisionService = executionModeDecisionService ?? throw new ArgumentNullException(nameof(executionModeDecisionService));
        this.unityRequestExecutor = unityRequestExecutor ?? throw new ArgumentNullException(nameof(unityRequestExecutor));
        this.unityStreamingRequestExecutor = unityStreamingRequestExecutor ?? throw new ArgumentNullException(nameof(unityStreamingRequestExecutor));
        this.runIdGenerator = runIdGenerator ?? throw new ArgumentNullException(nameof(runIdGenerator));
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
        if (profile.Inputs.Kind == BuildProfileInputsKind.Explicit
            && profile.Runner.Kind == BuildProfileRunnerKind.BuildPipeline
            && !IpcBuildOutputLayoutResolver.TryResolve(paths.RunnerOutputDirectory, profile.BuildTarget.StableName, out outputLayout))
        {
            return BuildExecutionResult.Failure(ExecutionError.InvalidArgument(
                $"BuildPipeline output layout could not be resolved for build target: {profile.BuildTarget.StableName}.",
                BuildErrorCodes.BuildInputsInvalid), project);
        }

        if (outputLayout != null)
        {
            var outputLayoutPrepareResult = artifactStore.PrepareBuildPipelineOutputLayout(
                paths,
                profile.BuildTarget.StableName,
                outputLayout);
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
                    BuildErrorCodes.BuildRunnerInvocationFailed.Value,
                    IpcExecuteDiagnosticSeverityNames.Error,
                    exception.Message,
                    ContractLiteralCodec.ToValue(BuildRunProgressPhase.RunnerInvocation),
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
                ContractLiteralCodec.ToValue(BuildRunProgressPhase.RunnerResult),
                ContractLiteralCodec.ToValue(profile.Runner.Kind),
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
            var terminalResult = ResolveTerminalBuildReportResult(GetTerminalResult(buildResponse));
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

            var completedOutput = output with
            {
                Reports = CreateReports(
                    accounting,
                    metadataWriteResult.Artifact!),
            };
            await EmitProgressAsync(
                    resolvedProgressSink,
                    BuildRunProgressEventNames.ArtifactsCompleted,
                    runId,
                    profile.Digest,
                    ContractLiteralCodec.ToValue(BuildRunProgressPhase.ArtifactAccounting),
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
        string profileDigest,
        CancellationToken cancellationToken)
    {
        return EmitProgressAsync(
            progressSink,
            BuildRunProgressEventNames.Started,
            runId,
            profileDigest,
            ContractLiteralCodec.ToValue(BuildRunProgressPhase.Started),
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
        if (profile.Runner.Kind == BuildProfileRunnerKind.BuildPipeline)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(profilePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
            ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
            ArgumentNullException.ThrowIfNull(projectFingerprint);
            return RunnerInvocationResolutionResult.Success(ResolvedRunnerInvocationInput.Empty);
        }

        if (!TryValidateRequiredPathVariable("ucli.build.profilePath", profilePath, out var pathError)
            || !TryValidateRequiredPathVariable("ucli.build.outputDir", outputDirectory, out pathError)
            || !TryValidateRequiredPathVariable("project.path", projectPath, out pathError))
        {
            return RunnerInvocationResolutionResult.Failure(pathError!);
        }

        ArgumentNullException.ThrowIfNull(projectFingerprint);

        var builtInVariables = CreateBuiltInVariableMap(
            profile,
            profilePath,
            runId,
            outputDirectory,
            projectPath,
            projectFingerprint);
        var arguments = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var argument in profile.Runner.Invocation.Arguments)
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

        var requestedEnv = profile.Runner.Invocation.Environment;
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
            ["ucli.build.profileDigest"] = profile.Digest,
            ["project.path"] = projectPath,
            ["project.fingerprint"] = projectFingerprint.ToString(),
            ["build.target"] = profile.BuildTarget.StableName,
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
            ContractLiteralCodec.ToValue(BuildRunProgressPhase.Completed),
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
        string code,
        string severity,
        string message,
        string phase,
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
        string profileDigest,
        string phase,
        string? runnerKind,
        string? runnerStatus,
        string? verdict,
        string[] reportRefs,
        string? errorCode,
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
        string profileDigest,
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
        string expectedProfileDigest,
        ICommandProgressSink progressSink,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedProfileDigest);
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
        string expectedProfileDigest,
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
        var inputKind = ContractLiteralCodec.ToValue(profile.Inputs.Kind);
        if (profile.Inputs.Kind == BuildProfileInputsKind.UnityBuildProfile)
        {
            return new UnityRequestPayload.BuildRun(
                RunId: runId,
                InputKind: inputKind,
                BuildTarget: null,
                UnityBuildTarget: null,
                SceneSource: null,
                ScenePaths: Array.Empty<string>(),
                Development: false,
                OutputPath: paths.RunnerOutputDirectory,
                OutputLayout: null,
                BuildReportPath: paths.BuildReportJsonPath,
                BuildLogPath: paths.BuildLogPath,
                AllowedEditorModes: profile.Policy.Runtime.AllowedEditorModes
                    .Select(ContractLiteralCodec.ToValue)
                    .ToArray(),
                ProjectMutationMode: ContractLiteralCodec.ToValue(profile.Policy.ProjectMutationMode),
                RunnerKind: ContractLiteralCodec.ToValue(profile.Runner.Kind))
            {
                ProfileDigest = profile.Digest,
                UnityBuildProfile = new IpcUnityBuildProfileInput(profile.Inputs.RequireUnityBuildProfilePath()),
            };
        }

        return new UnityRequestPayload.BuildRun(
            RunId: runId,
            InputKind: inputKind,
            BuildTarget: profile.BuildTarget.StableName,
            UnityBuildTarget: profile.BuildTarget.UnityBuildTargetLiteral,
            SceneSource: ContractLiteralCodec.ToValue(profile.Scenes.Source),
            ScenePaths: profile.Scenes.Paths,
            Development: profile.Options.Development,
            OutputPath: paths.RunnerOutputDirectory,
            OutputLayout: outputLayout,
            BuildReportPath: paths.BuildReportJsonPath,
            BuildLogPath: paths.BuildLogPath,
            AllowedEditorModes: profile.Policy.Runtime.AllowedEditorModes
                .Select(ContractLiteralCodec.ToValue)
                .ToArray(),
            ProjectMutationMode: ContractLiteralCodec.ToValue(profile.Policy.ProjectMutationMode),
            RunnerKind: ContractLiteralCodec.ToValue(profile.Runner.Kind))
        {
            ProfilePath = profile.Runner.Kind == BuildProfileRunnerKind.ExecuteMethod ? profilePath : null,
            ProfileDigest = profile.Digest,
            RunnerMethod = profile.Runner.Method,
            RunnerArguments = runnerInvocation.Arguments,
            RunnerEnvironmentVariables = runnerInvocation.EnvironmentVariables,
            RunnerEnvironmentSecrets = runnerInvocation.EnvironmentSecrets,
            RunnerEnvironmentVariableValues = runnerInvocation.EnvironmentVariableValues,
            RunnerEnvironmentSecretValues = runnerInvocation.EnvironmentSecretValues,
        };
    }

    private static string GetTerminalResult (IpcBuildRunResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return response.RunnerResult?.Status ?? response.Report!.Result;
    }

    private static OutputSourcesResolutionResult ResolveOutputSources (
        IpcBuildRunResponse response,
        IpcBuildOutputLayout? outputLayout,
        BuildProfileRunnerKind runnerKind,
        IpcBuildReportResult terminalResult)
    {
        if (runnerKind == BuildProfileRunnerKind.BuildPipeline)
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
        if (response.HasFailureStatus || response.Errors.Count != 0)
        {
            var firstError = response.Errors.FirstOrDefault();
            var failure = ApplicationFailure.FromCode(
                firstError?.Code,
                firstError?.Message ?? $"Unity build IPC failed with status '{response.FailureStatus}'.",
                firstError?.OpId);
            return BuildResponseResolutionResult.Failure(failure, TryReadErrorPayload(response));
        }

        var runnerResultPayloadShapeFailure = ValidateExecuteMethodRunnerResultPayloadShape(
            response.Payload,
            expectedProfile.Runner.Kind);
        if (runnerResultPayloadShapeFailure != null)
        {
            return BuildResponseResolutionResult.Failure(runnerResultPayloadShapeFailure);
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

        if (!ContractLiteralCodec.TryParse<BuildProfileInputsKind>(response.Input.InputKind, out var inputKind))
        {
            return ApplicationFailure.InternalError($"Unity build response contains unsupported input kind: {response.Input.InputKind}.");
        }

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

        if (expectedProfile.Inputs.Kind == BuildProfileInputsKind.Explicit)
        {
            var explicitValidationFailure = ValidateExplicitResponseInputs(response, expectedProfile);
            if (explicitValidationFailure != null)
            {
                return explicitValidationFailure;
            }
        }
        else
        {
            var unityBuildProfileValidationFailure = ValidateUnityBuildProfileResponseInputs(response, expectedProfile);
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

        if (!ContractLiteralCodec.TryParse<BuildProfileSceneSource>(response.Input.SceneSource, out var sceneSource))
        {
            return ApplicationFailure.InternalError($"Unity build response contains unsupported scene source: {response.Input.SceneSource}.");
        }

        if (expectedProfile.Inputs.Kind == BuildProfileInputsKind.Explicit
            && sceneSource != expectedProfile.Scenes.Source)
        {
            var expectedSceneSource = ContractLiteralCodec.ToValue(expectedProfile.Scenes.Source);
            return ApplicationFailure.InternalError(
                $"Unity build response scene source mismatch. Requested={expectedSceneSource}, Actual={response.Input.SceneSource}.");
        }

        if (expectedProfile.Inputs.Kind == BuildProfileInputsKind.UnityBuildProfile
            && sceneSource != BuildProfileSceneSource.UnityBuildProfile)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response scene source mismatch. Requested={ContractLiteralCodec.ToValue(BuildProfileSceneSource.UnityBuildProfile)}, Actual={response.Input.SceneSource}.");
        }

        if (expectedProfile.Inputs.Kind == BuildProfileInputsKind.Explicit
            && !HasExpectedDevelopmentBuildOption(response.Input.BuildOptions, expectedProfile.Options.Development))
        {
            return ApplicationFailure.InternalError(
                $"Unity build response build options mismatch. RequestedDevelopment={expectedProfile.Options.Development}, Actual={response.Input.BuildOptions}.");
        }

        IpcBuildReportResult? reportResult = null;
        if (expectedProfile.Runner.Kind == BuildProfileRunnerKind.BuildPipeline)
        {
            if (response.Report == null)
            {
                return ApplicationFailure.FromCode(
                    BuildErrorCodes.BuildReportMissing,
                    "Unity build response BuildReport is missing for buildPipeline runner.");
            }

            if (!ContractLiteralCodec.TryParse<IpcBuildReportResult>(response.Report.Result, out var parsedReportResult))
            {
                return ApplicationFailure.InternalError($"Unity build response contains unsupported report result: {response.Report.Result}.");
            }

            if (!IsTerminalBuildReportResult(parsedReportResult))
            {
                return ApplicationFailure.InternalError($"Unity build response contains non-terminal report result: {response.Report.Result}.");
            }

            if (!string.Equals(response.Report.UnityBuildTarget, response.Input.UnityBuildTarget, StringComparison.Ordinal))
            {
                return ApplicationFailure.InternalError(
                    $"Unity BuildReport BuildTarget mismatch. Input={response.Input.UnityBuildTarget}, Report={response.Report.UnityBuildTarget}.");
            }

            reportResult = parsedReportResult;
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

        var terminalResult = ResolveTerminalBuildReportResult(GetTerminalResult(response));

        if (!ContractLiteralCodec.TryParse<IpcBuildLogCompletionReason>(response.Logs.CompletionReason, out var completionReason))
        {
            return ApplicationFailure.InternalError($"Unity build response contains unsupported log completionReason: {response.Logs.CompletionReason}.");
        }

        var expectedCompletionReason = IpcBuildLogCompletionReasonResolver.FromReportResult(terminalResult);
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

        if (expectedProfile.Inputs.Kind == BuildProfileInputsKind.Explicit
            && expectedProfile.Scenes.Source == BuildProfileSceneSource.Explicit
            && !response.Input.Scenes.SequenceEqual(expectedProfile.Scenes.Paths, StringComparer.Ordinal))
        {
            return ApplicationFailure.InternalError("Unity build response resolved scenes do not match the requested explicit build scenes.");
        }

        return ValidateProjectMutationAudit(response.ProjectMutation, expectedProfile.Policy.ProjectMutationMode);
    }

    private static ApplicationFailure? ValidateResponseOutputLayout (
        IpcBuildOutputLayout? outputLayout,
        string buildTarget,
        string expectedOutputDirectory,
        BuildProfileInputsKind inputKind,
        BuildProfileRunnerKind runnerKind)
    {
        if (runnerKind == BuildProfileRunnerKind.ExecuteMethod)
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

        if (!IpcBuildOutputLayoutResolver.TryResolve(expectedOutputDirectory, buildTarget, out var expectedOutputLayout))
        {
            return ApplicationFailure.InternalError($"Unity build response buildTarget does not have a supported output layout: {buildTarget}.");
        }

        if (IsExpectedOutputLayout(outputLayout, expectedOutputLayout!))
        {
            return null;
        }

        if (inputKind == BuildProfileInputsKind.UnityBuildProfile
            && ContractLiteralCodec.Matches(buildTarget, BuildTargetStableName.Android)
            && IpcBuildOutputLayoutResolver.TryResolve(
                expectedOutputDirectory,
                buildTarget,
                true,
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
        return ContractLiteralCodec.TryParse<IpcBuildOutputLayoutShape>(actual.Shape, out var actualShape)
            && ContractLiteralCodec.TryParse<IpcBuildOutputLayoutShape>(expected.Shape, out var expectedShape)
            && actualShape == expectedShape
            && string.Equals(actual.LocationPathName, expected.LocationPathName, StringComparison.Ordinal);
    }

    private static ApplicationFailure? ValidateResponseInputBuildTarget (IpcBuildInputProbe input)
    {
        if (!ContractLiteralCodec.TryParse<BuildTargetStableName>(input.BuildTarget, out var expectedBuildTarget)
            || !BuildTargetStableNameUnityBuildTargetResolver.TryResolve(expectedBuildTarget, out var expectedUnityBuildTarget))
        {
            return ApplicationFailure.InternalError($"Unity build response contains unsupported buildTarget: {input.BuildTarget}.");
        }

        if (!BuildTargetStableNameUnityBuildTargetResolver.TryResolveStableName(input.UnityBuildTarget, out var actualBuildTarget)
            || actualBuildTarget != expectedBuildTarget)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response buildTarget and Unity BuildTarget mismatch. BuildTarget={input.BuildTarget}, ExpectedUnityBuildTarget={expectedUnityBuildTarget}, ActualUnityBuildTarget={input.UnityBuildTarget}.");
        }

        return null;
    }

    private static ApplicationFailure? ValidateExplicitResponseInputs (
        IpcBuildRunResponse response,
        ResolvedBuildProfile expectedProfile)
    {
        if (response.UnityBuildProfile != null)
        {
            return ApplicationFailure.InternalError("Unity build response unityBuildProfile input must be omitted for explicit build inputs.");
        }

        if (!ContractLiteralCodec.Matches(response.Input.BuildTarget, expectedProfile.BuildTarget.StableNameValue))
        {
            return ApplicationFailure.InternalError(
                $"Unity build response buildTarget mismatch. Requested={expectedProfile.BuildTarget.StableName}, Actual={response.Input.BuildTarget}.");
        }

        if (!BuildTargetStableNameUnityBuildTargetResolver.TryResolveStableName(response.Input.UnityBuildTarget, out var actualBuildTarget)
            || actualBuildTarget != expectedProfile.BuildTarget.StableNameValue)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response Unity BuildTarget mismatch. Requested={expectedProfile.BuildTarget.UnityBuildTargetLiteral}, Actual={response.Input.UnityBuildTarget}.");
        }

        return null;
    }

    private static ApplicationFailure? ValidateUnityBuildProfileResponseInputs (
        IpcBuildRunResponse response,
        ResolvedBuildProfile expectedProfile)
    {
        if (response.UnityBuildProfile == null)
        {
            return ApplicationFailure.InternalError("Unity build response unityBuildProfile input is missing.");
        }

        if (!string.Equals(response.UnityBuildProfile.Path, expectedProfile.Inputs.RequireUnityBuildProfilePath(), StringComparison.Ordinal))
        {
            return ApplicationFailure.InternalError(
                $"Unity build response unityBuildProfile path mismatch. Requested={expectedProfile.Inputs.RequireUnityBuildProfilePath()}, Actual={response.UnityBuildProfile.Path}.");
        }

        if (!Sha256LowerHex.IsLowerHexDigest(response.UnityBuildProfile.Digest))
        {
            return ApplicationFailure.InternalError("Unity build response unityBuildProfile digest must be lowercase SHA-256 hex.");
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
        if (!ContractLiteralCodec.TryParse<IpcBuildDirtyStateCoverage>(dirtyState.Coverage, out _))
        {
            return ApplicationFailure.InternalError(
                $"Unity build response unityBuildProfile applyAudit.dirtyStateAfter contains unsupported coverage: {dirtyState.Coverage}.");
        }

        if (dirtyState.Items == null)
        {
            return ApplicationFailure.InternalError("Unity build response unityBuildProfile applyAudit.dirtyStateAfter items must be present.");
        }

        string? previousPath = null;
        for (var i = 0; i < dirtyState.Items.Count; i++)
        {
            var item = dirtyState.Items[i];
            if (item == null)
            {
                return ApplicationFailure.InternalError(
                    $"Unity build response unityBuildProfile applyAudit.dirtyStateAfter item at index {i} is missing.");
            }

            if (!ContractLiteralCodec.TryParse<IpcBuildDirtyStateItemKind>(item.Kind, out _))
            {
                return ApplicationFailure.InternalError(
                    $"Unity build response unityBuildProfile applyAudit.dirtyStateAfter item at index {i} contains unsupported kind: {item.Kind}.");
            }

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
        BuildProfileRunnerKind expectedRunnerKind,
        IpcBuildReportArtifact? report,
        IpcBuildReportResult? reportResult)
    {
        if (runnerResult == null)
        {
            return expectedRunnerKind == BuildProfileRunnerKind.ExecuteMethod
                ? ApplicationFailure.FromCode(
                    BuildErrorCodes.BuildRunnerResultMissing,
                    "Unity build response runnerResult is missing for executeMethod runner.")
                : null;
        }

        var expectedSource = expectedRunnerKind == BuildProfileRunnerKind.ExecuteMethod
            ? IpcBuildRunnerResultSource.UcliBuildRunnerResult
            : IpcBuildRunnerResultSource.BuildPipelineBuildReport;
        if (!ContractLiteralCodec.TryParse<IpcBuildRunnerResultSource>(runnerResult.Source, out var source)
            || source != expectedSource)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response runnerResult source is invalid for {ContractLiteralCodec.ToValue(expectedRunnerKind)} runner: {runnerResult.Source}.");
        }

        if (!ContractLiteralCodec.TryParse<IpcBuildReportResult>(runnerResult.Status, out var status)
            || status == IpcBuildReportResult.Unknown)
        {
            return ApplicationFailure.FromCode(
                BuildErrorCodes.BuildRunnerResultInvalid,
                $"Unity build response runnerResult status is invalid: {runnerResult.Status}.");
        }

        if (runnerResult.DurationMilliseconds < 0
            || runnerResult.ErrorCount < 0
            || runnerResult.WarningCount < 0)
        {
            return ApplicationFailure.FromCode(
                BuildErrorCodes.BuildRunnerResultInvalid,
                "Unity build response runnerResult summary is invalid.");
        }

        if (!HasValidRunnerDiagnostics(runnerResult.Diagnostics))
        {
            return ApplicationFailure.FromCode(
                BuildErrorCodes.BuildRunnerResultInvalid,
                "Unity build response runnerResult diagnostics are invalid.");
        }

        if (expectedRunnerKind == BuildProfileRunnerKind.BuildPipeline)
        {
            if (report == null || reportResult == null)
            {
                return ApplicationFailure.FromCode(
                    BuildErrorCodes.BuildReportMissing,
                    "Unity build response BuildReport is missing for buildPipeline runner.");
            }

            if (status != reportResult.Value)
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

    private static ApplicationFailure? ValidateExecuteMethodRunnerResultPayloadShape (
        JsonElement payload,
        BuildProfileRunnerKind expectedRunnerKind)
    {
        if (expectedRunnerKind != BuildProfileRunnerKind.ExecuteMethod)
        {
            return null;
        }

        if (JsonObjectPropertyReader.TryFindDuplicatePropertyIgnoreCase(payload, "$", out var duplicatePropertyPath)
            && IsRunnerResultPropertyPath(duplicatePropertyPath))
        {
            return CreateBuildRunnerResultInvalidFailure(
                $"Unity build response runnerResult contains a duplicated property: {duplicatePropertyPath}.");
        }

        if (!JsonObjectPropertyReader.TryGetPropertyIgnoreCase(payload, "runnerResult", out var runnerResult)
            || runnerResult.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (runnerResult.ValueKind != JsonValueKind.Object)
        {
            return ApplicationFailure.FromCode(
                BuildErrorCodes.BuildRunnerResultInvalid,
                "Unity build response runnerResult must be an object for executeMethod runner.");
        }

        if (JsonObjectPropertyReader.TryFindDuplicatePropertyIgnoreCase(runnerResult, "$.runnerResult", out duplicatePropertyPath))
        {
            return CreateBuildRunnerResultInvalidFailure(
                $"Unity build response runnerResult contains a duplicated property: {duplicatePropertyPath}.");
        }

        foreach (var property in runnerResult.EnumerateObject())
        {
            if (!IsKnownRunnerResultProperty(property.Name))
            {
                return ApplicationFailure.FromCode(
                    BuildErrorCodes.BuildRunnerResultInvalid,
                    $"Unity build response runnerResult contains an unsupported property: {property.Name}.");
            }
        }

        var propertyFailure = ValidateRequiredStringProperty(runnerResult, "source", "runnerResult.source")
                              ?? ValidateRequiredStringProperty(runnerResult, "status", "runnerResult.status")
                              ?? ValidateRequiredInt64Property(runnerResult, "durationMilliseconds", "runnerResult.durationMilliseconds")
                              ?? ValidateRequiredInt32Property(runnerResult, "errorCount", "runnerResult.errorCount")
                              ?? ValidateRequiredInt32Property(runnerResult, "warningCount", "runnerResult.warningCount")
                              ?? ValidateRunnerResultDiagnosticsPayloadShape(runnerResult)
                              ?? ValidateRunnerResultOutputsPayloadShape(runnerResult)
                              ?? ValidateRunnerResultBuildReportPayloadShape(runnerResult);
        return propertyFailure;
    }

    private static bool IsRunnerResultPropertyPath (string propertyPath)
    {
        return propertyPath.Equals("$.runnerResult", StringComparison.OrdinalIgnoreCase)
               || propertyPath.StartsWith("$.runnerResult.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKnownRunnerResultProperty (string propertyName)
    {
        return propertyName.Equals("source", StringComparison.OrdinalIgnoreCase)
               || propertyName.Equals("status", StringComparison.OrdinalIgnoreCase)
               || propertyName.Equals("durationMilliseconds", StringComparison.OrdinalIgnoreCase)
               || propertyName.Equals("errorCount", StringComparison.OrdinalIgnoreCase)
               || propertyName.Equals("warningCount", StringComparison.OrdinalIgnoreCase)
               || propertyName.Equals("diagnostics", StringComparison.OrdinalIgnoreCase)
               || propertyName.Equals("outputs", StringComparison.OrdinalIgnoreCase)
               || propertyName.Equals("buildReport", StringComparison.OrdinalIgnoreCase);
    }

    private static ApplicationFailure? ValidateRunnerResultDiagnosticsPayloadShape (JsonElement runnerResult)
    {
        if (!JsonObjectPropertyReader.TryGetPropertyIgnoreCase(runnerResult, "diagnostics", out var diagnostics)
            || diagnostics.ValueKind != JsonValueKind.Array)
        {
            return CreateBuildRunnerResultInvalidFailure(
                "Unity build response runnerResult diagnostics must be present as an array.");
        }

        foreach (var diagnostic in diagnostics.EnumerateArray())
        {
            if (diagnostic.ValueKind != JsonValueKind.Object)
            {
                return CreateBuildRunnerResultInvalidFailure(
                    "Unity build response runnerResult diagnostics entries must be objects.");
            }

            foreach (var property in diagnostic.EnumerateObject())
            {
                if (!IsKnownRunnerDiagnosticProperty(property.Name))
                {
                    return CreateBuildRunnerResultInvalidFailure(
                        $"Unity build response runnerResult diagnostics contain an unsupported property: {property.Name}.");
                }
            }

            var propertyFailure = ValidateRequiredStringProperty(diagnostic, "code", "runnerResult.diagnostics[].code")
                                  ?? ValidateRequiredStringProperty(diagnostic, "severity", "runnerResult.diagnostics[].severity")
                                  ?? ValidateRequiredStringProperty(diagnostic, "message", "runnerResult.diagnostics[].message");
            if (propertyFailure != null)
            {
                return propertyFailure;
            }
        }

        return null;
    }

    private static bool IsKnownRunnerDiagnosticProperty (string propertyName)
    {
        return propertyName.Equals("code", StringComparison.OrdinalIgnoreCase)
               || propertyName.Equals("severity", StringComparison.OrdinalIgnoreCase)
               || propertyName.Equals("message", StringComparison.OrdinalIgnoreCase);
    }

    private static ApplicationFailure? ValidateRunnerResultOutputsPayloadShape (JsonElement runnerResult)
    {
        if (!JsonObjectPropertyReader.TryGetPropertyIgnoreCase(runnerResult, "outputs", out var outputs)
            || outputs.ValueKind != JsonValueKind.Array)
        {
            return CreateBuildRunnerResultInvalidFailure(
                "Unity build response runnerResult outputs must be present as an array.");
        }

        foreach (var output in outputs.EnumerateArray())
        {
            if (output.ValueKind != JsonValueKind.String)
            {
                return CreateBuildRunnerResultInvalidFailure(
                    "Unity build response runnerResult outputs entries must be strings.");
            }
        }

        return null;
    }

    private static ApplicationFailure? ValidateRunnerResultBuildReportPayloadShape (JsonElement runnerResult)
    {
        if (!JsonObjectPropertyReader.TryGetPropertyIgnoreCase(runnerResult, "buildReport", out var buildReport)
            || buildReport.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (buildReport.ValueKind != JsonValueKind.Object)
        {
            return CreateBuildRunnerResultInvalidFailure(
                "Unity build response runnerResult buildReport must be an object.");
        }

        foreach (var property in buildReport.EnumerateObject())
        {
            if (!property.Name.Equals("path", StringComparison.OrdinalIgnoreCase))
            {
                return CreateBuildRunnerResultInvalidFailure(
                    $"Unity build response runnerResult buildReport contains an unsupported property: {property.Name}.");
            }
        }

        return ValidateRequiredStringProperty(buildReport, "path", "runnerResult.buildReport.path");
    }

    private static ApplicationFailure? ValidateRequiredStringProperty (
        JsonElement owner,
        string propertyName,
        string propertyPath)
    {
        return !JsonObjectPropertyReader.TryGetPropertyIgnoreCase(owner, propertyName, out var property)
               || property.ValueKind != JsonValueKind.String
            ? CreateBuildRunnerResultInvalidFailure(
                $"Unity build response {propertyPath} must be present as a string.")
            : null;
    }

    private static ApplicationFailure? ValidateRequiredInt32Property (
        JsonElement owner,
        string propertyName,
        string propertyPath)
    {
        return !JsonObjectPropertyReader.TryGetPropertyIgnoreCase(owner, propertyName, out var property)
               || property.ValueKind != JsonValueKind.Number
               || !property.TryGetInt32(out _)
            ? CreateBuildRunnerResultInvalidFailure(
                $"Unity build response {propertyPath} must be present as an integer.")
            : null;
    }

    private static ApplicationFailure? ValidateRequiredInt64Property (
        JsonElement owner,
        string propertyName,
        string propertyPath)
    {
        return !JsonObjectPropertyReader.TryGetPropertyIgnoreCase(owner, propertyName, out var property)
               || property.ValueKind != JsonValueKind.Number
               || !property.TryGetInt64(out _)
            ? CreateBuildRunnerResultInvalidFailure(
                $"Unity build response {propertyPath} must be present as an integer.")
            : null;
    }

    private static ApplicationFailure CreateBuildRunnerResultInvalidFailure (string message)
    {
        return ApplicationFailure.FromCode(
            BuildErrorCodes.BuildRunnerResultInvalid,
            message);
    }

    private static bool HasValidRunnerDiagnostics (IReadOnlyList<IpcBuildRunnerDiagnostic> diagnostics)
    {
        if (diagnostics == null)
        {
            return false;
        }

        for (var i = 0; i < diagnostics.Count; i++)
        {
            var diagnostic = diagnostics[i];
            if (diagnostic == null
                || string.IsNullOrWhiteSpace(diagnostic.Code)
                || !IsKnownDiagnosticSeverity(diagnostic.Severity)
                || string.IsNullOrWhiteSpace(diagnostic.Message))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsKnownDiagnosticSeverity (string severity)
    {
        return severity is IpcExecuteDiagnosticSeverityNames.Info
            or IpcExecuteDiagnosticSeverityNames.Warning
            or IpcExecuteDiagnosticSeverityNames.Error;
    }

    private static ApplicationFailure? ValidateProjectMutationAudit (
        IpcBuildProjectMutationAudit projectMutation,
        BuildProfileProjectMutationMode expectedMode)
    {
        if (projectMutation == null)
        {
            return ApplicationFailure.InternalError("Unity build response projectMutation audit is missing.");
        }

        var expectedModeLiteral = ContractLiteralCodec.ToValue(expectedMode);
        if (!ContractLiteralCodec.TryParse<BuildProfileProjectMutationMode>(projectMutation.Mode, out var mode))
        {
            return ApplicationFailure.InternalError($"Unity build response contains unsupported projectMutation mode: {projectMutation.Mode}.");
        }

        if (mode != expectedMode)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response projectMutation mode mismatch. Requested={expectedModeLiteral}, Actual={projectMutation.Mode}.");
        }

        if (!ContractLiteralCodec.TryParse<IpcBuildProjectMutationAuditCoverage>(projectMutation.Coverage, out _))
        {
            return ApplicationFailure.InternalError($"Unity build response contains unsupported projectMutation coverage: {projectMutation.Coverage}.");
        }

        if (!Sha256LowerHex.IsLowerHexDigest(projectMutation.BeforeDigest))
        {
            return ApplicationFailure.InternalError("Unity build response projectMutation beforeDigest must be lowercase SHA-256 hex.");
        }

        if (!Sha256LowerHex.IsLowerHexDigest(projectMutation.AfterDigest))
        {
            return ApplicationFailure.InternalError("Unity build response projectMutation afterDigest must be lowercase SHA-256 hex.");
        }

        if (projectMutation.Items == null)
        {
            return ApplicationFailure.InternalError("Unity build response projectMutation items must be present.");
        }

        var hasItems = projectMutation.Items.Count != 0;
        if (projectMutation.Mutated != hasItems)
        {
            return ApplicationFailure.InternalError("Unity build response projectMutation mutated flag must match the item set.");
        }

        if (!projectMutation.Mutated
            && !string.Equals(projectMutation.BeforeDigest, projectMutation.AfterDigest, StringComparison.Ordinal))
        {
            return ApplicationFailure.InternalError("Unity build response projectMutation digests must match when no items changed.");
        }

        if (projectMutation.Mutated
            && string.Equals(projectMutation.BeforeDigest, projectMutation.AfterDigest, StringComparison.Ordinal))
        {
            return ApplicationFailure.InternalError("Unity build response projectMutation digests must differ when items changed.");
        }

        string? previousPath = null;
        for (var i = 0; i < projectMutation.Items.Count; i++)
        {
            var item = projectMutation.Items[i];
            var validationFailure = ValidateProjectMutationAuditItem(item, previousPath, i);
            if (validationFailure != null)
            {
                return validationFailure;
            }

            previousPath = item.Path;
        }

        return null;
    }

    private static IpcBuildReportResult ResolveTerminalBuildReportResult (string result)
    {
        if (!ContractLiteralCodec.TryParse<IpcBuildReportResult>(result, out var reportResult)
            || !IsTerminalBuildReportResult(reportResult))
        {
            throw new InvalidOperationException($"Build report result is not terminal: {result}");
        }

        return reportResult;
    }

    private static bool CanWriteEmptyOutputManifest (IpcBuildReportResult reportResult)
    {
        return reportResult is IpcBuildReportResult.Failed or IpcBuildReportResult.Canceled;
    }

    private static bool IsTerminalBuildReportResult (IpcBuildReportResult reportResult)
    {
        return reportResult is IpcBuildReportResult.Succeeded or IpcBuildReportResult.Failed or IpcBuildReportResult.Canceled;
    }

    private static ApplicationFailure? ValidateProjectMutationAuditItem (
        IpcBuildProjectMutationAuditItem item,
        string? previousPath,
        int index)
    {
        if (item == null)
        {
            return ApplicationFailure.InternalError($"Unity build response projectMutation item at index {index} is missing.");
        }

        if (!RelativePathContract.IsNormalized(item.Path)
            || !IsAuditedProjectMutationPath(item.Path))
        {
            return ApplicationFailure.InternalError(
                $"Unity build response projectMutation item at index {index} has an invalid audited project path: {item.Path}.");
        }

        if (previousPath != null && string.CompareOrdinal(previousPath, item.Path) >= 0)
        {
            return ApplicationFailure.InternalError("Unity build response projectMutation items must be ordered by unique project-relative path.");
        }

        if (!ContractLiteralCodec.TryParse<IpcBuildProjectMutationChangeKind>(item.ChangeKind, out var changeKind))
        {
            return ApplicationFailure.InternalError(
                $"Unity build response projectMutation item at index {index} contains unsupported changeKind: {item.ChangeKind}.");
        }

        return changeKind switch
        {
            IpcBuildProjectMutationChangeKind.Added => ValidateProjectMutationAddedItem(item, index),
            IpcBuildProjectMutationChangeKind.Modified => ValidateProjectMutationModifiedItem(item, index),
            IpcBuildProjectMutationChangeKind.Deleted => ValidateProjectMutationDeletedItem(item, index),
            _ => ApplicationFailure.InternalError(
                $"Unity build response projectMutation item at index {index} contains unsupported changeKind: {item.ChangeKind}."),
        };
    }

    private static ApplicationFailure? ValidateProjectMutationAddedItem (
        IpcBuildProjectMutationAuditItem item,
        int index)
    {
        if (item.BeforeSha256 != null)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response projectMutation added item at index {index} must not contain beforeSha256.");
        }

        return Sha256LowerHex.IsLowerHexDigest(item.AfterSha256)
            ? null
            : ApplicationFailure.InternalError(
                $"Unity build response projectMutation added item at index {index} must contain afterSha256.");
    }

    private static ApplicationFailure? ValidateProjectMutationModifiedItem (
        IpcBuildProjectMutationAuditItem item,
        int index)
    {
        if (!Sha256LowerHex.IsLowerHexDigest(item.BeforeSha256)
            || !Sha256LowerHex.IsLowerHexDigest(item.AfterSha256))
        {
            return ApplicationFailure.InternalError(
                $"Unity build response projectMutation modified item at index {index} must contain beforeSha256 and afterSha256.");
        }

        return !string.Equals(item.BeforeSha256, item.AfterSha256, StringComparison.Ordinal)
            ? null
            : ApplicationFailure.InternalError(
                $"Unity build response projectMutation modified item at index {index} must change digest.");
    }

    private static ApplicationFailure? ValidateProjectMutationDeletedItem (
        IpcBuildProjectMutationAuditItem item,
        int index)
    {
        if (item.AfterSha256 != null)
        {
            return ApplicationFailure.InternalError(
                $"Unity build response projectMutation deleted item at index {index} must not contain afterSha256.");
        }

        return Sha256LowerHex.IsLowerHexDigest(item.BeforeSha256)
            ? null
            : ApplicationFailure.InternalError(
                $"Unity build response projectMutation deleted item at index {index} must contain beforeSha256.");
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
        var reportRef = accounting.BuildReport == null ? null : BuildReportRefs.BuildReport;
        var summary = profile.Runner.Kind == BuildProfileRunnerKind.BuildPipeline
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
            ReportRef: BuildReportRefs.BuildLog,
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
            RunId: runId,
            Profile: new BuildProfileOutput(profilePath, profile.Digest),
            Inputs: inputs,
            Runner: new BuildRunnerOutput(
                Kind: ContractLiteralCodec.ToValue(profile.Runner.Kind),
                Method: profile.Runner.Method,
                Invocation: new BuildRunnerInvocationOutput(
                    Arguments: runnerInvocation.Arguments,
                    Environment: new BuildRunnerInvocationEnvironmentOutput(
                        Variables: runnerInvocation.EnvironmentVariables,
                        Secrets: runnerInvocation.EnvironmentSecrets))),
            RunnerResult: CreateRunnerResultOutput(profile, response),
            Output: new BuildArtifactOutput(
                ManifestRef: BuildReportRefs.BuildOutputManifest,
                ManifestDigest: accounting.OutputManifest.ManifestDigest,
                EntryCount: accounting.OutputManifest.EntryCount,
                FileCount: accounting.OutputManifest.FileCount,
                TotalBytes: accounting.OutputManifest.TotalBytes),
            Generations: generations,
            Summary: summary,
            Logs: logs);
        var residualRisks = CreateResidualRisks(profile.Policy.ProjectMutationMode, response.ProjectMutation);
        var claims = CreateClaims(response, build);
        return new BuildExecutionOutput(
            Verdict: RecalculateVerdict(claims, residualRisks),
            Project: project,
            Build: build,
            Verifiers:
            [
                new BuildVerifierOutput(
                    Id: BuildReportRefs.Build,
                    Kind: BuildReportRefs.Build,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: claims.Where(static claim => claim.Required).Select(static claim => claim.Id).ToArray(),
                    Effects: CreateVerifierEffects(build.Runner.Kind, accounting.BuildReport != null),
                    ReportRef: BuildReportRefs.Build),
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
            Path: unityBuildProfile.Path,
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
            Path: unityBuildProfile.Path,
            Digest: unityBuildProfile.Digest,
            ApplyAudit: unityBuildProfile.ApplyAudit);
    }

    private static IReadOnlyList<string> CreateVerifierEffects (
        string runnerKind,
        bool hasBuildReport)
    {
        var effects = new List<string>
        {
            ContractLiteralCodec.ToValue(BuildEffect.UnityLifecycleRead),
            ContractLiteralCodec.ToValue(BuildEffect.UnityLogWindowRead),
            ContractLiteralCodec.ToValue(BuildEffect.UcliArtifactWrite),
            ContractLiteralCodec.ToValue(BuildEffect.OutputManifestWrite),
            ContractLiteralCodec.ToValue(BuildEffect.GenerationSnapshot),
            ContractLiteralCodec.ToValue(BuildEffect.ProjectMutationAudit),
        };
        if (hasBuildReport)
        {
            effects.Insert(1, ContractLiteralCodec.ToValue(BuildEffect.UnityBuildReportRead));
        }

        if (ContractLiteralCodec.Matches(runnerKind, BuildProfileRunnerKind.ExecuteMethod))
        {
            effects.Insert(1, ContractLiteralCodec.ToValue(BuildEffect.UnityExecuteMethod));
        }
        else
        {
            effects.Insert(1, ContractLiteralCodec.ToValue(BuildEffect.UnityBuildPipeline));
        }

        return effects;
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

        var source = profile.Runner.Kind == BuildProfileRunnerKind.ExecuteMethod
            ? ContractLiteralCodec.ToValue(IpcBuildRunnerResultSource.UcliBuildRunnerResult)
            : ContractLiteralCodec.ToValue(IpcBuildRunnerResultSource.BuildPipelineBuildReport);
        return new BuildRunnerResultOutput(
            Source: source,
            Status: GetTerminalResult(response));
    }

    private static IReadOnlyDictionary<string, BuildReportOutput> CreateReports (
        BuildRunArtifactAccountingResult accounting,
        BuildArtifactRef? buildArtifact)
    {
        var reports = new Dictionary<string, BuildReportOutput>(StringComparer.Ordinal)
        {
            [BuildReportRefs.Build] = new BuildReportOutput(buildArtifact?.Path ?? "build.json", buildArtifact?.Digest),
            [BuildReportRefs.BuildOutputManifest] = new BuildReportOutput(accounting.BuildOutputManifest.Path, accounting.BuildOutputManifest.Digest),
            [BuildReportRefs.BuildLog] = new BuildReportOutput(accounting.BuildLog.Path, accounting.BuildLog.Digest),
        };
        if (accounting.BuildReport != null)
        {
            reports.Add(BuildReportRefs.BuildReport, new BuildReportOutput(accounting.BuildReport.Path, accounting.BuildReport.Digest));
        }

        return reports;
    }

    private static string[] CreateReportRefs (IReadOnlyDictionary<string, BuildReportOutput> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);
        var refs = new List<string>(capacity: 4);
        AddReportRefIfPresent(refs, reports, BuildReportRefs.Build);
        AddReportRefIfPresent(refs, reports, BuildReportRefs.BuildReport);
        AddReportRefIfPresent(refs, reports, BuildReportRefs.BuildOutputManifest);
        AddReportRefIfPresent(refs, reports, BuildReportRefs.BuildLog);
        return refs.ToArray();
    }

    private static void AddReportRefIfPresent (
        List<string> refs,
        IReadOnlyDictionary<string, BuildReportOutput> reports,
        string reportRef)
    {
        if (reports.ContainsKey(reportRef))
        {
            refs.Add(reportRef);
        }
    }

    private static BuildRunMetadataDocument CreateMetadataDocument (
        BuildExecutionOutput output,
        IpcBuildRunResponse response,
        ResolvedBuildProfile profile,
        IpcBuildOutputLayout? outputLayout,
        BuildRunArtifactAccountingResult accounting)
    {
        var invocationEnv = output.Build.Runner.Invocation.Environment;
        return new BuildRunMetadataDocument(
            SchemaVersion: BuildMetadataSchemaVersion,
            RunId: output.Build.RunId,
            Profile: SerializeMetadataElement(output.Build.Profile),
            Inputs: SerializeMetadataElement(CreateInputMetadata(output.Build.Inputs, response.UnityBuildProfile)),
            Runner: SerializeMetadataElement(new BuildRunRunnerMetadata(
                Kind: ContractLiteralCodec.ToValue(profile.Runner.Kind),
                Method: profile.Runner.Method,
                Invocation: new BuildRunRunnerInvocationMetadata(
                    Arguments: output.Build.Runner.Invocation.Arguments,
                    Environment: new BuildRunRunnerInvocationEnvironmentMetadata(
                        Variables: invocationEnv.Variables,
                        Secrets: invocationEnv.Secrets)),
                OutputLayout: outputLayout)),
            RunnerResult: SerializeMetadataElement(CreateRunnerResultMetadata(output, response, accounting.BuildReport != null)),
            Lifecycle: SerializeMetadataElement(new BuildRunLifecycleMetadata(
                Before: response.LifecycleBefore,
                After: response.LifecycleAfter)),
            Generations: SerializeMetadataElement(output.Build.Generations),
            Summary: SerializeMetadataElement(output.Build.Summary),
            Logs: SerializeMetadataElement(output.Build.Logs),
            ProjectMutation: SerializeMetadataElement(response.ProjectMutation));
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
                buildReportRef = hasBuildReport ? BuildReportRefs.BuildReport : null,
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
            buildReportRef = hasBuildReport ? BuildReportRefs.BuildReport : null,
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
        var reportResult = ContractLiteralCodec.TryParse<IpcBuildReportResult>(build.Summary.Result, out var parsedResult)
            ? parsedResult
            : IpcBuildReportResult.Unknown;
        var succeeded = reportResult == IpcBuildReportResult.Succeeded;
        var knownTerminalResult = reportResult is IpcBuildReportResult.Succeeded or IpcBuildReportResult.Failed or IpcBuildReportResult.Canceled;
        var isExecuteMethod = ContractLiteralCodec.Matches(build.Runner.Kind, BuildProfileRunnerKind.ExecuteMethod);
        var hasBuildReport = build.Summary.ReportRef != null;
        var terminalEvidenceKind = ContractLiteralCodec.ToValue(
            isExecuteMethod ? BuildEffect.UnityExecuteMethod : BuildEffect.UnityBuildPipeline);
        var terminalEvidenceRef = isExecuteMethod ? BuildReportRefs.Build : BuildReportRefs.BuildReport;

        var claims = new List<BuildClaimOutput>
        {
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
                IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(response.LifecycleBefore.State.LifecycleState)
                    ? BuildClaimStatus.Passed
                    : BuildClaimStatus.Failed,
                "Unity lifecycle was ready before BuildPipeline execution.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["lifecycleState"] = response.LifecycleBefore.State.LifecycleState,
                },
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEffect.UnityLifecycleRead), Data: response.LifecycleBefore)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildInputsResolved,
                BuildClaimStatus.Passed,
                "Unity resolved BuildPipeline BuildTarget and scenes.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["buildTarget"] = build.Inputs.Target.StableName,
                    ["sceneCount"] = build.Inputs.Scenes.Paths.Count,
                },
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEvidenceKind.BuildInput), EvidenceRef: BuildReportRefs.Build, Data: response.Input)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildRunnerResolved,
                BuildClaimStatus.Passed,
                "Build runner was resolved before invocation.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = build.Runner.Kind,
                },
                [new BuildEvidenceOutput(Kind: ResolveRunnerEffect(build.Runner.Kind), EvidenceRef: BuildReportRefs.Build)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildCompleted,
                knownTerminalResult ? BuildClaimStatus.Passed : BuildClaimStatus.Indeterminate,
                "Build runner reached a terminal result.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["result"] = build.Summary.Result,
                },
                [new BuildEvidenceOutput(Kind: terminalEvidenceKind, EvidenceRef: terminalEvidenceRef, Data: build.Summary)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildSucceeded,
                succeeded ? BuildClaimStatus.Passed : BuildClaimStatus.Failed,
                "Build runner reported a successful result.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["result"] = build.Summary.Result,
                    ["errorCount"] = build.Summary.ErrorCount,
                },
                [new BuildEvidenceOutput(Kind: terminalEvidenceKind, EvidenceRef: terminalEvidenceRef, Data: build.Summary)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildResultAccounted,
                BuildClaimStatus.Passed,
                "Build runner terminal result was persisted in build metadata.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["source"] = build.RunnerResult.Source,
                    ["status"] = build.RunnerResult.Status,
                },
                [new BuildEvidenceOutput(Kind: terminalEvidenceKind, EvidenceRef: BuildReportRefs.Build, Data: build.RunnerResult)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildArtifactsAccounted,
                BuildClaimStatus.Passed,
                "Build output artifacts were counted in the output manifest.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["manifestRef"] = BuildReportRefs.BuildOutputManifest,
                    ["entryCount"] = build.Output.EntryCount,
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
                BuildClaimCodes.UnityBuildProjectMutationAccounted,
                ResolveProjectMutationClaimStatus(response.ProjectMutation),
                "Project mutation audit was recorded according to build policy.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["mode"] = response.ProjectMutation.Mode,
                    ["coverage"] = response.ProjectMutation.Coverage,
                    ["mutated"] = response.ProjectMutation.Mutated,
                },
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEffect.ProjectMutationAudit), EvidenceRef: BuildReportRefs.Build, Data: response.ProjectMutation)]),
            CreateClaim(
                BuildClaimCodes.UnityBuildValidForGeneration,
                HasCompleteGenerationSnapshot(build.Generations) ? BuildClaimStatus.Passed : BuildClaimStatus.Indeterminate,
                "Build artifacts declare the Unity lifecycle generations they are valid for.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["compileGeneration"] = build.Generations.ValidFor?.CompileGeneration,
                    ["domainReloadGeneration"] = build.Generations.ValidFor?.DomainReloadGeneration,
                    ["assetRefreshGeneration"] = build.Generations.ValidFor?.AssetRefreshGeneration,
                    ["playModeGeneration"] = build.Generations.ValidFor?.PlayModeGeneration,
                },
                [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEffect.GenerationSnapshot), EvidenceRef: BuildReportRefs.Build, Data: build.Generations)]),
        };

        if (hasBuildReport)
        {
            claims.Insert(
                7,
                CreateClaim(
                    BuildClaimCodes.UnityBuildReportAccounted,
                    BuildClaimStatus.Passed,
                    "BuildReport artifact was written and digested.",
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["reportRef"] = BuildReportRefs.BuildReport,
                    },
                    [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEffect.UnityBuildReportRead), EvidenceRef: BuildReportRefs.BuildReport)],
                    required: !isExecuteMethod));
        }

        if (isExecuteMethod)
        {
            claims.InsertRange(
                4,
                [
                    CreateClaim(
                        BuildClaimCodes.UnityBuildExecuteMethodResolved,
                        BuildClaimStatus.Passed,
                        "executeMethod runner method resolved before invocation.",
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["method"] = build.Runner.Method,
                        },
                        [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEffect.UnityExecuteMethod), EvidenceRef: BuildReportRefs.Build)]),
                    CreateClaim(
                        BuildClaimCodes.UnityBuildExecuteMethodInvoked,
                        BuildClaimStatus.Passed,
                        "executeMethod runner method invocation started.",
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["method"] = build.Runner.Method,
                        },
                        [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEffect.UnityExecuteMethod), EvidenceRef: BuildReportRefs.Build)]),
                    CreateClaim(
                        BuildClaimCodes.UnityBuildExecuteMethodCompleted,
                        BuildClaimStatus.Passed,
                        "executeMethod runner terminal result was observed.",
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["status"] = build.RunnerResult.Status,
                        },
                        [new BuildEvidenceOutput(Kind: ContractLiteralCodec.ToValue(BuildEffect.UnityExecuteMethod), EvidenceRef: BuildReportRefs.Build, Data: build.RunnerResult)]),
                ]);
        }

        return claims;
    }

    private static BuildClaimStatus ResolveProjectMutationClaimStatus (IpcBuildProjectMutationAudit projectMutation)
    {
        return ContractLiteralCodec.Matches(projectMutation.Coverage, IpcBuildProjectMutationAuditCoverage.Full)
            ? BuildClaimStatus.Passed
            : BuildClaimStatus.Indeterminate;
    }

    private static IReadOnlyList<BuildResidualRiskOutput> CreateResidualRisks (
        BuildProfileProjectMutationMode mode,
        IpcBuildProjectMutationAudit projectMutation)
    {
        var hasMutationRisk = mode == BuildProfileProjectMutationMode.Audit && projectMutation.Mutated;
        var hasCoverageRisk = (mode == BuildProfileProjectMutationMode.Audit || mode == BuildProfileProjectMutationMode.AllowWithAudit)
            && !ContractLiteralCodec.Matches(projectMutation.Coverage, IpcBuildProjectMutationAuditCoverage.Full);
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
            Severity: IpcExecuteDiagnosticSeverityNames.Warning,
            Blocking: false,
            Statement: statement);
    }

    private static bool IsForbiddenProjectMutationViolation (
        BuildProfileProjectMutationMode mode,
        IpcBuildProjectMutationAudit projectMutation)
    {
        return mode == BuildProfileProjectMutationMode.Forbid
            && (projectMutation.Mutated || !ContractLiteralCodec.Matches(projectMutation.Coverage, IpcBuildProjectMutationAuditCoverage.Full));
    }

    private static string ResolveRunnerEffect (string runnerKind)
    {
        return ContractLiteralCodec.Matches(runnerKind, BuildProfileRunnerKind.ExecuteMethod)
            ? ContractLiteralCodec.ToValue(BuildEffect.UnityExecuteMethod)
            : ContractLiteralCodec.ToValue(BuildEffect.UnityBuildPipeline);
    }

    private static bool HasCompleteGenerationSnapshot (BuildGenerationsOutput generations)
    {
        return generations.Before is not null
            && generations.After is not null
            && generations.ValidFor is not null;
    }

    private static BuildClaimOutput CreateClaim (
        UcliCode id,
        BuildClaimStatus status,
        string statement,
        IReadOnlyDictionary<string, object?> subject,
        IReadOnlyList<BuildEvidenceOutput> evidence,
        bool required = true)
    {
        return new BuildClaimOutput(
            Id: id.Value,
            Status: ContractLiteralCodec.ToValue(status),
            Coverage: ContractLiteralCodec.ToValue(status == BuildClaimStatus.Indeterminate ? BuildCoverage.None : BuildCoverage.Full),
            Required: required,
            VerifierRef: BuildReportRefs.Build,
            Statement: statement,
            Subject: subject,
            Evidence: evidence,
            ResidualRisks: EmptyResidualRisks);
    }

    private static string RecalculateVerdict (
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
