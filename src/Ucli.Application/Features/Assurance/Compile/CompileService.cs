using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile;

/// <summary> Executes compile assurance probes and compiles the result into an assurance packet. </summary>
internal sealed class CompileService : ICompileService
{
    private const string VerifierId = "compile";
    private const string SummaryReportRef = "compile.summary";
    private const string DiagnosticsReportRef = "compile.diagnostics";

    private static readonly IReadOnlyList<CompileResidualRiskOutput> EmptyResidualRisks =
        Array.Empty<CompileResidualRiskOutput>();

    private static readonly IReadOnlyList<string> VerifierEffects =
    [
        "assetDatabaseRefresh",
        "scriptCompilation",
        "domainReload",
    ];

    private readonly IProjectContextResolver projectContextResolver;

    private readonly IUnityExecutionModeDecisionService executionModeDecisionService;

    private readonly IUnityRequestExecutor unityRequestExecutor;

    private readonly ICompileRunIdFactory runIdFactory;

    private readonly ICompileRunArtifactReader artifactReader;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="CompileService" /> class. </summary>
    public CompileService (
        IProjectContextResolver projectContextResolver,
        IUnityExecutionModeDecisionService executionModeDecisionService,
        IUnityRequestExecutor unityRequestExecutor,
        ICompileRunIdFactory runIdFactory,
        ICompileRunArtifactReader artifactReader,
        TimeProvider? timeProvider = null)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.executionModeDecisionService = executionModeDecisionService ?? throw new ArgumentNullException(nameof(executionModeDecisionService));
        this.unityRequestExecutor = unityRequestExecutor ?? throw new ArgumentNullException(nameof(unityRequestExecutor));
        this.runIdFactory = runIdFactory ?? throw new ArgumentNullException(nameof(runIdFactory));
        this.artifactReader = artifactReader ?? throw new ArgumentNullException(nameof(artifactReader));
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

        if (!string.Equals(summary.ProjectFingerprint, context.UnityProject.ProjectFingerprint, StringComparison.Ordinal))
        {
            return CompileExecutionResult.Failure(ApplicationFailure.InternalError(
                $"Unity compile summary projectFingerprint mismatch. Requested={context.UnityProject.ProjectFingerprint}, Actual={summary.ProjectFingerprint}."),
                project);
        }

        var output = CreateOutput(
            project,
            requestedMode,
            executionTarget,
            timeout,
            summary,
            artifactReader.ResolveSummaryPath(context.UnityProject, runId),
            artifactReader.ResolveDiagnosticsPath(context.UnityProject, runId));
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
            return CompileDispatchResult.ArtifactRecoverableFailure(ApplicationFailure.FromCode(
                failureInfo.Code,
                failureInfo.Message,
                startupFailure: failureInfo.StartupFailure));
        }

        var response = executionResult.Response!;
        if (response.HasFailureStatus || response.Errors.Count != 0)
        {
            var firstError = response.Errors.FirstOrDefault();
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

        if (!string.Equals(compileResponse.Summary.ProjectFingerprint, project.ProjectFingerprint, StringComparison.Ordinal))
        {
            return CompileDispatchResult.Failure(ApplicationFailure.InternalError(
                $"Unity compile response projectFingerprint mismatch. Requested={project.ProjectFingerprint}, Actual={compileResponse.Summary.ProjectFingerprint}."));
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

            var readResult = await artifactReader.ReadSummaryAsync(unityProject, runId, cancellationToken).ConfigureAwait(false);
            if (readResult.IsSuccess)
            {
                if (readResult.Summary!.Completed)
                {
                    return CompileSummaryPollResult.Success(readResult.Summary);
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
        var compileOutput = new CompileOutput(
            RunId: summary.RunId,
            Refresh: summary.Refresh,
            ScriptCompilation: summary.ScriptCompilation,
            DomainReload: summary.DomainReload,
            Lifecycle: summary.Lifecycle);
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
                    PrimaryClaims: CompileClaimCodes.All,
                    Effects: VerifierEffects,
                    ReportRef: SummaryReportRef),
            ],
            Claims: claims,
            Reports: new Dictionary<string, CompileReportOutput>(StringComparer.Ordinal)
            {
                [SummaryReportRef] = new CompileReportOutput("compile.summary", summaryJsonPath),
                [DiagnosticsReportRef] = new CompileReportOutput("compile.diagnostics", diagnosticsJsonPath),
            },
            ResidualRisks: EmptyResidualRisks,
            RequestedMode: ReadyExecutionModeCodec.ToRequestedModeValue(requestedMode),
            ResolvedMode: ReadyExecutionModeCodec.ToResolvedModeValue(executionTarget),
            SessionKind: ReadyExecutionModeCodec.ToSessionKindValue(executionTarget),
            TimeoutMilliseconds: checked((int)timeout.TotalMilliseconds),
            Compile: compileOutput);
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
                        Kind: "scriptCompilation",
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
                        Kind: "domainReload",
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
