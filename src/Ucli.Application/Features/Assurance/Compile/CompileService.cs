using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile;

/// <summary> Executes compile assurance probes and compiles the result into an assurance packet. </summary>
internal sealed class CompileService : ICompileService
{
    private const string VerifierId = "compile";
    private const string SummaryReportRef = "compile.summary";
    private const string DiagnosticsReportRef = "compile.diagnostics";
    private const string RefreshOriginDiagnosticsRead = "diagnosticsRead";
    private const string UnknownGeneration = "unknown";

    private static readonly IReadOnlyList<CompileResidualRiskOutput> EmptyResidualRisks =
        Array.Empty<CompileResidualRiskOutput>();

    private readonly IProjectContextResolver projectContextResolver;

    private readonly IUnityExecutionModeDecisionService executionModeDecisionService;

    private readonly IUnityRequestExecutor unityRequestExecutor;

    private readonly ICompileRunIdFactory runIdFactory;

    private readonly ICompileRunArtifactStore artifactStore;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="CompileService" /> class. </summary>
    public CompileService (
        IProjectContextResolver projectContextResolver,
        IUnityExecutionModeDecisionService executionModeDecisionService,
        IUnityRequestExecutor unityRequestExecutor,
        ICompileRunIdFactory runIdFactory,
        ICompileRunArtifactStore artifactStore,
        TimeProvider? timeProvider = null)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.executionModeDecisionService = executionModeDecisionService ?? throw new ArgumentNullException(nameof(executionModeDecisionService));
        this.unityRequestExecutor = unityRequestExecutor ?? throw new ArgumentNullException(nameof(unityRequestExecutor));
        this.runIdFactory = runIdFactory ?? throw new ArgumentNullException(nameof(runIdFactory));
        this.artifactStore = artifactStore ?? throw new ArgumentNullException(nameof(artifactStore));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async ValueTask<CompileExecutionResult> ExecuteAsync (
        CompileCommandInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await projectContextResolver.ResolveAsync(input.ProjectPath, cancellationToken).ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return CompileExecutionResult.Failure(contextResult.Error!);
        }

        var context = contextResult.Context!;
        var project = ProjectIdentityInfo.From(context.UnityProject);
        var timeoutResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            UcliCommandIds.Compile,
            context.Config);
        if (!timeoutResult.IsSuccess)
        {
            return CompileExecutionResult.Failure(timeoutResult.Error!, project);
        }

        var timeout = timeoutResult.Timeout!.Value;
        var requestedMode = input.Mode ?? UnityExecutionMode.Auto;
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        if (!deadline.TryGetRemainingTimeout(out var modeDecisionTimeout))
        {
            return CompileExecutionResult.Failure(CreateTimeoutFailure(timeout), project);
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
                return CompileExecutionResult.Failure(
                    ApplicationFailure.FromCode(contractError.Code, contractError.Message),
                    project);
            }

            return CompileExecutionResult.Failure(modeDecisionResult.Error!, project);
        }

        var executionTarget = modeDecisionResult.Decision!.Target;
        if (!deadline.TryGetRemainingTimeout(out var requestTimeout))
        {
            return CompileExecutionResult.Failure(CreateTimeoutFailure(timeout), project);
        }

        var runId = runIdFactory.Create();
        var responseSummary = await DispatchCompileAsync(
                context,
                project,
                ResolveExecutionMode(executionTarget),
                requestTimeout,
                runId,
                cancellationToken)
            .ConfigureAwait(false);
        if (!responseSummary.IsSuccess && !responseSummary.ShouldReadArtifact)
        {
            return CompileExecutionResult.Failure(responseSummary.FailureInfo!, project);
        }

        var summary = responseSummary.Summary;
        if (summary is null || !summary.Completed)
        {
            var artifactResult = await ReadSummaryUntilDeadlineAsync(
                    context.UnityProject,
                    runId,
                    deadline,
                    responseSummary.FailureInfo,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!artifactResult.IsSuccess)
            {
                return CompileExecutionResult.Failure(artifactResult.FailureInfo!, project);
            }

            summary = artifactResult.Summary!;
        }

        var completedSummary = summary ?? throw new InvalidOperationException("Compile summary was not resolved.");
        var summaryValidationFailure = ValidateSummary(
            completedSummary,
            runId,
            context.UnityProject.ProjectFingerprint,
            requireCompleted: true);
        if (summaryValidationFailure != null)
        {
            return CompileExecutionResult.Failure(summaryValidationFailure, project);
        }

        var output = CreateOutput(
            project,
            requestedMode,
            executionTarget,
            timeout,
            completedSummary,
            artifactStore.ResolveSummaryPath(context.UnityProject, runId),
            artifactStore.ResolveDiagnosticsPath(context.UnityProject, runId));
        return CompileExecutionResult.Success(output);
    }

    private async ValueTask<CompileDispatchResult> DispatchCompileAsync (
        ProjectContext context,
        ProjectIdentityInfo project,
        UnityExecutionMode mode,
        TimeSpan requestTimeout,
        string runId,
        CancellationToken cancellationToken)
    {
        var executionResult = await unityRequestExecutor.ExecuteAsync(
                UcliCommandIds.Compile,
                mode,
                requestTimeout,
                context.Config,
                context.UnityProject,
                new UnityRequestPayload.Compile(runId),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            var failureInfo = executionResult.FailureInfo!;
            if (TryCreateDiagnosticsReadSummary(
                    failureInfo.StartupFailure,
                    project,
                    runId,
                    out var diagnosticsReadSummary))
            {
                var writeError = await artifactStore.WriteArtifactsAsync(
                        context.UnityProject,
                        runId,
                        diagnosticsReadSummary!,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (writeError != null)
                {
                    return CompileDispatchResult.Failure(ApplicationFailure.FromExecutionError(writeError));
                }

                return CompileDispatchResult.Success(diagnosticsReadSummary!);
            }

            if (failureInfo.StartupFailure != null)
            {
                return CompileDispatchResult.Failure(ApplicationFailure.FromCode(
                    failureInfo.Code,
                    failureInfo.Message,
                    startupFailure: failureInfo.StartupFailure));
            }

            return CompileDispatchResult.ArtifactRecoverableFailure(ApplicationFailure.FromCode(
                failureInfo.Code,
                failureInfo.Message,
                startupFailure: failureInfo.StartupFailure));
        }

        var response = executionResult.Response!;
        if (response.HasFailureStatus || response.Errors.Count != 0)
        {
            var firstError = response.Errors.FirstOrDefault();
            if (firstError?.Code == ExecutionErrorCodes.IpcTimeout)
            {
                return CompileDispatchResult.ArtifactRecoverableFailure(ApplicationFailure.FromCode(
                    firstError.Code,
                    firstError.Message));
            }

            return CompileDispatchResult.Failure(ApplicationFailure.FromCode(
                firstError?.Code,
                firstError?.Message ?? $"Unity compile IPC failed with status '{response.FailureStatus}'."));
        }

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcCompileResponse compileResponse, out var payloadError))
        {
            return CompileDispatchResult.Failure(ApplicationFailure.InternalError(
                $"Unity compile payload is invalid. {payloadError.Message}"));
        }

        if (!string.Equals(compileResponse.RunId, runId, StringComparison.Ordinal))
        {
            return CompileDispatchResult.Failure(ApplicationFailure.InternalError(
                $"Unity compile response runId mismatch. Requested={runId}, Actual={compileResponse.RunId}."));
        }

        if (compileResponse.Summary is null)
        {
            return CompileDispatchResult.Failure(ApplicationFailure.InternalError("Unity compile response summary is missing."));
        }

        var summaryValidationFailure = ValidateSummary(
            compileResponse.Summary,
            runId,
            project.ProjectFingerprint,
            requireCompleted: false);
        if (summaryValidationFailure != null)
        {
            return CompileDispatchResult.Failure(summaryValidationFailure);
        }

        return CompileDispatchResult.Success(compileResponse.Summary);
    }

    private async ValueTask<CompileSummaryPollResult> ReadSummaryUntilDeadlineAsync (
        ResolvedUnityProjectContext unityProject,
        string runId,
        ExecutionDeadline deadline,
        ApplicationFailure? dispatchFailure,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var readResult = await artifactStore.ReadSummaryAsync(unityProject, runId, cancellationToken).ConfigureAwait(false);
            if (readResult.IsSuccess)
            {
                var summary = readResult.Summary!;
                var summaryValidationFailure = ValidateSummary(
                    summary,
                    runId,
                    unityProject.ProjectFingerprint,
                    requireCompleted: false);
                if (summaryValidationFailure != null)
                {
                    return CompileSummaryPollResult.Failure(summaryValidationFailure);
                }

                if (summary.Completed)
                {
                    return CompileSummaryPollResult.Success(summary);
                }
            }
            else if (!readResult.IsMissing)
            {
                return CompileSummaryPollResult.Failure(ApplicationFailure.FromExecutionError(readResult.Error!));
            }

            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                return CompileSummaryPollResult.Failure(dispatchFailure ?? ApplicationFailure.Timeout(
                    $"Unity compile assurance timed out after {timeout.TotalMilliseconds:0} milliseconds.",
                    ExecutionErrorCodes.IpcTimeout));
            }

            await TimeProviderDelay.DelayAsync(GetRetryDelay(remainingTimeout), timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private static CompileExecutionOutput CreateOutput (
        ProjectIdentityInfo project,
        UnityExecutionMode requestedMode,
        UnityExecutionTarget executionTarget,
        TimeSpan timeout,
        IpcCompileSummary summary,
        string summaryJsonPath,
        string diagnosticsJsonPath)
    {
        var compileOutput = CreateCompileOutput(summary);
        var claims = CreateClaims(summary, compileOutput);
        var verdict = RecalculateVerdict(claims);
        return new CompileExecutionOutput(
            Verdict: verdict,
            Project: project,
            Verifiers:
            [
                new CompileVerifierOutput(
                    Id: VerifierId,
                    Kind: VerifierId,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: CompileClaimCodes.AllValues,
                    Effects: CompileEffectValues.All,
                    ReportRef: SummaryReportRef),
            ],
            Claims: claims,
            Reports: new Dictionary<string, CompileReportOutput>(StringComparer.Ordinal)
            {
                [SummaryReportRef] = new CompileReportOutput("compile.summary", summaryJsonPath),
                [DiagnosticsReportRef] = new CompileReportOutput("compile.diagnostics", diagnosticsJsonPath),
            },
            ResidualRisks: EmptyResidualRisks,
            RequestedMode: AssuranceExecutionModeCodec.ToRequestedModeValue(requestedMode),
            ResolvedMode: AssuranceExecutionModeCodec.ToResolvedModeValue(executionTarget),
            SessionKind: AssuranceExecutionModeCodec.ToSessionKindValue(executionTarget),
            TimeoutMilliseconds: checked((int)timeout.TotalMilliseconds),
            Compile: compileOutput);
    }

    private static CompileOutput CreateCompileOutput (IpcCompileSummary summary)
    {
        return new CompileOutput(
            RunId: summary.RunId,
            Refresh: new CompileRefreshOutput(
                Origin: summary.Refresh.Origin,
                Requested: summary.Refresh.Requested,
                StartedAtUtc: summary.Refresh.StartedAtUtc,
                CompletedAtUtc: summary.Refresh.CompletedAtUtc,
                Completed: summary.Refresh.Completed),
            ScriptCompilation: new CompileScriptCompilationOutput(
                Started: summary.ScriptCompilation.Started,
                Completed: summary.ScriptCompilation.Completed,
                CompileGenerationBefore: summary.ScriptCompilation.CompileGenerationBefore,
                CompileGenerationAfter: summary.ScriptCompilation.CompileGenerationAfter,
                Diagnostics: new CompileDiagnosticsOutput(
                    ErrorCount: summary.ScriptCompilation.Diagnostics.ErrorCount,
                    WarningCount: summary.ScriptCompilation.Diagnostics.WarningCount,
                    PrimaryDiagnostic: CreatePrimaryDiagnosticOutput(summary.ScriptCompilation.Diagnostics.PrimaryDiagnostic))),
            DomainReload: new CompileDomainReloadOutput(
                ReloadRequired: summary.DomainReload.ReloadRequired,
                ReloadObserved: summary.DomainReload.ReloadObserved,
                GenerationBefore: summary.DomainReload.GenerationBefore,
                GenerationAfter: summary.DomainReload.GenerationAfter,
                Settled: summary.DomainReload.Settled),
            Lifecycle: new CompileLifecycleOutput(
                ServerVersion: summary.Lifecycle.ServerVersion,
                UnityVersion: summary.Lifecycle.UnityVersion,
                EditorMode: summary.Lifecycle.EditorMode,
                LifecycleState: summary.Lifecycle.LifecycleState,
                BlockingReason: summary.Lifecycle.BlockingReason,
                CompileState: summary.Lifecycle.CompileState,
                CompileGeneration: summary.Lifecycle.CompileGeneration,
                DomainReloadGeneration: summary.Lifecycle.DomainReloadGeneration,
                CanAcceptExecutionRequests: summary.Lifecycle.CanAcceptExecutionRequests,
                ObservedAtUtc: summary.Lifecycle.ObservedAtUtc,
                ActionRequired: summary.Lifecycle.ActionRequired,
                PrimaryDiagnostic: CreatePrimaryDiagnosticOutput(summary.Lifecycle.PrimaryDiagnostic)));
    }

    private static CompilePrimaryDiagnosticOutput? CreatePrimaryDiagnosticOutput (IpcPrimaryDiagnostic? diagnostic)
    {
        if (diagnostic is null || !StringValueNormalizer.TryTrimToNonEmpty(diagnostic.Kind, out var kind))
        {
            return null;
        }

        return new CompilePrimaryDiagnosticOutput(
            Kind: kind,
            Code: StringValueNormalizer.TrimToNull(diagnostic.Code),
            File: StringValueNormalizer.TrimToNull(diagnostic.File),
            Line: diagnostic.Line,
            Column: diagnostic.Column,
            Message: StringValueNormalizer.TrimToNull(diagnostic.Message));
    }

    private static IReadOnlyList<CompileClaimOutput> CreateClaims (
        IpcCompileSummary summary,
        CompileOutput compileOutput)
    {
        return
        [
            CreateClaim(
                CompileClaimCodes.UnityCompileNoErrors,
                ResolveCompileNoErrorsStatus(summary),
                "Unity script compilation completed without compiler errors.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = "unityCompile",
                    ["runId"] = summary.RunId,
                },
                [
                    new CompileEvidenceOutput(
                        Kind: CompileEffectValues.ScriptCompilation,
                        EvidenceRef: DiagnosticsReportRef,
                        Data: compileOutput.ScriptCompilation),
                ]),
            CreateClaim(
                CompileClaimCodes.UnityDomainReloadSettled,
                summary.DomainReload.Settled ? CompileClaimStatusValues.Passed : CompileClaimStatusValues.Failed,
                "Unity domain reload reached a settled state after compile observation.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = "unityDomainReload",
                    ["runId"] = summary.RunId,
                },
                [
                    new CompileEvidenceOutput(
                        Kind: CompileEffectValues.DomainReload,
                        Data: compileOutput.DomainReload),
                ]),
            CreateClaim(
                CompileClaimCodes.UnityLifecycleReadyAfterCompile,
                summary.Lifecycle.CanAcceptExecutionRequests ? CompileClaimStatusValues.Passed : CompileClaimStatusValues.Failed,
                "Unity lifecycle is ready after compile observation.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = "unityLifecycle",
                    ["runId"] = summary.RunId,
                    ["lifecycleState"] = summary.Lifecycle.LifecycleState,
                },
                [
                    new CompileEvidenceOutput(
                        Kind: "lifecycleSnapshot",
                        Data: compileOutput.Lifecycle),
                ]),
        ];
    }

    private static CompileClaimOutput CreateClaim (
        string id,
        string status,
        string statement,
        IReadOnlyDictionary<string, object?> subject,
        IReadOnlyList<CompileEvidenceOutput> evidence)
    {
        return new CompileClaimOutput(
            Id: id,
            Status: status,
            Coverage: CompileCoverageValues.Full,
            Required: true,
            VerifierRef: VerifierId,
            Statement: string.Equals(status, CompileClaimStatusValues.Passed, StringComparison.Ordinal)
                ? statement
                : statement.Replace(" completed ", " did not complete ", StringComparison.Ordinal),
            Subject: subject,
            Evidence: evidence,
            ResidualRisks: EmptyResidualRisks);
    }

    private static string ResolveCompileNoErrorsStatus (IpcCompileSummary summary)
    {
        return summary.ScriptCompilation.Completed
            && summary.ScriptCompilation.Diagnostics.ErrorCount == 0
            ? CompileClaimStatusValues.Passed
            : CompileClaimStatusValues.Failed;
    }

    private static bool TryCreateDiagnosticsReadSummary (
        StartupFailureDetail? startupFailure,
        ProjectIdentityInfo project,
        string runId,
        out IpcCompileSummary? summary)
    {
        summary = null;
        var diagnosis = startupFailure?.Diagnosis;
        if (diagnosis is null)
        {
            return false;
        }

        var primaryDiagnostic = diagnosis.PrimaryDiagnostic;
        var isCompilerDiagnosis = primaryDiagnostic is not null
            && string.Equals(primaryDiagnostic.Kind, DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler, StringComparison.Ordinal);
        if (!isCompilerDiagnosis
            && !string.Equals(diagnosis.Reason, DaemonDiagnosisReasonValues.UnityScriptCompilationFailed, StringComparison.Ordinal))
        {
            return false;
        }

        var startup = startupFailure!.Startup;
        var observedAtUtc = diagnosis.UpdatedAtUtc;
        var startedAtUtc = startup?.StartedAtUtc ?? observedAtUtc;
        var compileDiagnostic = primaryDiagnostic is null
            ? new IpcPrimaryDiagnostic(
                Kind: DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler,
                Code: null,
                File: null,
                Line: null,
                Column: null,
                Message: diagnosis.Message)
            : new IpcPrimaryDiagnostic(
                Kind: primaryDiagnostic.Kind,
                Code: primaryDiagnostic.Code,
                File: primaryDiagnostic.File,
                Line: primaryDiagnostic.Line,
                Column: primaryDiagnostic.Column,
                Message: primaryDiagnostic.Message);

        summary = new IpcCompileSummary(
            RunId: runId,
            ProjectFingerprint: project.ProjectFingerprint,
            Completed: true,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: observedAtUtc,
            Refresh: new IpcCompileSummary.RefreshEvidence(
                Origin: RefreshOriginDiagnosticsRead,
                Requested: false,
                StartedAtUtc: startedAtUtc,
                CompletedAtUtc: observedAtUtc,
                Completed: true),
            ScriptCompilation: new IpcCompileSummary.ScriptCompilationEvidence(
                Started: false,
                Completed: true,
                CompileGenerationBefore: UnknownGeneration,
                CompileGenerationAfter: UnknownGeneration,
                Diagnostics: new IpcCompileSummary.DiagnosticsEvidence(
                    ErrorCount: 1,
                    WarningCount: 0,
                    PrimaryDiagnostic: compileDiagnostic)),
            DomainReload: new IpcCompileSummary.DomainReloadEvidence(
                ReloadRequired: false,
                ReloadObserved: false,
                GenerationBefore: UnknownGeneration,
                GenerationAfter: UnknownGeneration,
                Settled: true),
            Lifecycle: new IpcCompileSummary.LifecycleEvidence(
                ServerVersion: null,
                UnityVersion: project.UnityVersion,
                EditorMode: startup?.EditorMode,
                LifecycleState: IpcEditorLifecycleStateCodec.CompileFailed,
                BlockingReason: IpcEditorLifecycleStateCodec.CompileFailed,
                CompileState: IpcCompileStateCodec.Failed,
                CompileGeneration: UnknownGeneration,
                DomainReloadGeneration: UnknownGeneration,
                CanAcceptExecutionRequests: false,
                ObservedAtUtc: observedAtUtc,
                ActionRequired: DaemonDiagnosisActionRequiredValues.FixCompileErrors,
                PrimaryDiagnostic: compileDiagnostic));
        return true;
    }

    private static ApplicationFailure? ValidateSummary (
        IpcCompileSummary summary,
        string expectedRunId,
        string expectedProjectFingerprint,
        bool requireCompleted)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedRunId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedProjectFingerprint);

        if (!string.Equals(summary.RunId, expectedRunId, StringComparison.Ordinal))
        {
            return ApplicationFailure.InternalError(
                $"Unity compile summary runId mismatch. Requested={expectedRunId}, Actual={summary.RunId}.");
        }

        if (!string.Equals(summary.ProjectFingerprint, expectedProjectFingerprint, StringComparison.Ordinal))
        {
            return ApplicationFailure.InternalError(
                $"Unity compile summary projectFingerprint mismatch. Requested={expectedProjectFingerprint}, Actual={summary.ProjectFingerprint}.");
        }

        if (requireCompleted && !summary.Completed)
        {
            return ApplicationFailure.InternalError("Unity compile summary is incomplete.");
        }

        if (summary.Refresh is null)
        {
            return ApplicationFailure.InternalError("Unity compile summary is missing refresh evidence.");
        }

        if (!string.Equals(summary.Refresh.Origin, CompileEffectValues.AssetDatabaseRefresh, StringComparison.Ordinal)
            && !string.Equals(summary.Refresh.Origin, RefreshOriginDiagnosticsRead, StringComparison.Ordinal))
        {
            return ApplicationFailure.InternalError(
                $"Unity compile summary refresh origin is invalid: {summary.Refresh.Origin}.");
        }

        if (summary.ScriptCompilation is null)
        {
            return ApplicationFailure.InternalError("Unity compile summary is missing script compilation evidence.");
        }

        if (summary.ScriptCompilation.Diagnostics is null)
        {
            return ApplicationFailure.InternalError("Unity compile summary is missing diagnostics evidence.");
        }

        if (summary.ScriptCompilation.Diagnostics.ErrorCount < 0
            || summary.ScriptCompilation.Diagnostics.WarningCount < 0)
        {
            return ApplicationFailure.InternalError("Unity compile summary diagnostic counts must not be negative.");
        }

        if (summary.DomainReload is null)
        {
            return ApplicationFailure.InternalError("Unity compile summary is missing domain reload evidence.");
        }

        if (summary.Lifecycle is null)
        {
            return ApplicationFailure.InternalError("Unity compile summary is missing lifecycle evidence.");
        }

        return null;
    }

    private static string RecalculateVerdict (IReadOnlyList<CompileClaimOutput> claims)
    {
        if (claims.Any(static claim => string.Equals(claim.Status, CompileClaimStatusValues.Failed, StringComparison.Ordinal)))
        {
            return CompileVerdictValues.Fail;
        }

        if (claims.Any(static claim =>
                !string.Equals(claim.Status, CompileClaimStatusValues.Passed, StringComparison.Ordinal)
                || !string.Equals(claim.Coverage, CompileCoverageValues.Full, StringComparison.Ordinal)))
        {
            return CompileVerdictValues.Incomplete;
        }

        return CompileVerdictValues.Pass;
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
            $"Unity compile assurance timed out after {timeout.TotalMilliseconds:0} milliseconds.",
            ExecutionErrorCodes.IpcTimeout);
    }

    private static TimeSpan GetRetryDelay (TimeSpan remainingTimeout)
    {
        var retryDelayMilliseconds = Math.Min(
            DaemonTimeouts.StartupProbeRetryDelayMilliseconds,
            Math.Max(1, (int)Math.Ceiling(remainingTimeout.TotalMilliseconds)));
        return TimeSpan.FromMilliseconds(retryDelayMilliseconds);
    }

    private sealed record CompileDispatchResult (
        IpcCompileSummary? Summary,
        ApplicationFailure? FailureInfo,
        bool ShouldReadArtifact)
    {
        public bool IsSuccess => Summary != null && FailureInfo == null;

        public static CompileDispatchResult Success (IpcCompileSummary summary)
        {
            ArgumentNullException.ThrowIfNull(summary);
            return new CompileDispatchResult(summary, null, ShouldReadArtifact: false);
        }

        public static CompileDispatchResult Failure (ApplicationFailure failure)
        {
            ArgumentNullException.ThrowIfNull(failure);
            return new CompileDispatchResult(null, failure, ShouldReadArtifact: false);
        }

        public static CompileDispatchResult ArtifactRecoverableFailure (ApplicationFailure failure)
        {
            ArgumentNullException.ThrowIfNull(failure);
            return new CompileDispatchResult(null, failure, ShouldReadArtifact: true);
        }
    }

    private sealed record CompileSummaryPollResult (
        IpcCompileSummary? Summary,
        ApplicationFailure? FailureInfo)
    {
        public bool IsSuccess => Summary != null && FailureInfo == null;

        public static CompileSummaryPollResult Success (IpcCompileSummary summary)
        {
            ArgumentNullException.ThrowIfNull(summary);
            return new CompileSummaryPollResult(summary, null);
        }

        public static CompileSummaryPollResult Failure (ApplicationFailure failure)
        {
            ArgumentNullException.ThrowIfNull(failure);
            return new CompileSummaryPollResult(null, failure);
        }
    }
}
