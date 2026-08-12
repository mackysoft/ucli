using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;

/// <summary>
/// Owns the typed project-refresh application workflow from execution registration through
/// provider-independent terminal-result projection.
/// </summary>
internal sealed partial class RefreshService : IRefreshService
{
    private static readonly LifecycleExecutionDefinition Definition =
        new(LifecycleExecutionKind.Refresh);

    private readonly IMutationReadPostconditionStore mutationReadPostconditionStore;

    private readonly ILifecycleExecutionReconnectResolver reconnectResolver;

    private readonly ILifecycleExecutionHostExitTerminalizer hostExitTerminalizer;

    private readonly LifecycleExecutionRegistrationIssuer registrationIssuer;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes one refresh application handler. </summary>
    public RefreshService (
        IMutationReadPostconditionStore mutationReadPostconditionStore,
        ILifecycleExecutionReconnectResolver reconnectResolver,
        ILifecycleExecutionHostExitTerminalizer hostExitTerminalizer,
        LifecycleExecutionRegistrationIssuer registrationIssuer,
        TimeProvider timeProvider)
    {
        this.mutationReadPostconditionStore = mutationReadPostconditionStore ?? throw new ArgumentNullException(nameof(mutationReadPostconditionStore));
        this.reconnectResolver = reconnectResolver ?? throw new ArgumentNullException(nameof(reconnectResolver));
        this.hostExitTerminalizer = hostExitTerminalizer ?? throw new ArgumentNullException(nameof(hostExitTerminalizer));
        this.registrationIssuer = registrationIssuer ?? throw new ArgumentNullException(nameof(registrationIssuer));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    private async ValueTask<RefreshExecutionResult> ExecuteRegisteredAsync (
        Guid requestId,
        ProjectContext context,
        ProjectIdentityInfo project,
        LifecycleExecutionRegistration registration,
        ILifecycleExecutionStartAdmissionPolicy? startAdmissionPolicy,
        ExecutionRef? reconnectedExecutionRef,
        LifecycleExecutionStartBinding? requiredStart,
        CancellationToken cancellationToken,
        Func<UnityRequestPayload, CancellationToken, ValueTask<UnityRequestExecutionResult>> dispatchAsync)
    {
        var payload = new UnityRequestPayload.Refresh(
            registration,
            requiredStart,
            startAdmissionPolicy);
        var executionResult = await dispatchAsync(payload, cancellationToken)
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
                    return await CreateHostExitPublicationFailureAsync(
                            requestId,
                            context,
                            project,
                            registration,
                            publicationFailed.ExecutionReference,
                            publicationFailed.ApplicationState,
                            publicationFailed.Failure,
                            publicationFailed.FixedTerminalRecord
                                as RefreshLifecycleExecutionTerminalRecord)
                        .ConfigureAwait(false);
                }

                var published =
                    (LifecycleExecutionHostExitTerminalizationResult.Published)
                        terminalization;
                return await CreateResultFromTerminalRecordAsync(
                        requestId,
                        context,
                        project,
                        published.ExecutionReference,
                        published.TerminalRecord)
                    .ConfigureAwait(false);
            }

            return await CreateTransportFailureAsync(
                    requestId,
                    context,
                    project,
                    registration,
                    executionResult,
                    reconnectedExecutionRef)
                .ConfigureAwait(false);
        }

        return await CreateResponseResultAsync(
                requestId,
                context,
                project,
                registration,
                executionResult,
                reconnectedExecutionRef)
            .ConfigureAwait(false);
    }

    private async ValueTask<RefreshExecutionResult>
        CreateResultFromTerminalRecordAsync (
            Guid requestId,
            ProjectContext context,
            ProjectIdentityInfo project,
            ExecutionRef executionReference,
            LifecycleExecutionTerminalRecord terminalRecord)
    {
        if (executionReference is not TerminalExecutionRef terminalReference
            || terminalRecord
                is not RefreshLifecycleExecutionTerminalRecord refreshRecord)
        {
            return RefreshExecutionResult.Failure(
                ApplicationFailure.InternalError(
                    "Refresh reconnection did not resolve a typed refresh Terminal Record."),
                new RefreshExecutionErrorOutput(
                    project,
                    requestId,
                    executionReference,
                    ExecutionApplicationState.Indeterminate,
                    Refresh: null,
                    ObservedLifecycle: null,
                    ReadPostcondition: null));
        }

        var result = refreshRecord.Result;
        if (refreshRecord.TerminalReason
            == LifecycleExecutionTerminalReason.Completed)
        {
            var persistenceFailure = await PersistReadPostconditionAsync(
                    context,
                    result!.ReadPostcondition,
                    CancellationToken.None)
                .ConfigureAwait(false);
            if (persistenceFailure is not null)
            {
                return RefreshExecutionResult.Failure(
                    persistenceFailure,
                    new RefreshExecutionErrorOutput(
                        project,
                        requestId,
                        terminalReference,
                        refreshRecord.ApplicationState,
                        new RefreshLifecycleStartEvidence(
                            result.Refresh.StartedAtUtc,
                            result.Refresh.DomainReloadGenerationBefore),
                        result.Lifecycle,
                        result.ReadPostcondition));
            }

            return RefreshExecutionResult.Success(
                new RefreshExecutionOutput(
                    project,
                    requestId,
                    terminalReference,
                    result.Refresh,
                    result.Lifecycle,
                    result.ReadPostcondition));
        }

        var readPostcondition = result?.ReadPostcondition;
        if (refreshRecord.ApplicationState
                != ExecutionApplicationState.NotApplied
            && readPostcondition is null)
        {
            readPostcondition = CreateAllReadSurfacesPostcondition(
                refreshRecord.StartedAtUtc);
        }
        var failures = new List<ApplicationFailure>
        {
            CreateTerminalFailure(refreshRecord.TerminalReason),
        };
        var readPostconditionFailure = await PersistReadPostconditionAsync(
                context,
                readPostcondition,
                CancellationToken.None)
            .ConfigureAwait(false);
        if (readPostconditionFailure is not null)
        {
            failures.Add(readPostconditionFailure);
        }

        return RefreshExecutionResult.Failure(
            failures,
            new RefreshExecutionErrorOutput(
                project,
                requestId,
                terminalReference,
                refreshRecord.ApplicationState,
                result is null
                    ? null
                    : new RefreshLifecycleStartEvidence(
                        result.Refresh.StartedAtUtc,
                        result.Refresh.DomainReloadGenerationBefore),
                result?.Lifecycle,
                readPostcondition));
    }

    private static LifecycleExecutionTerminalRecord
        CreateHostExitTerminalRecord (
            LifecycleExecutionStartBinding start,
            LifecycleExecutionTerminalFacts terminalFacts)
    {
        return new RefreshLifecycleExecutionTerminalRecord(
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

    private async ValueTask<RefreshExecutionResult>
        CreateHostExitPublicationFailureAsync (
            Guid requestId,
            ProjectContext context,
            ProjectIdentityInfo project,
            LifecycleExecutionRegistration registration,
            ExecutionRef executionReference,
            ExecutionApplicationState applicationState,
            ApplicationFailure publicationFailure,
            RefreshLifecycleExecutionTerminalRecord? fixedTerminalRecord)
    {
        var result = fixedTerminalRecord?.Result;
        var readPostcondition = result?.ReadPostcondition;
        var failures = new List<ApplicationFailure>
        {
            publicationFailure,
        };
        if (applicationState != ExecutionApplicationState.NotApplied)
        {
            readPostcondition ??= CreateAllReadSurfacesPostcondition(
                registration.StartedAtUtc);
            var persistenceFailure = await PersistReadPostconditionAsync(
                    context,
                    readPostcondition,
                    CancellationToken.None)
                .ConfigureAwait(false);
            if (persistenceFailure is not null)
            {
                failures.Add(persistenceFailure);
            }
        }

        return RefreshExecutionResult.Failure(
            failures,
            new RefreshExecutionErrorOutput(
                project,
                requestId,
                executionReference,
                applicationState,
                result is null
                    ? null
                    : new RefreshLifecycleStartEvidence(
                        result.Refresh.StartedAtUtc,
                        result.Refresh.DomainReloadGenerationBefore),
                result?.Lifecycle,
                readPostcondition));
    }

    private static ApplicationFailure CreateTerminalFailure (
        LifecycleExecutionTerminalReason terminalReason)
    {
        return terminalReason switch
        {
            LifecycleExecutionTerminalReason.ActionFailed =>
                ApplicationFailure.InternalError(
                    "Unity refresh action ended with an explicit failure."),
            LifecycleExecutionTerminalReason.DeadlineExceeded =>
                ApplicationFailure.Timeout(
                    "Refresh reached its durable execution deadline.",
                    LifecycleExecutionErrorCodes.DeadlineExceeded),
            LifecycleExecutionTerminalReason.ProjectMismatch =>
                ApplicationFailure.ContractViolation(
                    "Refresh recovery project does not match its durable start.",
                    LifecycleExecutionErrorCodes.ProjectMismatch),
            LifecycleExecutionTerminalReason.HostMismatch =>
                ApplicationFailure.ContractViolation(
                    "Refresh recovery host does not match its durable start.",
                    LifecycleExecutionErrorCodes.HostMismatch),
            LifecycleExecutionTerminalReason.GenerationMismatch =>
                ApplicationFailure.ContractViolation(
                    "Refresh recovery generation was not a proven successor.",
                    LifecycleExecutionErrorCodes.GenerationMismatch),
            LifecycleExecutionTerminalReason.UnityExited =>
                ApplicationFailure.ExternalProcessFailure(
                    "The Unity Editor hosting refresh exited before completion.",
                    LifecycleExecutionErrorCodes.UnityExited),
            _ => throw new ArgumentOutOfRangeException(
                nameof(terminalReason),
                terminalReason,
                "Completed refresh Terminal Records are projected as success."),
        };
    }

    private async ValueTask<RefreshExecutionResult> CreateTransportFailureAsync (
        Guid requestId,
        ProjectContext context,
        ProjectIdentityInfo project,
        LifecycleExecutionRegistration registration,
        UnityRequestExecutionResult executionResult,
        ExecutionRef? reconnectedExecutionRef)
    {
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
        ExecutionReadPostcondition? readPostcondition = null;
        var failures = new List<ApplicationFailure>
        {
            RequestFailureNormalizer.FromUnityRequestFailure(executionResult.FailureInfo!),
        };

        if (waitFailure.ApplicationState
            != ExecutionApplicationState.NotApplied)
        {
            readPostcondition = CreateAllReadSurfacesPostcondition(registration.StartedAtUtc);
            var persistenceFailure = await PersistReadPostconditionAsync(
                    context,
                    readPostcondition,
                    CancellationToken.None)
                .ConfigureAwait(false);
            if (persistenceFailure is not null)
            {
                failures.Add(persistenceFailure);
            }
        }

        return RefreshExecutionResult.Failure(
            failures,
            new RefreshExecutionErrorOutput(
                project,
                requestId,
                waitFailure.ExecutionReference,
                waitFailure.ApplicationState,
                Refresh: null,
                ObservedLifecycle: null,
                readPostcondition));
    }

    private async ValueTask<RefreshExecutionResult>
        CreateCallerWaitCanceledFailureAsync (
            Guid requestId,
            ProjectContext context,
            ProjectIdentityInfo project,
            LifecycleExecutionRegistration registration,
            ExecutionRef lifecycleExecutionRef)
    {
        var readPostcondition =
            CreateAllReadSurfacesPostcondition(registration.StartedAtUtc);
        var failures = new List<ApplicationFailure>
        {
            ApplicationFailure.Canceled(
                "Waiting for the reconnected Unity refresh execution was canceled.",
                ExecutionErrorCodes.Canceled),
        };
        var persistenceFailure = await PersistReadPostconditionAsync(
                context,
                readPostcondition,
                CancellationToken.None)
            .ConfigureAwait(false);
        if (persistenceFailure is not null)
        {
            failures.Add(persistenceFailure);
        }

        return RefreshExecutionResult.Failure(
            failures,
            new RefreshExecutionErrorOutput(
                project,
                requestId,
                lifecycleExecutionRef,
                ExecutionApplicationState.Unknown,
                Refresh: null,
                ObservedLifecycle: null,
                readPostcondition));
    }

    private async ValueTask<RefreshExecutionResult> CreateResponseResultAsync (
        Guid requestId,
        ProjectContext context,
        ProjectIdentityInfo fallbackProject,
        LifecycleExecutionRegistration registration,
        UnityRequestExecutionResult executionResult,
        ExecutionRef? reconnectedExecutionRef)
    {
        var response = executionResult.Response!;
        if (response.Errors.Count != 0
            && executionResult.LifecycleExecutionStart is null)
        {
            var failure = RequestFailureNormalizer.FromOperationError(
                response.Errors[0]);
            if (reconnectedExecutionRef is not null)
            {
                return await CreateUntrustedResponseFailureAsync(
                        requestId,
                        context,
                        fallbackProject,
                        registration,
                        start: null,
                        reconnectedExecutionRef,
                        [failure])
                    .ConfigureAwait(false);
            }

            return RefreshExecutionResult.Failure(
                failure,
                CreatePreStartErrorOutput(
                    fallbackProject,
                    requestId));
        }

        if (response.Errors.Count != 0)
        {
            return await CreateActionFailureAsync(
                    requestId,
                    context,
                    fallbackProject,
                    registration,
                    executionResult.LifecycleExecutionStart,
                    reconnectedExecutionRef,
                    response)
                .ConfigureAwait(false);
        }

        if (!IpcPayloadCodec.TryDeserialize(
                response.Payload,
                out IpcRefreshResponse refreshResponse,
                out var payloadError))
        {
            return await CreateUntrustedResponseFailureAsync(
                    requestId,
                    context,
                    fallbackProject,
                    registration,
                    executionResult.LifecycleExecutionStart,
                    reconnectedExecutionRef,
                    [
                        ApplicationFailure.ContractViolation(
                            $"Unity refresh payload is invalid. {payloadError.Message}"),
                    ])
                .ConfigureAwait(false);
        }

        if (!registration.HasSameIdentity(
                refreshResponse.LifecycleExecutionRef))
        {
            return await CreateUntrustedResponseFailureAsync(
                    requestId,
                    context,
                    fallbackProject,
                    registration,
                    executionResult.LifecycleExecutionStart,
                    reconnectedExecutionRef,
                    [
                        ApplicationFailure.ContractViolation(
                            "Unity refresh response identifies a different Lifecycle Execution."),
                    ])
                .ConfigureAwait(false);
        }

        if (!ProjectIdentityInfo.TryFromHost(
                context.UnityProject,
                refreshResponse.Project,
                out var project,
                out var mismatchKind))
        {
            return await CreateUntrustedResponseFailureAsync(
                    requestId,
                    context,
                    fallbackProject,
                    registration,
                    executionResult.LifecycleExecutionStart,
                    reconnectedExecutionRef,
                    [
                        ApplicationFailure.ContractViolation(
                            $"Unity refresh project identity mismatch. Component={mismatchKind}."),
                    ])
                .ConfigureAwait(false);
        }

        var persistenceFailure = await PersistReadPostconditionAsync(
                context,
                refreshResponse.Result.ReadPostcondition,
                CancellationToken.None)
            .ConfigureAwait(false);
        if (persistenceFailure is not null)
        {
            var refresh = refreshResponse.Result.Refresh;
            return RefreshExecutionResult.Failure(
                persistenceFailure,
                new RefreshExecutionErrorOutput(
                    project,
                    requestId,
                    refreshResponse.LifecycleExecutionRef,
                    ExecutionApplicationState.Applied,
                    new RefreshLifecycleStartEvidence(
                        refresh.StartedAtUtc,
                        refresh.DomainReloadGenerationBefore),
                    refreshResponse.Result.Lifecycle,
                    refreshResponse.Result.ReadPostcondition));
        }

        return RefreshExecutionResult.Success(
            new RefreshExecutionOutput(
                project,
                requestId,
                (ITerminalExecutionRef)refreshResponse.LifecycleExecutionRef,
                refreshResponse.Result.Refresh,
                refreshResponse.Result.Lifecycle,
                refreshResponse.Result.ReadPostcondition));
    }

    private async ValueTask<RefreshExecutionResult> CreateActionFailureAsync (
        Guid requestId,
        ProjectContext context,
        ProjectIdentityInfo fallbackProject,
        LifecycleExecutionRegistration registration,
        LifecycleExecutionStartBinding? start,
        ExecutionRef? reconnectedExecutionRef,
        UnityRequestResponse response)
    {
        var firstError = response.Errors[0];
        var failure = ApplicationFailure.FromCode(
            firstError.Code,
            firstError.Message,
            firstError.InstancePath);
        if (!IpcPayloadCodec.TryDeserialize(
                response.Payload,
                out IpcRefreshErrorResponse errorResponse,
                out var payloadError))
        {
            return await CreateUntrustedResponseFailureAsync(
                    requestId,
                    context,
                    fallbackProject,
                    registration,
                    start,
                    reconnectedExecutionRef,
                    [
                        failure,
                        ApplicationFailure.ContractViolation(
                            $"Unity refresh error payload is invalid. {payloadError.Message}"),
                    ])
                .ConfigureAwait(false);
        }

        if (errorResponse.LifecycleExecutionRef == null
            ? start is not null
            : !registration.HasSameIdentity(
                errorResponse.LifecycleExecutionRef))
        {
            return await CreateUntrustedResponseFailureAsync(
                    requestId,
                    context,
                    fallbackProject,
                    registration,
                    start,
                    reconnectedExecutionRef,
                    [
                        failure,
                        ApplicationFailure.ContractViolation(
                            "Unity refresh error response does not identify the registered Lifecycle Execution."),
                    ])
                .ConfigureAwait(false);
        }

        if (!ProjectIdentityInfo.TryFromHost(
                context.UnityProject,
                errorResponse.Project,
                out var project,
                out var mismatchKind))
        {
            return await CreateUntrustedResponseFailureAsync(
                    requestId,
                    context,
                    fallbackProject,
                    registration,
                    start,
                    reconnectedExecutionRef,
                    [
                        failure,
                        ApplicationFailure.ContractViolation(
                            $"Unity refresh error project identity mismatch. Component={mismatchKind}."),
                    ])
                .ConfigureAwait(false);
        }

        var retainedReference = errorResponse.LifecycleExecutionRef
            ?? start?.LifecycleExecutionRef
            ?? reconnectedExecutionRef;
        var applicationState = errorResponse.LifecycleExecutionRef is null
            && reconnectedExecutionRef is not null
                ? ExecutionApplicationState.Unknown
                : errorResponse.ApplicationState;
        var readPostcondition = errorResponse.ReadPostcondition;
        if (applicationState != ExecutionApplicationState.NotApplied
            && readPostcondition is null)
        {
            readPostcondition = CreateAllReadSurfacesPostcondition(registration.StartedAtUtc);
        }

        var failures = new List<ApplicationFailure> { failure };
        var persistenceFailure = await PersistReadPostconditionAsync(
                context,
                readPostcondition,
                CancellationToken.None)
            .ConfigureAwait(false);
        if (persistenceFailure is not null)
        {
            failures.Add(persistenceFailure);
        }

        return RefreshExecutionResult.Failure(
            failures,
            new RefreshExecutionErrorOutput(
                project,
                requestId,
                retainedReference,
                applicationState,
                errorResponse.Refresh,
                errorResponse.ObservedLifecycle,
                readPostcondition));
    }

    private async ValueTask<RefreshExecutionResult> CreateUntrustedResponseFailureAsync (
        Guid requestId,
        ProjectContext context,
        ProjectIdentityInfo project,
        LifecycleExecutionRegistration registration,
        LifecycleExecutionStartBinding? start,
        ExecutionRef? reconnectedExecutionRef,
        IReadOnlyList<ApplicationFailure> responseFailures)
    {
        var failures = responseFailures.ToList();
        var retainedReference = start?.LifecycleExecutionRef
            ?? reconnectedExecutionRef;
        var applicationState = retainedReference is null
            ? ExecutionApplicationState.NotApplied
            : ExecutionApplicationState.Unknown;
        ExecutionReadPostcondition? readPostcondition = null;
        if (retainedReference is not null)
        {
            readPostcondition = CreateAllReadSurfacesPostcondition(
                registration.StartedAtUtc);
            var persistenceFailure = await PersistReadPostconditionAsync(
                    context,
                    readPostcondition,
                    CancellationToken.None)
                .ConfigureAwait(false);
            if (persistenceFailure is not null)
            {
                failures.Add(persistenceFailure);
            }
        }

        return RefreshExecutionResult.Failure(
            failures,
            new RefreshExecutionErrorOutput(
                project,
                requestId,
                retainedReference,
                applicationState,
                Refresh: null,
                ObservedLifecycle: null,
                readPostcondition));
    }

    private async ValueTask<ApplicationFailure?> PersistReadPostconditionAsync (
        ProjectContext context,
        ExecutionReadPostcondition? readPostcondition,
        CancellationToken cancellationToken)
    {
        if (readPostcondition is null || readPostcondition.Requirements.Count == 0)
        {
            return null;
        }

        var result = await mutationReadPostconditionStore.WriteMergedAsync(
                context.UnityProject.RepositoryRoot,
                context.UnityProject.ProjectFingerprint,
                readPostcondition,
                cancellationToken)
            .ConfigureAwait(false);
        return result.IsSuccess
            ? null
            : ApplicationFailure.FromExecutionError(result.Error!);
    }

    private static ExecutionReadPostcondition CreateAllReadSurfacesPostcondition (
        DateTimeOffset minSafeGeneratedAtUtc)
    {
        return new ExecutionReadPostcondition(
        [
            new ExecutionReadPostconditionRequirement(
                ExecutionReadPostconditionSurface.AssetSearch,
                minSafeGeneratedAtUtc,
                ScenePath: null),
            new ExecutionReadPostconditionRequirement(
                ExecutionReadPostconditionSurface.GuidPath,
                minSafeGeneratedAtUtc,
                ScenePath: null),
            new ExecutionReadPostconditionRequirement(
                ExecutionReadPostconditionSurface.SceneTreeLite,
                minSafeGeneratedAtUtc,
                ScenePath: null),
        ]);
    }

    private static RefreshExecutionErrorOutput CreatePreStartErrorOutput (
        ProjectIdentityInfo project,
        Guid requestId)
    {
        return new RefreshExecutionErrorOutput(
            project,
            requestId,
            LifecycleExecutionRef: null,
            ExecutionApplicationState.NotApplied,
            Refresh: null,
            ObservedLifecycle: null,
            ReadPostcondition: null);
    }
}
