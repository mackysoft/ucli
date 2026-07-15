using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Executes readiness probes and compiles the result into an assurance packet. </summary>
internal sealed class ReadyService : IReadyService
{
    private const string OpsCatalogReadinessActionRequired =
        "Run `ucli ops list --readIndexMode requireFresh` to refresh ops catalog readIndex artifacts.";

    private const string AssetLookupReadinessActionRequired =
        "Run `ucli query assets find --pathPrefix Assets --limit 1 --readIndexMode disabled` to refresh asset lookup readIndex artifacts.";

    private static readonly AssuranceVerifierId LifecycleVerifierId = new("ready.lifecycle");

    private static readonly AssuranceVerifierId ReadIndexVerifierId = new("ready.readIndex");

    private static readonly IReadOnlyDictionary<string, AssuranceReportReference> EmptyReports =
        new Dictionary<string, AssuranceReportReference>(StringComparer.Ordinal);

    private static readonly IReadOnlyList<ReadyResidualRiskOutput> EmptyResidualRisks =
        Array.Empty<ReadyResidualRiskOutput>();

    private readonly IProjectContextResolver projectContextResolver;

    private readonly IUnityExecutionModeDecisionService executionModeDecisionService;

    private readonly IDaemonPingInfoClient daemonPingInfoClient;

    private readonly IUnityRequestExecutor unityRequestExecutor;

    private readonly IReadIndexArtifactReader readIndexArtifactReader;

    private readonly IReadIndexFreshnessEvaluator readIndexFreshnessEvaluator;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="ReadyService" /> class. </summary>
    public ReadyService (
        IProjectContextResolver projectContextResolver,
        IUnityExecutionModeDecisionService executionModeDecisionService,
        IDaemonPingInfoClient daemonPingInfoClient,
        IUnityRequestExecutor unityRequestExecutor,
        IReadIndexArtifactReader readIndexArtifactReader,
        IReadIndexFreshnessEvaluator readIndexFreshnessEvaluator,
        TimeProvider timeProvider)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.executionModeDecisionService = executionModeDecisionService ?? throw new ArgumentNullException(nameof(executionModeDecisionService));
        this.daemonPingInfoClient = daemonPingInfoClient ?? throw new ArgumentNullException(nameof(daemonPingInfoClient));
        this.unityRequestExecutor = unityRequestExecutor ?? throw new ArgumentNullException(nameof(unityRequestExecutor));
        this.readIndexArtifactReader = readIndexArtifactReader ?? throw new ArgumentNullException(nameof(readIndexArtifactReader));
        this.readIndexFreshnessEvaluator = readIndexFreshnessEvaluator ?? throw new ArgumentNullException(nameof(readIndexFreshnessEvaluator));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async ValueTask<ReadyExecutionResult> ExecuteAsync (
        ReadyCommandInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        if (input.IsReadIndexModeSpecified && input.Target != ReadyTarget.ReadIndex)
        {
            return ReadyExecutionResult.Failure(ExecutionError.InvalidArgument(
                "--readIndexMode is only supported when --for readIndex."));
        }

        var contextResult = await projectContextResolver.ResolveAsync(input.ProjectPath, cancellationToken).ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return ReadyExecutionResult.Failure(contextResult.Error!);
        }

        var context = contextResult.Context!;
        var project = ProjectIdentityInfo.From(context.UnityProject);
        var timeoutResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            UcliCommandIds.Ready,
            context.Config);
        if (!timeoutResult.IsSuccess)
        {
            return ReadyExecutionResult.Failure(timeoutResult.Error!, project);
        }

        var timeout = timeoutResult.Timeout!.Value;
        var requestedMode = input.Mode ?? UnityExecutionMode.Auto;

        if (input.Target == ReadyTarget.ReadIndex)
        {
            if (input.Mode is UnityExecutionMode.Daemon or UnityExecutionMode.Oneshot)
            {
                return ReadyExecutionResult.Failure(ExecutionError.InvalidArgument(
                    "--mode daemon and --mode oneshot are not supported when --for readIndex because read-index readiness is artifact-only."),
                    project);
            }

            var readIndexModeResult = ResolveReadyReadIndexMode(input.ReadIndexMode, context.Config);
            if (!readIndexModeResult.IsSuccess)
            {
                return ReadyExecutionResult.Failure(readIndexModeResult.Error!, project);
            }

            using var readIndexTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readIndexTimeout.CancelAfter(timeout);

            ReadyReadIndexObservation readIndexObservation;
            try
            {
                readIndexObservation = await ObserveReadIndexAsync(
                        context.UnityProject,
                        readIndexModeResult.Mode!.Value,
                        readIndexTimeout.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return ReadyExecutionResult.Failure(CreateTimeoutFailure(timeout), project);
            }

            var readIndexOutput = CreateOutput(
                input.Target,
                requestedMode,
                ResolveReadIndexExecutionTarget(requestedMode),
                timeout,
                project,
                ReadyLifecycleProbeResult.Success(null, UnityReadinessDecision.Ready()),
                readIndexObservation);
            return ReadyExecutionResult.Success(readIndexOutput);
        }

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        if (!deadline.TryGetRemainingTimeout(out var modeDecisionTimeout))
        {
            return ReadyExecutionResult.Failure(CreateTimeoutFailure(timeout), project);
        }

        var modeDecisionResult = await executionModeDecisionService.DecideAsync(
                requestedMode,
                context.UnityProject,
                modeDecisionTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!modeDecisionResult.IsSuccess && !modeDecisionResult.HasContractError)
        {
            return ReadyExecutionResult.Failure(modeDecisionResult.Error!, project);
        }

        ReadyLifecycleProbeResult lifecycleProbeResult;
        UnityExecutionTarget executionTarget;
        if (modeDecisionResult.HasContractError)
        {
            var contractError = modeDecisionResult.ContractError!;
            return ReadyExecutionResult.Failure(
                ApplicationFailure.FromCode(
                    contractError.Code,
                    contractError.Message),
                project);
        }
        else
        {
            var decision = modeDecisionResult.Decision!;
            executionTarget = decision.Target;
            if (!deadline.TryGetRemainingTimeout(out var probeTimeout))
            {
                return ReadyExecutionResult.Failure(CreateTimeoutFailure(timeout), project);
            }

            lifecycleProbeResult = await ProbeLifecycleAsync(
                    context,
                    requestedMode,
                    decision.Target,
                    probeTimeout,
                    input.FailFast,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!lifecycleProbeResult.IsSuccess)
            {
                return ReadyExecutionResult.Failure(lifecycleProbeResult.Failure!, project);
            }
        }

        var output = CreateOutput(
            input.Target,
            requestedMode,
            executionTarget,
            timeout,
            project,
            lifecycleProbeResult,
            default);
        return ReadyExecutionResult.Success(output);
    }

    private async ValueTask<ReadyLifecycleProbeResult> ProbeLifecycleAsync (
        ProjectContext context,
        UnityExecutionMode requestedMode,
        UnityExecutionTarget executionTarget,
        TimeSpan timeout,
        bool failFast,
        CancellationToken cancellationToken)
    {
        return executionTarget switch
        {
            UnityExecutionTarget.Daemon => await ProbeDaemonLifecycleAsync(
                    context.UnityProject,
                    timeout,
                    failFast,
                    cancellationToken)
                .ConfigureAwait(false),
            UnityExecutionTarget.Oneshot => await ProbeOneshotLifecycleAsync(
                    context,
                    requestedMode,
                    timeout,
                    failFast,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(executionTarget), executionTarget, "Unsupported execution target."),
        };
    }

    private async ValueTask<ReadyLifecycleProbeResult> ProbeDaemonLifecycleAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        bool failFast,
        CancellationToken cancellationToken)
    {
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        ReadyLifecycleOutput? lastLifecycle = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                return CreateDaemonProbeTimeoutResult(lastLifecycle, timeout);
            }

            try
            {
                var pingResponse = await daemonPingInfoClient.PingAndReadAsync(
                        unityProject,
                        remainingTimeout,
                        validateProjectFingerprint: true,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                lastLifecycle = ReadyLifecycleOutputFactory.Create(pingResponse);
                var readinessDecision = UnityEditorReadinessPolicy.Evaluate(pingResponse, failFast);
                if (readinessDecision.IsReady || readinessDecision.IsFailure)
                {
                    return CreateReadinessDecisionProbeResult(lastLifecycle, readinessDecision);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TimeoutException)
            {
            }
            catch (Exception exception)
            {
                return ReadyLifecycleProbeResult.FailureResult(ApplicationFailure.UnityIpcFailure(
                    $"Failed while probing Unity daemon readiness. {exception.Message}"));
            }

            if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
            {
                return CreateDaemonProbeTimeoutResult(lastLifecycle, timeout);
            }

            await TimeProviderDelay.DelayAsync(
                    GetRetryDelay(remainingTimeout),
                    timeProvider,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async ValueTask<ReadyLifecycleProbeResult> ProbeOneshotLifecycleAsync (
        ProjectContext context,
        UnityExecutionMode requestedMode,
        TimeSpan timeout,
        bool failFast,
        CancellationToken cancellationToken)
    {
        var executionResult = await unityRequestExecutor.ExecuteAsync(
                UcliCommandIds.Ready,
                requestedMode == UnityExecutionMode.Auto ? UnityExecutionMode.Oneshot : requestedMode,
                timeout,
                context.Config,
                context.UnityProject,
                new UnityRequestPayload.Ping(
                    IpcPingClientVersions.Ready,
                    failFast),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            var failureInfo = executionResult.FailureInfo!;
            if (IsObservedEditorLifecycleFailure(failureInfo))
            {
                return ReadyLifecycleProbeResult.Success(
                    lifecycle: null,
                    UnityReadinessDecision.Failure(
                        failureInfo.Code,
                        failureInfo.Message));
            }

            return ReadyLifecycleProbeResult.FailureResult(ApplicationFailure.FromCode(
                failureInfo.Code,
                failureInfo.Message,
                startupFailure: failureInfo.StartupFailure));
        }

        var response = executionResult.Response!;
        if (response.Errors.Count != 0)
        {
            var firstError = response.Errors[0];
            return ReadyLifecycleProbeResult.FailureResult(ApplicationFailure.FromCode(
                firstError.Code,
                firstError.Message));
        }

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcUnityEditorObservation pingResponse, out var payloadError))
        {
            return ReadyLifecycleProbeResult.FailureResult(ApplicationFailure.InternalError(
                $"Unity ping payload is invalid. {payloadError.Message}"));
        }

        var lifecycle = ReadyLifecycleOutputFactory.Create(pingResponse);
        if (pingResponse.ProjectFingerprint != context.UnityProject.ProjectFingerprint)
        {
            return ReadyLifecycleProbeResult.FailureResult(ApplicationFailure.InternalError(
                $"Unity ready ping projectFingerprint mismatch. Requested={context.UnityProject.ProjectFingerprint}, Actual={pingResponse.ProjectFingerprint}."));
        }

        var readinessDecision = UnityEditorReadinessPolicy.Evaluate(pingResponse, failFast);
        return CreateReadinessDecisionProbeResult(lifecycle, readinessDecision);
    }

    private static ReadyLifecycleProbeResult CreateReadinessDecisionProbeResult (
        ReadyLifecycleOutput? lifecycle,
        UnityReadinessDecision readinessDecision)
    {
        if (!readinessDecision.IsFailure)
        {
            return ReadyLifecycleProbeResult.Success(lifecycle, readinessDecision);
        }

        if (readinessDecision.ErrorCode is null)
        {
            return ReadyLifecycleProbeResult.FailureResult(ApplicationFailure.InternalError(
                "Unity readiness decision did not provide an error code."));
        }

        var errorCode = readinessDecision.ErrorCode;
        if (UnityEditorReadinessPolicy.IsReadinessFailureCode(errorCode))
        {
            return ReadyLifecycleProbeResult.Success(lifecycle, readinessDecision);
        }

        return ReadyLifecycleProbeResult.FailureResult(ApplicationFailure.FromCode(
            errorCode,
            readinessDecision.ErrorMessage ?? "Unity readiness failed."));
    }

    private async ValueTask<ReadyReadIndexObservation> ObserveReadIndexAsync (
        ResolvedUnityProjectContext unityProject,
        ReadIndexMode mode,
        CancellationToken cancellationToken)
    {
        var artifacts = new List<ReadyReadIndexArtifactOutput>
        {
            await ReadArtifactAsync(
                    ReadyReadIndexArtifactName.OpsCatalog,
                    IndexFreshnessTarget.OpsCatalog,
                    cancellationToken => readIndexArtifactReader.ReadOpsCatalogAsync(unityProject, cancellationToken),
                    unityProject,
                    mode,
                    required: true,
                    actionRequired: OpsCatalogReadinessActionRequired,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false),
            await ReadArtifactAsync(
                    ReadyReadIndexArtifactName.AssetSearchLookup,
                    IndexFreshnessTarget.AssetSearchLookup,
                    cancellationToken => readIndexArtifactReader.ReadAssetSearchLookupAsync(unityProject, cancellationToken),
                    unityProject,
                    mode,
                    required: true,
                    actionRequired: AssetLookupReadinessActionRequired,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false),
            await ReadArtifactAsync(
                    ReadyReadIndexArtifactName.GuidPathLookup,
                    IndexFreshnessTarget.GuidPathLookup,
                    cancellationToken => readIndexArtifactReader.ReadGuidPathLookupAsync(unityProject, cancellationToken),
                    unityProject,
                    mode,
                    required: true,
                    actionRequired: AssetLookupReadinessActionRequired,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false),
        };

        return ReadyReadIndexObservation.FromArtifacts(mode, artifacts);
    }

    private async ValueTask<ReadyReadIndexArtifactOutput> ReadArtifactAsync<T> (
        ReadyReadIndexArtifactName name,
        IndexFreshnessTarget target,
        Func<CancellationToken, ValueTask<ReadIndexArtifactReadResult<T>>> readAsync,
        ResolvedUnityProjectContext unityProject,
        ReadIndexMode mode,
        bool required,
        string actionRequired,
        CancellationToken cancellationToken)
        where T : class, IReadIndexArtifactSnapshot
    {
        var result = await readAsync(cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return ReadyReadIndexArtifactOutput.Failed(
                name,
                required,
                result.Error!.Code,
                result.Error.Message,
                actionRequired);
        }

        var value = result.Value!;
        var freshnessResult = await readIndexFreshnessEvaluator.ObserveAsync(
                unityProject,
                target,
                value.SourceInputsHash,
                cancellationToken)
            .ConfigureAwait(false);
        if (!freshnessResult.IsSuccess)
        {
            return ReadyReadIndexArtifactOutput.FailedWithSnapshot(
                name,
                required,
                freshnessResult.Freshness,
                value.SourceInputsHash,
                value.GeneratedAtUtc,
                freshnessResult.Error!.Code,
                freshnessResult.Error.Message,
                actionRequired);
        }

        var constrainedFreshnessResult = IndexFreshnessPolicy.ApplyModeConstraint(mode, freshnessResult.Freshness);
        if (!constrainedFreshnessResult.IsSuccess)
        {
            return ReadyReadIndexArtifactOutput.FailedWithSnapshot(
                name,
                required,
                constrainedFreshnessResult.Freshness,
                value.SourceInputsHash,
                value.GeneratedAtUtc,
                constrainedFreshnessResult.Error!.Code,
                constrainedFreshnessResult.Error.Message,
                actionRequired);
        }

        return ReadyReadIndexArtifactOutput.Available(
            name,
            required,
            constrainedFreshnessResult.Freshness,
            value.SourceInputsHash,
            value.GeneratedAtUtc);
    }

    private static ReadyExecutionOutput CreateOutput (
        ReadyTarget target,
        UnityExecutionMode requestedMode,
        UnityExecutionTarget executionTarget,
        TimeSpan timeout,
        ProjectIdentityInfo project,
        ReadyLifecycleProbeResult lifecycleProbeResult,
        ReadyReadIndexObservation readIndexObservation)
    {
        var claimId = ReadyClaimCodes.ForTarget(target);
        var targetValue = ContractLiteralCodec.ToValue(target);
        var requestedModeValue = AssuranceExecutionModeCodec.ToRequestedMode(requestedMode);
        var resolvedMode = ResolveResolvedMode(target, executionTarget);
        var sessionKind = ResolveSessionKind(target, executionTarget);
        var validity = CreateValidity(target, executionTarget, lifecycleProbeResult.Decision.IsReady);
        var evidence = CreateEvidence(lifecycleProbeResult, readIndexObservation);
        var claimStatus = ResolveClaimStatus(lifecycleProbeResult.Decision, readIndexObservation);
        var coverage = ResolveClaimCoverage(readIndexObservation);
        var verdict = AssuranceVerdictCalculator.Calculate(
            [new AssuranceVerdictClaimState(claimStatus, coverage, Required: true, HasBlockingResidualRisk: false)],
            Array.Empty<AssuranceVerdictResidualRiskState>());
        var verifierId = target == ReadyTarget.ReadIndex ? ReadIndexVerifierId : LifecycleVerifierId;
        var claim = new ReadyClaimOutput(
            Id: claimId,
            Status: claimStatus,
            Coverage: coverage,
            Required: true,
            VerifierRef: verifierId,
            Statement: CreateStatement(target, claimStatus),
            Subject: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["kind"] = "unityReady",
                ["target"] = targetValue,
                ["requestedMode"] = ContractLiteralCodec.ToValue(requestedModeValue),
                ["resolvedMode"] = ContractLiteralCodec.ToValue(resolvedMode),
                ["sessionKind"] = sessionKind,
            },
            Validity: validity,
            Evidence: evidence,
            ResidualRisks: EmptyResidualRisks);

        return new ReadyExecutionOutput(
            Verdict: verdict,
            Project: project,
            Verifiers:
            [
                new ReadyVerifierOutput(
                    Id: verifierId,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: [claimId]),
            ],
            Claims: [claim],
            Reports: EmptyReports,
            ResidualRisks: EmptyResidualRisks,
            Target: target,
            RequestedMode: requestedModeValue,
            ResolvedMode: resolvedMode,
            SessionKind: sessionKind,
            TimeoutMilliseconds: checked((int)timeout.TotalMilliseconds),
            Lifecycle: lifecycleProbeResult.Lifecycle,
            ReadIndex: readIndexObservation.Output);
    }

    private static AssuranceResolvedExecutionMode ResolveResolvedMode (
        ReadyTarget target,
        UnityExecutionTarget executionTarget)
    {
        return target == ReadyTarget.ReadIndex
            ? AssuranceResolvedExecutionMode.NotApplicable
            : AssuranceExecutionModeCodec.ToResolvedMode(executionTarget);
    }

    private static AssuranceSessionKind ResolveSessionKind (
        ReadyTarget target,
        UnityExecutionTarget executionTarget)
    {
        return target == ReadyTarget.ReadIndex
            ? AssuranceSessionKind.ArtifactOnly
            : AssuranceExecutionModeCodec.ToSessionKind(executionTarget);
    }

    private static ReadyClaimValidityOutput CreateValidity (
        ReadyTarget target,
        UnityExecutionTarget executionTarget,
        bool isReady)
    {
        if (target == ReadyTarget.ReadIndex)
        {
            return new ReadyClaimValidityOutput(ReadyValidityKind.ProbeOnly, GuaranteesReusableSession: false);
        }

        return executionTarget == UnityExecutionTarget.Daemon
            ? new ReadyClaimValidityOutput(ReadyValidityKind.SessionBound, isReady)
            : new ReadyClaimValidityOutput(ReadyValidityKind.ProbeOnly, GuaranteesReusableSession: false);
    }

    private static IReadOnlyList<ReadyEvidenceOutput> CreateEvidence (
        ReadyLifecycleProbeResult lifecycleProbeResult,
        ReadyReadIndexObservation readIndexObservation)
    {
        var evidence = new List<ReadyEvidenceOutput>();
        if (lifecycleProbeResult.Lifecycle != null)
        {
            evidence.Add(new ReadyEvidenceOutput(
                Kind: "lifecycleSnapshot",
                Data: lifecycleProbeResult.Lifecycle));
        }

        if (lifecycleProbeResult.Decision.IsFailure)
        {
            evidence.Add(new ReadyEvidenceOutput(
                Kind: "readinessDecision",
                Data: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["code"] = lifecycleProbeResult.Decision.ErrorCode?.Value,
                    ["message"] = lifecycleProbeResult.Decision.ErrorMessage,
                }));
        }

        if (readIndexObservation.Output != null)
        {
            evidence.Add(new ReadyEvidenceOutput(
                Kind: "readIndexSummary",
                Data: readIndexObservation.Output));
        }

        return evidence;
    }

    private static AssuranceClaimStatus ResolveClaimStatus (
        UnityReadinessDecision lifecycleDecision,
        ReadyReadIndexObservation readIndexObservation)
    {
        if (lifecycleDecision.IsFailure || readIndexObservation.HasFailure)
        {
            return AssuranceClaimStatus.Failed;
        }

        if (!lifecycleDecision.IsReady || readIndexObservation.IsDisabled)
        {
            return AssuranceClaimStatus.Indeterminate;
        }

        return AssuranceClaimStatus.Passed;
    }

    private static AssuranceCoverage ResolveClaimCoverage (ReadyReadIndexObservation readIndexObservation)
    {
        return readIndexObservation.IsDisabled
            ? AssuranceCoverage.None
            : AssuranceCoverage.Full;
    }

    private static string CreateStatement (
        ReadyTarget target,
        AssuranceClaimStatus claimStatus)
    {
        var targetValue = ContractLiteralCodec.ToValue(target);
        return claimStatus == AssuranceClaimStatus.Passed
            ? $"Unity is ready for {targetValue}."
            : $"Unity readiness for {targetValue} is not guaranteed.";
    }

    private static ReadIndexModeResolutionResult ResolveReadyReadIndexMode (
        ReadIndexMode? readIndexModeInput,
        UcliConfig config)
    {
        var readIndexModeResult = ReadIndexModeResolver.Resolve(readIndexModeInput, config);
        if (!readIndexModeResult.IsSuccess)
        {
            return readIndexModeResult;
        }

        if (readIndexModeResult.Mode == ReadIndexMode.Disabled)
        {
            return ReadIndexModeResolutionResult.Failure(ExecutionError.InvalidArgument(
                "ready --for readIndex requires --readIndexMode allowStale or requireFresh."));
        }

        return readIndexModeResult;
    }

    private static UnityExecutionTarget ResolveReadIndexExecutionTarget (UnityExecutionMode requestedMode)
    {
        return requestedMode == UnityExecutionMode.Daemon
            ? UnityExecutionTarget.Daemon
            : UnityExecutionTarget.Oneshot;
    }

    private static ApplicationFailure CreateTimeoutFailure (TimeSpan timeout)
    {
        return ApplicationFailure.Timeout(
            $"Unity readiness probe timed out after {timeout.TotalMilliseconds:0} milliseconds.",
            ExecutionErrorCodes.IpcTimeout);
    }

    private static ReadyLifecycleProbeResult CreateDaemonProbeTimeoutResult (
        ReadyLifecycleOutput? lastLifecycle,
        TimeSpan timeout)
    {
        var message = $"Unity readiness probe timed out after {timeout.TotalMilliseconds:0} milliseconds.";
        return lastLifecycle is null
            ? ReadyLifecycleProbeResult.FailureResult(ApplicationFailure.Timeout(message, ExecutionErrorCodes.IpcTimeout))
            : ReadyLifecycleProbeResult.Success(
                lastLifecycle,
                UnityReadinessDecision.Failure(
                    ExecutionErrorCodes.IpcTimeout,
                    message));
    }

    private static bool IsObservedEditorLifecycleFailure (UnityRequestFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        return failure.StartupFailure is null
            && UnityEditorReadinessPolicy.IsReadinessFailureCode(failure.Code);
    }

    private static TimeSpan GetRetryDelay (TimeSpan remainingTimeout)
    {
        var retryDelayMilliseconds = Math.Min(
            DaemonTimeouts.StartupProbeRetryDelayMilliseconds,
            Math.Max(1, (int)Math.Ceiling(remainingTimeout.TotalMilliseconds)));
        return TimeSpan.FromMilliseconds(retryDelayMilliseconds);
    }
}
