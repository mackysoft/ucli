using MackySoft.Ucli.Application.Features.Assurance.Compile.Artifacts;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Vocabulary;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Application.Shared.Identifiers;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Execution;

/// <summary> Executes compile assurance probes and compiles the result into an assurance packet. </summary>
internal sealed class CompileService : ICompileService
{
    private const string SummaryReportRef = "compile.summary";
    private const string DiagnosticsReportRef = "compile.diagnostics";
    private const string ProgressObservationSourceHostDispatch = "hostDispatch";

    internal static readonly AssuranceVerifierId VerifierId = new("compile");

    private static readonly IReadOnlyList<CompileResidualRiskOutput> EmptyResidualRisks =
        Array.Empty<CompileResidualRiskOutput>();

    private readonly IProjectContextResolver projectContextResolver;

    private readonly IUnityExecutionModeDecisionService executionModeDecisionService;

    private readonly IUnityRequestExecutor unityRequestExecutor;

    private readonly IGuidGenerator runIdGenerator;

    private readonly ICompileRunArtifactStore artifactStore;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="CompileService" /> class. </summary>
    public CompileService (
        IProjectContextResolver projectContextResolver,
        IUnityExecutionModeDecisionService executionModeDecisionService,
        IUnityRequestExecutor unityRequestExecutor,
        IGuidGenerator runIdGenerator,
        ICompileRunArtifactStore artifactStore,
        TimeProvider timeProvider)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.executionModeDecisionService = executionModeDecisionService ?? throw new ArgumentNullException(nameof(executionModeDecisionService));
        this.unityRequestExecutor = unityRequestExecutor ?? throw new ArgumentNullException(nameof(unityRequestExecutor));
        this.runIdGenerator = runIdGenerator ?? throw new ArgumentNullException(nameof(runIdGenerator));
        this.artifactStore = artifactStore ?? throw new ArgumentNullException(nameof(artifactStore));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async ValueTask<CompileExecutionResult> ExecuteAsync (
        CompileCommandInput input,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        var resolvedProgressSink = progressSink ?? NullCommandProgressSink.Instance;
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

        var runId = runIdGenerator.Generate();
        await EmitStartedAsync(
                resolvedProgressSink,
                runId,
                project,
                requestedMode,
                executionTarget,
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        await EmitRefreshStartedAsync(resolvedProgressSink, runId, cancellationToken).ConfigureAwait(false);
        var responseSummary = await DispatchCompileAsync(
                context,
                project,
                UnityExecutionTargetModeMapper.ToExplicitMode(executionTarget),
                requestTimeout,
                runId,
                resolvedProgressSink,
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
            await EmitRecoveredAsync(
                    resolvedProgressSink,
                    summary,
                    artifactStore.ResolveSummaryPath(context.UnityProject, runId),
                    responseSummary.FailureInfo,
                    artifactResult.PollAttempts,
                    cancellationToken)
                .ConfigureAwait(false);
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

        var summaryJsonPath = artifactStore.ResolveSummaryPath(context.UnityProject, runId);
        var diagnosticsJsonPath = artifactStore.ResolveDiagnosticsPath(context.UnityProject, runId);
        var output = CreateOutput(
            project,
            requestedMode,
            executionTarget,
            timeout,
            completedSummary,
            summaryJsonPath,
            diagnosticsJsonPath);
        await EmitCompletedAsync(
                resolvedProgressSink,
                output,
                summaryJsonPath,
                diagnosticsJsonPath,
                cancellationToken)
            .ConfigureAwait(false);
        return CompileExecutionResult.Success(output);
    }

    private async ValueTask<CompileDispatchResult> DispatchCompileAsync (
        ProjectContext context,
        ProjectIdentityInfo project,
        UnityExecutionMode mode,
        TimeSpan requestTimeout,
        Guid runId,
        ICommandProgressSink progressSink,
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

                await EmitDiagnosticAsync(progressSink, diagnosticsReadSummary!, cancellationToken).ConfigureAwait(false);
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
        if (response.Errors.Count != 0)
        {
            var firstError = response.Errors[0];
            if (firstError.Code == ExecutionErrorCodes.IpcTimeout)
            {
                return CompileDispatchResult.ArtifactRecoverableFailure(ApplicationFailure.FromCode(
                    firstError.Code,
                    firstError.Message));
            }

            return CompileDispatchResult.Failure(ApplicationFailure.FromCode(
                firstError.Code,
                firstError.Message));
        }

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcCompileResponse compileResponse, out var payloadError))
        {
            return CompileDispatchResult.Failure(ApplicationFailure.InternalError(
                $"Unity compile payload is invalid. {payloadError.Message}"));
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
        Guid runId,
        ExecutionDeadline deadline,
        ApplicationFailure? dispatchFailure,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var pollAttempts = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            pollAttempts++;
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
                    return CompileSummaryPollResult.Failure(summaryValidationFailure, pollAttempts);
                }

                if (summary.Completed)
                {
                    return CompileSummaryPollResult.Success(summary, pollAttempts);
                }
            }
            else if (!readResult.IsMissing)
            {
                return CompileSummaryPollResult.Failure(ApplicationFailure.FromExecutionError(readResult.Error!), pollAttempts);
            }

            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                return CompileSummaryPollResult.Failure(dispatchFailure ?? ApplicationFailure.Timeout(
                    $"Unity compile assurance timed out after {timeout.TotalMilliseconds:0} milliseconds.",
                    ExecutionErrorCodes.IpcTimeout), pollAttempts);
            }

            await TimeProviderDelay.DelayAsync(GetRetryDelay(remainingTimeout), timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private static ValueTask EmitStartedAsync (
        ICommandProgressSink progressSink,
        Guid runId,
        ProjectIdentityInfo project,
        UnityExecutionMode requestedMode,
        UnityExecutionTarget executionTarget,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        return progressSink.OnEntryAsync(
            CompileProgressEventNames.Started,
            new CompileStartedEntry(
                RunId: runId,
                ProjectFingerprint: project.ProjectFingerprint,
                RequestedMode: AssuranceExecutionModeCodec.ToRequestedMode(requestedMode),
                ResolvedMode: AssuranceExecutionModeCodec.ToResolvedMode(executionTarget),
                SessionKind: AssuranceExecutionModeCodec.ToSessionKind(executionTarget),
                TimeoutMilliseconds: checked((int)timeout.TotalMilliseconds)),
            cancellationToken);
    }

    private static ValueTask EmitRefreshStartedAsync (
        ICommandProgressSink progressSink,
        Guid runId,
        CancellationToken cancellationToken)
    {
        return progressSink.OnEntryAsync(
            CompileProgressEventNames.RefreshStarted,
            new CompileRefreshStartedEntry(
                RunId: runId,
                RefreshOrigin: CompileRefreshOrigin.AssetDatabaseRefresh,
                ObservationSource: ProgressObservationSourceHostDispatch),
            cancellationToken);
    }

    private static ValueTask EmitDiagnosticAsync (
        ICommandProgressSink progressSink,
        IpcCompileSummary summary,
        CancellationToken cancellationToken)
    {
        return progressSink.OnEntryAsync(
            CompileProgressEventNames.Diagnostic,
            new CompileDiagnosticEntry(
                RunId: summary.RunId,
                RefreshOrigin: CompileRefreshOrigin.DiagnosticsRead,
                PrimaryDiagnostic: summary.ScriptCompilation.Diagnostics.PrimaryDiagnostic),
            cancellationToken);
    }

    private static ValueTask EmitRecoveredAsync (
        ICommandProgressSink progressSink,
        IpcCompileSummary summary,
        string summaryJsonPath,
        ApplicationFailure? dispatchFailure,
        int pollAttempts,
        CancellationToken cancellationToken)
    {
        return progressSink.OnEntryAsync(
            CompileProgressEventNames.Recovered,
            new CompileRecoveredEntry(
                RunId: summary.RunId,
                SummaryJsonPath: summaryJsonPath,
                DispatchFailureCode: dispatchFailure?.Code.Value,
                PollAttempts: pollAttempts),
            cancellationToken);
    }

    private static ValueTask EmitCompletedAsync (
        ICommandProgressSink progressSink,
        CompileExecutionOutput output,
        string summaryJsonPath,
        string diagnosticsJsonPath,
        CancellationToken cancellationToken)
    {
        return progressSink.OnEntryAsync(
            CompileProgressEventNames.Completed,
            new CompileCompletedEntry(
                RunId: output.Compile.RunId,
                Verdict: output.Verdict,
                ErrorCount: output.Compile.ScriptCompilation.Diagnostics.ErrorCount,
                WarningCount: output.Compile.ScriptCompilation.Diagnostics.WarningCount,
                SummaryJsonPath: summaryJsonPath,
                DiagnosticsJsonPath: diagnosticsJsonPath),
            cancellationToken);
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
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: CompileClaimCodes.All,
                    Effects: AssuranceEffectSets.Compile,
                    ReportRef: SummaryReportRef),
            ],
            Claims: claims,
            Reports: new Dictionary<string, AssuranceReportReference>(StringComparer.Ordinal)
            {
                [SummaryReportRef] = AssuranceReportReference.FromPath(summaryJsonPath, digest: null),
                [DiagnosticsReportRef] = AssuranceReportReference.FromPath(diagnosticsJsonPath, digest: null),
            },
            ResidualRisks: EmptyResidualRisks,
            RequestedMode: AssuranceExecutionModeCodec.ToRequestedMode(requestedMode),
            ResolvedMode: AssuranceExecutionModeCodec.ToResolvedMode(executionTarget),
            SessionKind: AssuranceExecutionModeCodec.ToSessionKind(executionTarget),
            TimeoutMilliseconds: checked((int)timeout.TotalMilliseconds),
            Compile: compileOutput);
    }

    private static CompileOutput CreateCompileOutput (IpcCompileSummary summary)
    {
        var state = summary.Lifecycle.State;
        return new CompileOutput(
            runId: summary.RunId,
            refresh: new CompileRefreshOutput(
                Origin: summary.Refresh.Origin,
                Requested: summary.Refresh.Requested,
                StartedAtUtc: summary.Refresh.StartedAtUtc,
                CompletedAtUtc: summary.Refresh.CompletedAtUtc,
                Completed: summary.Refresh.Completed),
            scriptCompilation: new CompileScriptCompilationOutput(
                Started: summary.ScriptCompilation.Started,
                Completed: summary.ScriptCompilation.Completed,
                CompileGenerationBefore: summary.ScriptCompilation.CompileGenerationBefore,
                CompileGenerationAfter: summary.ScriptCompilation.CompileGenerationAfter,
                Diagnostics: new CompileDiagnosticsOutput(
                    ErrorCount: summary.ScriptCompilation.Diagnostics.ErrorCount,
                    WarningCount: summary.ScriptCompilation.Diagnostics.WarningCount,
                    PrimaryDiagnostic: CreatePrimaryDiagnosticOutput(summary.ScriptCompilation.Diagnostics.PrimaryDiagnostic))),
            domainReload: new CompileDomainReloadOutput(
                ReloadRequired: summary.DomainReload.ReloadRequired,
                ReloadObserved: summary.DomainReload.ReloadObserved,
                GenerationBefore: summary.DomainReload.GenerationBefore,
                GenerationAfter: summary.DomainReload.GenerationAfter,
                Settled: summary.DomainReload.Settled),
            lifecycle: new CompileLifecycleOutput(
                ServerVersion: summary.Lifecycle.ServerVersion,
                UnityVersion: summary.Lifecycle.UnityVersion,
                EditorMode: state?.EditorMode,
                LifecycleState: state?.LifecycleState,
                BlockingReason: state is not null
                    ? IpcEditorLifecycleSemantics.ResolveBlockingReason(state.LifecycleState)
                    : null,
                CompileState: state?.CompileState,
                Generations: state?.Generations,
                CanAcceptExecutionRequests: state is not null
                    && IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(state.LifecycleState),
                ObservedAtUtc: summary.Lifecycle.ObservedAtUtc,
                ActionRequired: summary.Lifecycle.ActionRequired,
                PrimaryDiagnostic: CreatePrimaryDiagnosticOutput(summary.Lifecycle.PrimaryDiagnostic)));
    }

    private static CompilePrimaryDiagnosticOutput? CreatePrimaryDiagnosticOutput (IpcPrimaryDiagnostic? diagnostic)
    {
        if (diagnostic is null || !diagnostic.Kind.HasValue)
        {
            return null;
        }

        return new CompilePrimaryDiagnosticOutput(
            Kind: diagnostic.Kind.Value,
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
                        Kind: CompileEvidenceKind.ScriptCompilation,
                        EvidenceRef: DiagnosticsReportRef,
                        Data: compileOutput.ScriptCompilation),
                ]),
            CreateClaim(
                CompileClaimCodes.UnityDomainReloadSettled,
                summary.DomainReload.Settled ? AssuranceClaimStatus.Passed : AssuranceClaimStatus.Failed,
                "Unity domain reload reached a settled state after compile observation.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = "unityDomainReload",
                    ["runId"] = summary.RunId,
                },
                [
                    new CompileEvidenceOutput(
                        Kind: CompileEvidenceKind.DomainReload,
                        EvidenceRef: null,
                        Data: compileOutput.DomainReload),
                ]),
            CreateClaim(
                CompileClaimCodes.UnityLifecycleReadyAfterCompile,
                summary.Lifecycle.State is not null
                    && IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(summary.Lifecycle.State.LifecycleState)
                    ? AssuranceClaimStatus.Passed
                    : AssuranceClaimStatus.Failed,
                "Unity lifecycle is ready after compile observation.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = "unityLifecycle",
                    ["runId"] = summary.RunId,
                    ["lifecycleState"] = summary.Lifecycle.State?.LifecycleState,
                },
                [
                    new CompileEvidenceOutput(
                        Kind: CompileEvidenceKind.LifecycleSnapshot,
                        EvidenceRef: null,
                        Data: compileOutput.Lifecycle),
                ]),
        ];
    }

    private static CompileClaimOutput CreateClaim (
        UcliCode id,
        AssuranceClaimStatus status,
        string statement,
        IReadOnlyDictionary<string, object?> subject,
        IReadOnlyList<CompileEvidenceOutput> evidence)
    {
        return new CompileClaimOutput(
            Id: id,
            Status: status,
            Coverage: AssuranceCoverage.Full,
            Required: true,
            VerifierRef: VerifierId,
            Statement: status == AssuranceClaimStatus.Passed
                ? statement
                : statement.Replace(" completed ", " did not complete ", StringComparison.Ordinal),
            Subject: subject,
            Evidence: evidence,
            ResidualRisks: EmptyResidualRisks);
    }

    private static AssuranceClaimStatus ResolveCompileNoErrorsStatus (IpcCompileSummary summary)
    {
        return summary.ScriptCompilation.Completed
            && summary.ScriptCompilation.Diagnostics.ErrorCount == 0
            ? AssuranceClaimStatus.Passed
            : AssuranceClaimStatus.Failed;
    }

    private static bool TryCreateDiagnosticsReadSummary (
        StartupFailureDetail? startupFailure,
        ProjectIdentityInfo project,
        Guid runId,
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
            && primaryDiagnostic.Kind == DaemonDiagnosisPrimaryDiagnosticKind.Compiler;
        if (!isCompilerDiagnosis
            && diagnosis.Reason != DaemonDiagnosisReason.UnityScriptCompilationFailed)
        {
            return false;
        }

        var startup = startupFailure!.Startup;
        var observedAtUtc = diagnosis.UpdatedAtUtc;
        var startedAtUtc = startup?.StartedAtUtc ?? observedAtUtc;
        var compileDiagnostic = primaryDiagnostic is null
            ? new IpcPrimaryDiagnostic(
                Kind: DaemonDiagnosisPrimaryDiagnosticKind.Compiler,
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
                Origin: CompileRefreshOrigin.DiagnosticsRead,
                Requested: false,
                StartedAtUtc: startedAtUtc,
                CompletedAtUtc: observedAtUtc,
                Completed: true),
            ScriptCompilation: new IpcCompileSummary.ScriptCompilationEvidence(
                Started: false,
                Completed: true,
                CompileGenerationBefore: null,
                CompileGenerationAfter: null,
                Diagnostics: new IpcCompileSummary.DiagnosticsEvidence(
                    ErrorCount: 1,
                    WarningCount: 0,
                    PrimaryDiagnostic: compileDiagnostic)),
            DomainReload: new IpcCompileSummary.DomainReloadEvidence(
                ReloadRequired: false,
                ReloadObserved: false,
                GenerationBefore: null,
                GenerationAfter: null,
                Settled: true),
            Lifecycle: new IpcCompileSummary.LifecycleEvidence(
                ServerVersion: null,
                UnityVersion: project.UnityVersion,
                State: null,
                ObservedAtUtc: observedAtUtc,
                ActionRequired: DaemonDiagnosisActionRequired.FixCompileErrors,
                PrimaryDiagnostic: compileDiagnostic));
        return true;
    }

    private static ApplicationFailure? ValidateSummary (
        IpcCompileSummary summary,
        Guid expectedRunId,
        ProjectFingerprint expectedProjectFingerprint,
        bool requireCompleted)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(expectedProjectFingerprint);

        if (summary.RunId != expectedRunId)
        {
            return ApplicationFailure.InternalError(
                $"Unity compile summary runId mismatch. Requested={expectedRunId}, Actual={summary.RunId}.");
        }

        if (summary.ProjectFingerprint != expectedProjectFingerprint)
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

        if (summary.ScriptCompilation.CompileGenerationBefore is < 0
            || summary.ScriptCompilation.CompileGenerationAfter is < 0
            || summary.DomainReload.GenerationBefore is < 0
            || summary.DomainReload.GenerationAfter is < 0)
        {
            return ApplicationFailure.InternalError("Unity compile summary generation values must not be negative.");
        }

        return null;
    }

    private static AssuranceVerdict RecalculateVerdict (IReadOnlyList<CompileClaimOutput> claims)
    {
        var claimStates = new AssuranceVerdictClaimState[claims.Count];
        for (var i = 0; i < claims.Count; i++)
        {
            var claim = claims[i];
            claimStates[i] = new AssuranceVerdictClaimState(
                claim.Status,
                claim.Coverage,
                claim.Required,
                claim.ResidualRisks.Any(static risk => risk.Blocking));
        }

        return AssuranceVerdictCalculator.Calculate(
            claimStates,
            Array.Empty<AssuranceVerdictResidualRiskState>());
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
        ApplicationFailure? FailureInfo,
        int PollAttempts)
    {
        public bool IsSuccess => Summary != null && FailureInfo == null;

        public static CompileSummaryPollResult Success (
            IpcCompileSummary summary,
            int pollAttempts)
        {
            ArgumentNullException.ThrowIfNull(summary);
            return new CompileSummaryPollResult(summary, null, pollAttempts);
        }

        public static CompileSummaryPollResult Failure (
            ApplicationFailure failure,
            int pollAttempts)
        {
            ArgumentNullException.ThrowIfNull(failure);
            return new CompileSummaryPollResult(null, failure, pollAttempts);
        }
    }
}
