using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Vocabulary;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Execution;

/// <summary> Executes compile assurance through the typed compile Lifecycle Execution handler. </summary>
internal sealed class CompileService : ICompileService
{
    internal static readonly AssuranceVerifierId VerifierId = new("compile");

    private static readonly IReadOnlyList<CompileResidualRiskOutput> EmptyResidualRisks =
        Array.Empty<CompileResidualRiskOutput>();

    private static readonly LifecycleExecutionDefinition Definition =
        new(LifecycleExecutionKind.Compile);

    private readonly IProjectContextResolver projectContextResolver;

    private readonly IUnityExecutionModeDecisionService executionModeDecisionService;

    private readonly IUnityRequestExecutor unityRequestExecutor;

    private readonly ILifecycleExecutionReconnectResolver reconnectResolver;

    private readonly ILifecycleExecutionHostExitTerminalizer hostExitTerminalizer;

    private readonly LifecycleExecutionRegistrationIssuer registrationIssuer;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="CompileService" /> class. </summary>
    public CompileService (
        IProjectContextResolver projectContextResolver,
        IUnityExecutionModeDecisionService executionModeDecisionService,
        IUnityRequestExecutor unityRequestExecutor,
        ILifecycleExecutionReconnectResolver reconnectResolver,
        ILifecycleExecutionHostExitTerminalizer hostExitTerminalizer,
        LifecycleExecutionRegistrationIssuer registrationIssuer,
        TimeProvider timeProvider)
    {
        this.projectContextResolver = projectContextResolver
            ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.executionModeDecisionService = executionModeDecisionService
            ?? throw new ArgumentNullException(nameof(executionModeDecisionService));
        this.unityRequestExecutor = unityRequestExecutor
            ?? throw new ArgumentNullException(nameof(unityRequestExecutor));
        this.reconnectResolver = reconnectResolver
            ?? throw new ArgumentNullException(nameof(reconnectResolver));
        this.hostExitTerminalizer = hostExitTerminalizer
            ?? throw new ArgumentNullException(nameof(hostExitTerminalizer));
        this.registrationIssuer = registrationIssuer
            ?? throw new ArgumentNullException(nameof(registrationIssuer));
        this.timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
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
        var contextResult = await projectContextResolver.ResolveAsync(
                input.ProjectPath,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return CompileExecutionResult.Failed(
                contextResult.Error!,
                project: null,
                lifecycleExecutionRef: null,
                ExecutionApplicationState.NotApplied);
        }

        var context = contextResult.Context!;
        var project = ProjectIdentityInfo.From(context.UnityProject);
        var timeoutResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            UcliCommandIds.Compile,
            context.Config);
        if (!timeoutResult.IsSuccess)
        {
            return CompileExecutionResult.Failed(
                timeoutResult.Error!,
                project,
                lifecycleExecutionRef: null,
                ExecutionApplicationState.NotApplied);
        }

        var timeout = timeoutResult.Timeout!.Value;
        var requestedMode = input.Mode ?? UnityExecutionMode.Auto;
        var executionDeadline = ExecutionDeadline.Start(timeout, timeProvider);
        if (!executionDeadline.TryGetRemainingTimeout(out var modeDecisionTimeout))
        {
            return CompileExecutionResult.Failed(
                CreateTimeoutFailure(timeout),
                project,
                lifecycleExecutionRef: null,
                ExecutionApplicationState.NotApplied);
        }

        var modeDecisionResult = await executionModeDecisionService.DecideAsync(
                requestedMode,
                context.UnityProject,
                modeDecisionTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!modeDecisionResult.IsSuccess)
        {
            var failure = modeDecisionResult.HasContractError
                ? ApplicationFailure.EnvironmentError(
                    modeDecisionResult.ContractError!.Message,
                    modeDecisionResult.ContractError.Code,
                    instancePath: null)
                : ApplicationFailure.FromExecutionError(modeDecisionResult.Error!);
            return CompileExecutionResult.Failed(
                failure,
                project,
                lifecycleExecutionRef: null,
                ExecutionApplicationState.NotApplied);
        }

        if (!executionDeadline.TryGetRemainingTimeout(out var requestTimeout))
        {
            return CompileExecutionResult.Failed(
                CreateTimeoutFailure(timeout),
                project,
                lifecycleExecutionRef: null,
                ExecutionApplicationState.NotApplied);
        }

        var executionTarget = modeDecisionResult.Decision!.Target;
        if (!registrationIssuer.TryIssueBeforeDeadline(
                Definition,
                executionDeadline.UtcDeadline,
                out var registration))
        {
            return CompileExecutionResult.Failed(
                CreateTimeoutFailure(timeout),
                project,
                lifecycleExecutionRef: null,
                ExecutionApplicationState.NotApplied);
        }

        await EmitStartedAsync(
                resolvedProgressSink,
                registration.ExecutionId,
                project,
                requestedMode,
                executionTarget,
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        return await ExecuteRegisteredAsync(
                context,
                project,
                registration,
                UnityExecutionTargetModeMapper.ToExplicitMode(
                    executionTarget),
                requestTimeout,
                resolvedProgressSink,
                reconnectedExecutionRef: null,
                requiredStart: null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<CompileExecutionResult> ReconnectAsync (
        CompileCommandInput input,
        ExecutionRef lifecycleExecutionRef,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(lifecycleExecutionRef);
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await projectContextResolver.ResolveAsync(
                input.ProjectPath,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return CompileExecutionResult.Failed(
                contextResult.Error!,
                project: null,
                lifecycleExecutionRef: null,
                ExecutionApplicationState.NotApplied);
        }

        var context = contextResult.Context!;
        var project = ProjectIdentityInfo.From(context.UnityProject);
        var timeoutResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            UcliCommandIds.Compile,
            context.Config);
        if (!timeoutResult.IsSuccess)
        {
            return CompileExecutionResult.Failed(
                timeoutResult.Error!,
                project,
                lifecycleExecutionRef: null,
                ExecutionApplicationState.NotApplied);
        }

        var reconnectResult = await reconnectResolver.ResolveAsync(
                context.UnityProject,
                Definition,
                lifecycleExecutionRef,
                cancellationToken)
            .ConfigureAwait(false);
        if (reconnectResult
            is LifecycleExecutionReconnectResolution.PublicationFailed
                publicationFailed)
        {
            return CompileExecutionResult.Failed(
                publicationFailed.Failure,
                project,
                publicationFailed.CurrentReference,
                ExecutionApplicationState.Indeterminate);
        }
        if (reconnectResult
            is LifecycleExecutionReconnectResolution.Rejected rejected)
        {
            return CompileExecutionResult.Failed(
                rejected.Failure,
                project,
                lifecycleExecutionRef: null,
                ExecutionApplicationState.NotApplied);
        }
        if (reconnectResult
            is LifecycleExecutionReconnectResolution.Terminal terminal)
        {
            return await CreateResultFromTerminalRecordAsync(
                    project,
                    terminal.ExecutionReference,
                    terminal.TerminalRecord,
                    progressSink ?? NullCommandProgressSink.Instance)
                .ConfigureAwait(false);
        }

        var open =
            (LifecycleExecutionReconnectResolution.Open)reconnectResult;
        try
        {
            return await ExecuteRegisteredAsync(
                    context,
                    project,
                    open.Registration,
                    UnityExecutionMode.Auto,
                    timeoutResult.Timeout!.Value,
                    progressSink ?? NullCommandProgressSink.Instance,
                    open.CurrentReference,
                    open.RequiredStart,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return CompileExecutionResult.Failed(
                ApplicationFailure.Canceled(
                    "Waiting for the reconnected Unity compile execution was canceled.",
                    ExecutionErrorCodes.Canceled),
                project,
                open.CurrentReference,
                ExecutionApplicationState.Unknown);
        }
    }

    private async ValueTask<CompileExecutionResult> ExecuteRegisteredAsync (
        ProjectContext context,
        ProjectIdentityInfo project,
        LifecycleExecutionRegistration registration,
        UnityExecutionMode executionMode,
        TimeSpan requestTimeout,
        ICommandProgressSink progressSink,
        ExecutionRef? reconnectedExecutionRef,
        LifecycleExecutionStartBinding? requiredStart,
        CancellationToken cancellationToken)
    {
        var executionResult = await unityRequestExecutor.ExecuteAsync(
                UcliCommandIds.Compile,
                executionMode,
                LifecycleExecutionTiming.AddResponseDeliveryGrace(requestTimeout),
                context.Config,
                context.UnityProject,
                new UnityRequestPayload.Compile(
                    registration,
                    requiredStart),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            if (executionResult.ConfirmedHostExit is not null)
            {
                var start = executionResult.LifecycleExecutionStart!;
                var currentReference =
                    reconnectedExecutionRef
                    ?? start.LifecycleExecutionRef;
                var terminalFacts =
                    LifecycleExecutionTerminalFactsPolicy.ResolveHostExit(
                        start,
                        currentReference,
                        executionResult.LifecycleActionDispatched,
                        timeProvider.GetUtcNow());
                var terminalization =
                    await hostExitTerminalizer.TerminalizeAsync(
                            context.UnityProject,
                            start,
                            currentReference,
                            terminalFacts,
                            CreateHostExitTerminalRecord)
                        .ConfigureAwait(false);
                if (terminalization
                    is LifecycleExecutionHostExitTerminalizationResult
                        .PublicationFailed publicationFailed)
                {
                    var fixedCompileRecord =
                        publicationFailed.FixedTerminalRecord
                            as CompileLifecycleExecutionTerminalRecord;
                    return CompileExecutionResult.Failed(
                        publicationFailed.Failure,
                        project,
                        publicationFailed.ExecutionReference,
                        publicationFailed.ApplicationState,
                        fixedCompileRecord?.Result,
                        observedLifecycle: null);
                }

                var published =
                    (LifecycleExecutionHostExitTerminalizationResult.Published)
                        terminalization;
                return await CreateResultFromTerminalRecordAsync(
                        project,
                        published.ExecutionReference,
                        published.TerminalRecord,
                        progressSink)
                    .ConfigureAwait(false);
            }

            var waitFailure = LifecycleExecutionWaitFailure.Resolve(
                durableStartExecutionReference: executionResult
                    .LifecycleExecutionStart
                    ?.LifecycleExecutionRef,
                isCallerCancellation: executionResult
                    .FailureInfo!.Code
                    == ExecutionErrorCodes.Canceled,
                lifecycleActionDispatched:
                    executionResult.LifecycleActionDispatched,
                establishedExecutionReference:
                    reconnectedExecutionRef);
            return CompileExecutionResult.Failed(
                NormalizeWaitFailure(executionResult.FailureInfo!),
                project,
                waitFailure.ExecutionReference,
                waitFailure.ApplicationState);
        }

        var response = executionResult.Response!;
        if (response.Errors.Count != 0
            && executionResult.LifecycleExecutionStart is null)
        {
            return CompileExecutionResult.Failed(
                RequestFailureNormalizer.FromOperationError(
                    response.Errors[0]),
                project,
                reconnectedExecutionRef,
                reconnectedExecutionRef == null
                    ? ExecutionApplicationState.NotApplied
                    : ExecutionApplicationState.Unknown);
        }

        if (response.Errors.Count != 0)
        {
            return CreateFailureFromResponse(
                project,
                registration,
                executionResult,
                response,
                reconnectedExecutionRef);
        }

        if (!IpcPayloadCodec.TryDeserialize(
                response.Payload,
                out IpcCompileResponse compileResponse,
                out var payloadError))
        {
            return CreateInvalidPayloadFailure(
                project,
                executionResult,
                reconnectedExecutionRef,
                $"Unity compile payload is invalid. {payloadError.Message}");
        }

        if (!registration.HasSameIdentity(
                compileResponse.LifecycleExecutionRef))
        {
            return CreateInvalidPayloadFailure(
                project,
                executionResult,
                reconnectedExecutionRef,
                "Unity compile response identifies a different Lifecycle Execution.");
        }

        var terminalResolution = await reconnectResolver.ResolveAsync(
                context.UnityProject,
                Definition,
                compileResponse.LifecycleExecutionRef,
                CancellationToken.None)
            .ConfigureAwait(false);
        if (terminalResolution
            is LifecycleExecutionReconnectResolution.PublicationFailed
                terminalPublicationFailed)
        {
            return CompileExecutionResult.Failed(
                terminalPublicationFailed.Failure,
                project,
                terminalPublicationFailed.CurrentReference,
                ExecutionApplicationState.Applied,
                compileResponse.Result,
                observedLifecycle: null);
        }
        if (terminalResolution
            is LifecycleExecutionReconnectResolution.Rejected rejected)
        {
            var retainedReference =
                reconnectedExecutionRef
                ?? executionResult.LifecycleExecutionStart
                    ?.LifecycleExecutionRef;
            return CompileExecutionResult.Failed(
                rejected.Failure,
                project,
                retainedReference,
                retainedReference is null
                    ? ExecutionApplicationState.NotApplied
                    : ExecutionApplicationState.Applied,
                retainedReference is null
                    ? null
                    : compileResponse.Result,
                observedLifecycle: null);
        }
        if (terminalResolution
                is not LifecycleExecutionReconnectResolution.Terminal terminal
            || terminal.TerminalRecord
                is not CompileLifecycleExecutionTerminalRecord compileRecord)
        {
            return CreateInvalidPayloadFailure(
                project,
                executionResult,
                reconnectedExecutionRef,
                "Unity compile success did not resolve a typed compile Terminal Record.");
        }
        if (!Equals(compileResponse.Result, compileRecord.Result))
        {
            return CompileExecutionResult.Failed(
                ApplicationFailure.InternalError(
                    "Unity compile result does not match its reverified Terminal Record.",
                    UcliCoreErrorCodes.InternalError,
                    instancePath: null,
                    startupFailure: null),
                project,
                LifecycleExecutionFailureReferenceProjection
                    .CreatePublishing(terminal.ExecutionReference),
                ExecutionApplicationState.Applied,
                compileResponse.Result,
                observedLifecycle: null);
        }

        return await CreateResultFromTerminalRecordAsync(
                project,
                terminal.ExecutionReference,
                compileRecord,
                progressSink)
            .ConfigureAwait(false);
    }

    private static async ValueTask<CompileExecutionResult>
        CreateResultFromTerminalRecordAsync (
            ProjectIdentityInfo project,
            ExecutionRef executionReference,
            LifecycleExecutionTerminalRecord terminalRecord,
            ICommandProgressSink progressSink)
    {
        if (executionReference is not TerminalExecutionRef terminalReference
            || terminalRecord
                is not CompileLifecycleExecutionTerminalRecord compileRecord)
        {
            return CompileExecutionResult.Failed(
                ApplicationFailure.InternalError(
                    "Compile reconnection did not resolve a typed compile Terminal Record."),
                project,
                executionReference,
                ExecutionApplicationState.Indeterminate);
        }

        if (compileRecord.TerminalReason
            != LifecycleExecutionTerminalReason.Completed)
        {
            return CompileExecutionResult.Failed(
                CreateTerminalFailure(compileRecord.TerminalReason),
                project,
                terminalReference,
                compileRecord.ApplicationState,
                compileRecord.Result,
                observedLifecycle: null);
        }

        var output = CreateOutput(
            project,
            terminalReference,
            compileRecord.Result!,
            compileRecord.Verdict!.Value);
        await EmitCompletedAsync(
                progressSink,
                output,
                CancellationToken.None)
            .ConfigureAwait(false);
        return CompileExecutionResult.Completed(output);
    }

    private static LifecycleExecutionTerminalRecord
        CreateHostExitTerminalRecord (
            LifecycleExecutionStartBinding start,
            LifecycleExecutionTerminalFacts terminalFacts)
    {
        return new CompileLifecycleExecutionTerminalRecord(
            start.LifecycleExecutionRef.Id,
            start.LifecycleExecutionRef.DefinitionDigest,
            start.Project,
            start.Host,
            start.StartedGeneration,
            terminalGeneration: null,
            start.DeadlineUtc,
            start.StartedAtUtc,
            terminalFacts.CompletedAtUtc,
            terminalFacts.TerminalReason,
            terminalFacts.ApplicationState,
            result: null,
            verdict: null,
            Array.Empty<ArtifactRef>());
    }

    private static ApplicationFailure CreateTerminalFailure (
        LifecycleExecutionTerminalReason terminalReason)
    {
        return terminalReason switch
        {
            LifecycleExecutionTerminalReason.ActionFailed =>
                ApplicationFailure.InternalError(
                    "Unity compile action ended with an explicit failure."),
            LifecycleExecutionTerminalReason.DeadlineExceeded =>
                ApplicationFailure.Timeout(
                    "Compile reached its durable execution deadline.",
                    LifecycleExecutionErrorCodes.DeadlineExceeded),
            LifecycleExecutionTerminalReason.ProjectMismatch =>
                ApplicationFailure.ContractViolation(
                    "Compile recovery project does not match its durable start.",
                    LifecycleExecutionErrorCodes.ProjectMismatch),
            LifecycleExecutionTerminalReason.HostMismatch =>
                ApplicationFailure.ContractViolation(
                    "Compile recovery host does not match its durable start.",
                    LifecycleExecutionErrorCodes.HostMismatch),
            LifecycleExecutionTerminalReason.GenerationMismatch =>
                ApplicationFailure.ContractViolation(
                    "Compile recovery generation was not a proven successor.",
                    LifecycleExecutionErrorCodes.GenerationMismatch),
            LifecycleExecutionTerminalReason.UnityExited =>
                ApplicationFailure.ExternalProcessFailure(
                    "The Unity Editor hosting compile exited before completion.",
                    LifecycleExecutionErrorCodes.UnityExited),
            _ => throw new ArgumentOutOfRangeException(
                nameof(terminalReason),
                terminalReason,
                "Completed compile Terminal Records are projected as success."),
        };
    }

    private static CompileExecutionResult CreateFailureFromResponse (
        ProjectIdentityInfo project,
        LifecycleExecutionRegistration registration,
        UnityRequestExecutionResult executionResult,
        UnityRequestResponse response,
        ExecutionRef? reconnectedExecutionRef)
    {
        if (!IpcPayloadCodec.TryDeserialize(
                response.Payload,
                out IpcCompileErrorResponse errorResponse,
                out var payloadError))
        {
            return CreateInvalidPayloadFailure(
                project,
                executionResult,
                reconnectedExecutionRef,
                $"Unity compile error payload is invalid. {payloadError.Message}");
        }

        if (errorResponse.LifecycleExecutionRef != null
            && !registration.HasSameIdentity(
                errorResponse.LifecycleExecutionRef))
        {
            return CreateInvalidPayloadFailure(
                project,
                executionResult,
                reconnectedExecutionRef,
                "Unity compile error response identifies a different Lifecycle Execution.");
        }

        var retainedReference =
            errorResponse.LifecycleExecutionRef
            ?? reconnectedExecutionRef;
        return CompileExecutionResult.Failed(
            RequestFailureNormalizer.FromOperationError(response.Errors[0]),
            project,
            retainedReference,
            errorResponse.LifecycleExecutionRef == null
                && reconnectedExecutionRef != null
                    ? ExecutionApplicationState.Unknown
                    : errorResponse.ApplicationState,
            errorResponse.Result,
            errorResponse.ObservedLifecycle);
    }

    private static CompileExecutionResult CreateInvalidPayloadFailure (
        ProjectIdentityInfo project,
        UnityRequestExecutionResult executionResult,
        ExecutionRef? reconnectedExecutionRef,
        string message)
    {
        var retainedReference =
            executionResult.LifecycleExecutionStart?.LifecycleExecutionRef
            ?? reconnectedExecutionRef;
        return CompileExecutionResult.Failed(
            ApplicationFailure.InternalError(
                message,
                UcliCoreErrorCodes.InternalError,
                instancePath: null,
                startupFailure: null),
            project,
            retainedReference,
            retainedReference == null
                ? ExecutionApplicationState.NotApplied
                : ExecutionApplicationState.Unknown);
    }

    private static ValueTask EmitStartedAsync (
        ICommandProgressSink progressSink,
        Guid executionId,
        ProjectIdentityInfo project,
        UnityExecutionMode requestedMode,
        UnityExecutionTarget executionTarget,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        return progressSink.OnEntryAsync(
            CompileProgressEventNames.Started,
            new CompileStartedEntry(
                ExecutionId: executionId,
                ProjectFingerprint: project.ProjectFingerprint,
                RequestedMode: AssuranceExecutionModeCodec.ToRequestedMode(requestedMode),
                ResolvedMode: AssuranceExecutionModeCodec.ToResolvedMode(executionTarget),
                SessionKind: AssuranceExecutionModeCodec.ToSessionKind(executionTarget),
                TimeoutMilliseconds: checked((int)timeout.TotalMilliseconds)),
            cancellationToken);
    }

    private static ValueTask EmitCompletedAsync (
        ICommandProgressSink progressSink,
        CompileExecutionOutput output,
        CancellationToken cancellationToken)
    {
        return progressSink.OnEntryAsync(
            CompileProgressEventNames.Completed,
            new CompileCompletedEntry(
                ExecutionId: output.LifecycleExecutionRef.Id,
                Verdict: output.Verdict,
                ErrorCount: output.Compile.ScriptCompilation.Diagnostics.ErrorCount,
                WarningCount: output.Compile.ScriptCompilation.Diagnostics.WarningCount),
            cancellationToken);
    }

    private static CompileExecutionOutput CreateOutput (
        ProjectIdentityInfo project,
        ExecutionRef lifecycleExecutionRef,
        CompileLifecycleResult result,
        Verdict verdict)
    {
        var compileOutput = CreateCompileOutput(result);
        var claims = CreateClaims(
            lifecycleExecutionRef.Id,
            result,
            compileOutput);
        var terminalRecordReport = CreateTerminalRecordReportReference(
            ((TerminalExecutionRef)lifecycleExecutionRef).TerminalRecordRef);
        return new CompileExecutionOutput(
            Project: project,
            LifecycleExecutionRef: (ITerminalExecutionRef)lifecycleExecutionRef,
            Verdict: verdict,
            Verifiers:
            [
                new CompileVerifierOutput(
                    Id: VerifierId,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: CompileClaimCodes.All,
                    Effects: AssuranceEffectSets.Compile,
                    ReportRef: AssuranceReportIds.CompileSummary),
            ],
            Claims: claims,
            Reports: new Dictionary<string, AssuranceReportReference>(StringComparer.Ordinal)
            {
                [AssuranceReportIds.CompileSummary.Value] = terminalRecordReport,
                [AssuranceReportIds.CompileDiagnostics.Value] = terminalRecordReport,
            },
            ResidualRisks: EmptyResidualRisks,
            Compile: compileOutput);
    }

    private static CompileOutput CreateCompileOutput (CompileLifecycleResult result)
    {
        var state = result.Lifecycle.State;
        return new CompileOutput(
            refresh: new CompileRefreshOutput(
                Origin: result.Refresh.Origin,
                Requested: result.Refresh.Requested,
                StartedAtUtc: result.Refresh.StartedAtUtc,
                CompletedAtUtc: result.Refresh.CompletedAtUtc,
                Completed: result.Refresh.Completed),
            scriptCompilation: new CompileScriptCompilationOutput(
                Started: result.ScriptCompilation.Started,
                Completed: result.ScriptCompilation.Completed,
                CompileGenerationBefore: result.ScriptCompilation.CompileGenerationBefore,
                CompileGenerationAfter: result.ScriptCompilation.CompileGenerationAfter,
                Diagnostics: new CompileDiagnosticsOutput(
                    ErrorCount: result.ScriptCompilation.Diagnostics.ErrorCount,
                    WarningCount: result.ScriptCompilation.Diagnostics.WarningCount,
                    PrimaryDiagnostic: CreatePrimaryDiagnosticOutput(
                        result.ScriptCompilation.Diagnostics.PrimaryDiagnostic))),
            domainReload: new CompileDomainReloadOutput(
                ReloadRequired: result.DomainReload.ReloadRequired,
                ReloadObserved: result.DomainReload.ReloadObserved,
                GenerationBefore: result.DomainReload.GenerationBefore,
                GenerationAfter: result.DomainReload.GenerationAfter,
                Settled: result.DomainReload.Settled),
            lifecycle: new CompileLifecycleOutput(
                ServerVersion: result.Lifecycle.ServerVersion,
                UnityVersion: result.Lifecycle.UnityVersion,
                EditorMode: state?.EditorMode,
                LifecycleState: state?.LifecycleState,
                BlockingReason: state is not null
                    ? UnityEditorLifecycleSemantics.ResolveBlockingReason(state.LifecycleState)
                    : null,
                CompileState: state?.CompileState,
                Generations: state?.Generations,
                CanAcceptExecutionRequests: state is not null
                    && UnityEditorLifecycleSemantics.CanAcceptExecutionRequests(state.LifecycleState),
                ObservedAtUtc: result.Lifecycle.ObservedAtUtc,
                ActionRequired: result.Lifecycle.ActionRequired,
                PrimaryDiagnostic: CreatePrimaryDiagnosticOutput(
                    result.Lifecycle.PrimaryDiagnostic)));
    }

    private static CompilePrimaryDiagnosticOutput? CreatePrimaryDiagnosticOutput (
        UnityEditorPrimaryDiagnostic? diagnostic)
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
        Guid executionId,
        CompileLifecycleResult result,
        CompileOutput compileOutput)
    {
        return
        [
            CreateClaim(
                CompileClaimCodes.UnityCompileNoErrors,
                ResolveCompileNoErrorsStatus(result),
                "Unity script compilation completed without compiler errors.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = "unityCompile",
                    ["executionId"] = executionId,
                },
                [
                    CompileScriptEvidenceOutput.Create(
                        AssuranceReportIds.CompileDiagnostics,
                        compileOutput.ScriptCompilation),
                ]),
            CreateClaim(
                CompileClaimCodes.UnityDomainReloadSettled,
                result.DomainReload.Settled
                    ? AssuranceClaimStatus.Passed
                    : AssuranceClaimStatus.Failed,
                "Unity domain reload reached a settled state after compile observation.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = "unityDomainReload",
                    ["executionId"] = executionId,
                },
                [
                    CompileDomainReloadEvidenceOutput.Create(compileOutput.DomainReload),
                ]),
            CreateClaim(
                CompileClaimCodes.UnityLifecycleReadyAfterCompile,
                result.Lifecycle.State is not null
                    && UnityEditorLifecycleSemantics.CanAcceptExecutionRequests(
                        result.Lifecycle.State.LifecycleState)
                    ? AssuranceClaimStatus.Passed
                    : AssuranceClaimStatus.Failed,
                "Unity lifecycle is ready after compile observation.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = "unityLifecycle",
                    ["executionId"] = executionId,
                    ["lifecycleState"] = result.Lifecycle.State?.LifecycleState,
                },
                [
                    CompileLifecycleEvidenceOutput.Create(compileOutput.Lifecycle),
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
                : statement.Replace(
                    " completed ",
                    " did not complete ",
                    StringComparison.Ordinal),
            Subject: subject,
            Evidence: evidence,
            ResidualRisks: EmptyResidualRisks);
    }

    private static AssuranceClaimStatus ResolveCompileNoErrorsStatus (
        CompileLifecycleResult result)
    {
        return result.ScriptCompilation.Completed
            && result.ScriptCompilation.Diagnostics.ErrorCount == 0
            ? AssuranceClaimStatus.Passed
            : AssuranceClaimStatus.Failed;
    }

    private static AssuranceReportReference CreateTerminalRecordReportReference (
        ArtifactRef terminalRecordRef)
    {
        return terminalRecordRef switch
        {
            PathArtifactRef path => AssuranceReportReference.FromPath(
                path.Path.Value,
                path.Digest),
            PathAndUriArtifactRef pathAndUri => AssuranceReportReference.FromPath(
                pathAndUri.Path.Value,
                pathAndUri.Digest),
            UriArtifactRef uri => AssuranceReportReference.FromUri(
                uri.Uri.Value,
                uri.Digest),
            _ => throw new ArgumentOutOfRangeException(
                nameof(terminalRecordRef),
                terminalRecordRef.GetType(),
                "Compile terminal record artifact reference variant is unsupported."),
        };
    }

    private static ApplicationFailure CreateTimeoutFailure (TimeSpan timeout)
    {
        return ApplicationFailure.Timeout(
            $"Unity compile assurance timed out after {timeout.TotalMilliseconds:0} milliseconds.",
            ExecutionErrorCodes.IpcTimeout,
            instancePath: null,
            startupFailure: null);
    }

    private static ApplicationFailure NormalizeWaitFailure (
        UnityRequestFailure failure)
    {
        return failure.Code == ExecutionErrorCodes.Canceled
            ? ApplicationFailure.Canceled(
                failure.Message,
                failure.Code,
                instancePath: null)
            : RequestFailureNormalizer.FromUnityRequestFailure(failure);
    }

}
