using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiEndpoint;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiAttach;

/// <summary> Implements existing GUI Editor detection and attach handling for daemon start. </summary>
internal sealed class DaemonGuiEditorAttachService : IDaemonGuiEditorAttachService
{
    private readonly IUnityEditorInstanceMarkerReader markerReader;

    private readonly IUnityGuiEditorProcessProbe processProbe;

    private readonly IDaemonGuiSessionRegistrationAwaiter sessionRegistrationAwaiter;

    private readonly IDaemonGuiRebootstrapClient rebootstrapClient;

    private readonly IDaemonDiagnosisStore daemonDiagnosisStore;

    private readonly DaemonCompensationOperationOwner compensationOperationOwner;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonGuiEditorAttachService" /> class. </summary>
    public DaemonGuiEditorAttachService (
        IUnityEditorInstanceMarkerReader markerReader,
        IUnityGuiEditorProcessProbe processProbe,
        IDaemonGuiSessionRegistrationAwaiter sessionRegistrationAwaiter,
        IDaemonGuiRebootstrapClient rebootstrapClient,
        IDaemonDiagnosisStore daemonDiagnosisStore,
        DaemonCompensationOperationOwner compensationOperationOwner,
        TimeProvider timeProvider)
    {
        this.markerReader = markerReader ?? throw new ArgumentNullException(nameof(markerReader));
        this.processProbe = processProbe ?? throw new ArgumentNullException(nameof(processProbe));
        this.sessionRegistrationAwaiter = sessionRegistrationAwaiter ?? throw new ArgumentNullException(nameof(sessionRegistrationAwaiter));
        this.rebootstrapClient = rebootstrapClient ?? throw new ArgumentNullException(nameof(rebootstrapClient));
        this.daemonDiagnosisStore = daemonDiagnosisStore ?? throw new ArgumentNullException(nameof(daemonDiagnosisStore));
        this.compensationOperationOwner = compensationOperationOwner ?? throw new ArgumentNullException(nameof(compensationOperationOwner));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async ValueTask<DaemonStartResult?> TryAttachExistingGuiEditorAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        DaemonEditorMode? editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        IDaemonStartProgressObserver? progressObserver = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(deadline);

        var markerReadOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                deadline,
                cancellationToken,
                "Timed out before Unity Editor instance marker read could begin.",
                "Timed out while reading the Unity Editor instance marker.",
                token => markerReader.ReadAsync(unityProject, token))
            .ConfigureAwait(false);
        if (!markerReadOperation.IsSuccess)
        {
            return DaemonStartResult.Failure(CreateTimeoutError(markerReadOperation.Error!.Message));
        }

        var markerReadResult = markerReadOperation.Value!;
        if (!markerReadResult.IsSuccess)
        {
            return DaemonStartResult.Failure(markerReadResult.Error!);
        }

        if (!markerReadResult.Exists)
        {
            return null;
        }

        var marker = markerReadResult.Marker!;
        var processProbeOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                deadline,
                cancellationToken,
                "Timed out before Unity GUI Editor process probe could begin.",
                "Timed out while probing the Unity GUI Editor process.",
                token => processProbe.ProbeAsync(marker, token))
            .ConfigureAwait(false);
        if (!processProbeOperation.IsSuccess)
        {
            return DaemonStartResult.Failure(CreateTimeoutError(processProbeOperation.Error!.Message));
        }

        var probeResult = processProbeOperation.Value!;
        if (!probeResult.IsMatchingGuiEditor)
        {
            return null;
        }

        if (editorMode == DaemonEditorMode.Batchmode)
        {
            return DaemonStartResult.Failure(ExecutionError.InvalidArgument(
                "Requested daemon editorMode 'batchmode' conflicts with an existing GUI Editor process for the target project.",
                DaemonErrorCodes.DaemonEditorModeMismatch));
        }

        if (!deadline.TryGetRemainingTimeout(out var waitTimeout))
        {
            return await CreateGuiEndpointNotRegisteredFailureAsync(
                    unityProject,
                    marker,
                    probeResult.ProcessStartedAtUtc,
                    onStartupBlocked,
                    progressObserver,
                    CreateTimeoutError($"Timed out before waiting for existing GUI Editor endpoint registration. ProcessId={marker.ProcessId}."),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await EmitWaitingForEndpointAsync(progressObserver, marker, probeResult.ProcessStartedAtUtc, cancellationToken).ConfigureAwait(false);
        var initialProbeDeadline = deadline.CreateCappedDeadline(
            GetInitialSessionProbeTimeout(waitTimeout));
        var initialWaitResult = await sessionRegistrationAwaiter.WaitForSessionAsync(
                unityProject,
                marker.ProcessId,
                initialProbeDeadline,
                expectedProcessStartedAtUtc: probeResult.ProcessStartedAtUtc!.Value,
                cancellationToken)
            .ConfigureAwait(false);
        if (initialWaitResult.IsSuccess)
        {
            await EmitEndpointReadyAsync(progressObserver, initialWaitResult.Session!, initialWaitResult.LifecycleObservation, cancellationToken).ConfigureAwait(false);
            return DaemonStartResult.Attached(initialWaitResult.Session!, initialWaitResult.LifecycleObservation);
        }

        if (initialWaitResult.Error!.Kind != ExecutionErrorKind.Timeout)
        {
            return DaemonStartResult.Failure(initialWaitResult.Error);
        }

        if (!deadline.TryGetRemainingTimeout(out _))
        {
            return await CreateGuiEndpointNotRegisteredFailureAsync(
                    unityProject,
                    marker,
                    probeResult.ProcessStartedAtUtc,
                    onStartupBlocked,
                    progressObserver,
                    initialWaitResult.Error,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var rebootstrapResult = await rebootstrapClient.RequestRebootstrapAsync(
                unityProject,
                marker.ProcessId,
                probeResult.ProcessStartedAtUtc,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (!rebootstrapResult.IsAccepted)
        {
            return await CreateGuiRebootstrapUnavailableFailureAsync(
                    unityProject,
                    marker,
                    probeResult.ProcessStartedAtUtc,
                    onStartupBlocked,
                    rebootstrapResult.Error!,
                    progressObserver,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!deadline.TryGetRemainingTimeout(out _))
        {
            return await CreateGuiEndpointNotRegisteredFailureAsync(
                    unityProject,
                    marker,
                    probeResult.ProcessStartedAtUtc,
                    onStartupBlocked,
                    progressObserver,
                    initialWaitResult.Error,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var waitResult = await sessionRegistrationAwaiter.WaitForSessionAsync(
                unityProject,
                marker.ProcessId,
                deadline,
                expectedProcessStartedAtUtc: probeResult.ProcessStartedAtUtc!.Value,
                cancellationToken)
            .ConfigureAwait(false);
        if (waitResult.IsSuccess)
        {
            await EmitEndpointReadyAsync(progressObserver, waitResult.Session!, waitResult.LifecycleObservation, cancellationToken).ConfigureAwait(false);
            return DaemonStartResult.Attached(waitResult.Session!, waitResult.LifecycleObservation);
        }

        if (waitResult.Error!.Kind != ExecutionErrorKind.Timeout)
        {
            return DaemonStartResult.Failure(waitResult.Error);
        }

        return await CreateGuiEndpointNotRegisteredFailureAsync(
                unityProject,
                marker,
                probeResult.ProcessStartedAtUtc,
                onStartupBlocked,
                progressObserver,
                waitResult.Error,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<DaemonStartResult> CreateGuiRebootstrapUnavailableFailureAsync (
        ResolvedUnityProjectContext unityProject,
        UnityEditorInstanceMarker marker,
        DateTimeOffset? processStartedAtUtc,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        ExecutionError rebootstrapError,
        IDaemonStartProgressObserver? progressObserver,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await DaemonGuiRebootstrapUnavailableFailureFactory.CreateFailureAsync(
                unityProject,
                compensationOperationOwner,
                daemonDiagnosisStore,
                timeProvider,
                marker.MarkerPath,
                marker.ProcessId,
                processStartedAtUtc,
                onStartupBlocked,
                rebootstrapError,
                cancellationToken)
            .ConfigureAwait(false);
        await EmitBlockerDetectedAsync(
                progressObserver,
                marker,
                processStartedAtUtc,
                result.Startup!,
                result.Error,
                cancellationToken)
            .ConfigureAwait(false);
        return result;
    }

    private static TimeSpan GetInitialSessionProbeTimeout (TimeSpan remainingTimeout)
    {
        var probeMilliseconds = Math.Min(
            DaemonTimeouts.ProbeAttemptTimeoutCap.TotalMilliseconds,
            Math.Max(1, Math.Ceiling(remainingTimeout.TotalMilliseconds / 4)));
        return TimeSpan.FromMilliseconds(probeMilliseconds);
    }

    private async ValueTask<DaemonStartResult> CreateGuiEndpointNotRegisteredFailureAsync (
        ResolvedUnityProjectContext unityProject,
        UnityEditorInstanceMarker marker,
        DateTimeOffset? processStartedAtUtc,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        IDaemonStartProgressObserver? progressObserver,
        ExecutionError waitError,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await DaemonGuiEndpointNotRegisteredFailureFactory.CreateFailureAsync(
                unityProject,
                compensationOperationOwner,
                daemonDiagnosisStore,
                timeProvider,
                "existing GUI Editor",
                marker.MarkerPath,
                marker.ProcessId,
                waitError,
                processStartedAtUtc,
                unityLogPath: null,
                cancellationToken)
            .ConfigureAwait(false);
        var policyResolution = DaemonStartupBlockedProcessPolicyResolver.Resolve(
            onStartupBlocked,
            DaemonEditorMode.Gui,
            DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            marker.ProcessId);
        var startup = new DaemonStartupObservation(
            StartupStatus: DaemonStartupStatus.Timeout,
            StartupBlockingReason: DaemonStartupBlockingReason.EndpointNotRegistered,
            LaunchAttemptId: null,
            ProcessAction: policyResolution.ProcessActionWhenNotTerminated,
            RetryDisposition: DaemonStartupRetryDisposition.WaitThenRetry,
            EditorMode: DaemonEditorMode.Gui,
            OwnerKind: DaemonSessionOwnerKind.User,
            CanShutdownProcess: false,
            ProcessId: marker.ProcessId,
            StartedAtUtc: processStartedAtUtc,
            ElapsedMilliseconds: null,
            ArtifactPath: null);
        var failure = DaemonStartResult.Failure(result.Error!, result.Diagnosis, startup);
        await EmitBlockerDetectedAsync(
                progressObserver,
                marker,
                processStartedAtUtc,
                startup,
                failure.Error,
                cancellationToken)
            .ConfigureAwait(false);
        return failure;
    }

    private static ExecutionError CreateTimeoutError (string message)
    {
        return ExecutionError.Timeout(message, ExecutionErrorCodes.IpcTimeout);
    }

    private static async ValueTask EmitWaitingForEndpointAsync (
        IDaemonStartProgressObserver? progressObserver,
        UnityEditorInstanceMarker marker,
        DateTimeOffset? processStartedAtUtc,
        CancellationToken cancellationToken)
    {
        if (progressObserver is null)
        {
            return;
        }

        await progressObserver.EmitWaitingForEndpointAsync(
                new DaemonStartStartupProgressObservation(
                    LaunchAttemptId: null,
                    EditorMode: DaemonEditorMode.Gui,
                    OwnerKind: DaemonSessionOwnerKind.User,
                    CanShutdownProcess: false,
                    ProcessId: marker.ProcessId,
                    ProcessStartedAtUtc: processStartedAtUtc,
                    StartupStatus: null,
                    StartupBlockingReason: null,
                    StartupPhase: null,
                    RetryDisposition: null,
                    Message: null,
                    ErrorCode: null),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask EmitEndpointReadyAsync (
        IDaemonStartProgressObserver? progressObserver,
        DaemonSession session,
        IpcUnityEditorObservation? lifecycleObservation,
        CancellationToken cancellationToken)
    {
        if (progressObserver is null)
        {
            return;
        }

        await progressObserver.EmitSessionRegisteredAsync(session, launchAttemptId: null, cancellationToken).ConfigureAwait(false);
        await progressObserver.EmitEndpointRegisteredAsync(session, launchAttemptId: null, cancellationToken).ConfigureAwait(false);
        if (lifecycleObservation is not null)
        {
            await progressObserver.EmitLifecycleObservedAsync(lifecycleObservation, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask EmitBlockerDetectedAsync (
        IDaemonStartProgressObserver? progressObserver,
        UnityEditorInstanceMarker marker,
        DateTimeOffset? processStartedAtUtc,
        DaemonStartupObservation startup,
        ExecutionError? error,
        CancellationToken cancellationToken)
    {
        if (progressObserver is null)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(startup);
        await progressObserver.EmitBlockerDetectedAsync(
                new DaemonStartStartupProgressObservation(
                    LaunchAttemptId: startup.LaunchAttemptId,
                    EditorMode: startup.EditorMode,
                    OwnerKind: startup.OwnerKind,
                    CanShutdownProcess: startup.CanShutdownProcess,
                    ProcessId: startup.ProcessId ?? marker.ProcessId,
                    ProcessStartedAtUtc: startup.StartedAtUtc ?? processStartedAtUtc,
                    StartupStatus: startup.StartupStatus,
                    StartupBlockingReason: startup.StartupBlockingReason,
                    StartupPhase: DaemonDiagnosisStartupPhase.EndpointRegistration,
                    RetryDisposition: startup.RetryDisposition,
                    Message: error?.Message,
                    ErrorCode: error is null ? null : ExecutionErrorCodeMapper.ToCode(error).Value),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
