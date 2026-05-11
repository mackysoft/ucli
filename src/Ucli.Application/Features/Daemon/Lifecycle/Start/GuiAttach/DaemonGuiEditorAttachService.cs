using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiEndpoint;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Startup;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiAttach;

/// <summary> Implements existing GUI Editor detection and attach handling for daemon start. </summary>
internal sealed class DaemonGuiEditorAttachService : IDaemonGuiEditorAttachService
{
    private readonly IUnityEditorInstanceMarkerReader markerReader;

    private readonly IUnityGuiEditorProcessProbe processProbe;

    private readonly IDaemonGuiSessionRegistrationAwaiter sessionRegistrationAwaiter;

    private readonly IDaemonDiagnosisStore daemonDiagnosisStore;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonGuiEditorAttachService" /> class. </summary>
    public DaemonGuiEditorAttachService (
        IUnityEditorInstanceMarkerReader markerReader,
        IUnityGuiEditorProcessProbe processProbe,
        IDaemonGuiSessionRegistrationAwaiter sessionRegistrationAwaiter,
        IDaemonDiagnosisStore daemonDiagnosisStore,
        TimeProvider? timeProvider = null)
    {
        this.markerReader = markerReader ?? throw new ArgumentNullException(nameof(markerReader));
        this.processProbe = processProbe ?? throw new ArgumentNullException(nameof(processProbe));
        this.sessionRegistrationAwaiter = sessionRegistrationAwaiter ?? throw new ArgumentNullException(nameof(sessionRegistrationAwaiter));
        this.daemonDiagnosisStore = daemonDiagnosisStore ?? throw new ArgumentNullException(nameof(daemonDiagnosisStore));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async ValueTask<DaemonStartResult?> TryAttachExistingGuiEditorAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        DaemonEditorMode? editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        var markerReadResult = await markerReader.ReadAsync(unityProject, cancellationToken).ConfigureAwait(false);
        if (!markerReadResult.IsSuccess)
        {
            return DaemonStartResult.Failure(markerReadResult.Error!);
        }

        if (!markerReadResult.Exists)
        {
            return null;
        }

        var marker = markerReadResult.Marker!;
        var probeResult = await processProbe.ProbeAsync(marker, cancellationToken).ConfigureAwait(false);
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
                    CreateTimeoutError($"Timed out before waiting for existing GUI Editor endpoint registration. ProcessId={marker.ProcessId}."))
                .ConfigureAwait(false);
        }

        var waitResult = await sessionRegistrationAwaiter.WaitForSessionAsync(
                unityProject,
                marker.ProcessId,
                waitTimeout,
                expectedProcessStartedAtUtc: probeResult.ProcessStartedAtUtc,
                cancellationToken)
            .ConfigureAwait(false);
        if (waitResult.IsSuccess)
        {
            return DaemonStartResult.AlreadyRunning(waitResult.Session!, waitResult.LifecycleSnapshot);
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
                waitResult.Error)
            .ConfigureAwait(false);
    }

    private async ValueTask<DaemonStartResult> CreateGuiEndpointNotRegisteredFailureAsync (
        ResolvedUnityProjectContext unityProject,
        UnityEditorInstanceMarker marker,
        DateTimeOffset? processStartedAtUtc,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        ExecutionError waitError)
    {
        var result = await DaemonGuiEndpointNotRegisteredFailureFactory.CreateFailureAsync(
                unityProject,
                daemonDiagnosisStore,
                timeProvider,
                "existing GUI Editor",
                marker.MarkerPath,
                marker.ProcessId,
                waitError,
                processStartedAtUtc)
            .ConfigureAwait(false);
        var policyResolution = DaemonStartupBlockedProcessPolicyResolver.Resolve(
            onStartupBlocked,
            DaemonEditorModeValues.Gui,
            DaemonSessionOwnerKindValues.User,
            canShutdownProcess: false,
            marker.ProcessId);
        var startup = new DaemonStartupObservation(
            StartupStatus: DaemonStartupStatusValues.Timeout,
            StartupBlockingReason: DaemonStartupBlockingReasonValues.EndpointNotRegistered,
            LaunchAttemptId: null,
            ProcessAction: policyResolution.ProcessActionWhenNotTerminated,
            RetryDisposition: DaemonStartupRetryDispositionValues.WaitThenRetry,
            EditorMode: DaemonEditorModeValues.Gui,
            OwnerKind: DaemonSessionOwnerKindValues.User,
            CanShutdownProcess: false,
            ProcessId: marker.ProcessId,
            StartedAtUtc: processStartedAtUtc,
            ElapsedMilliseconds: null,
            ArtifactPath: null);
        return DaemonStartResult.Failure(result.Error!, result.Diagnosis, startup);
    }

    private static ExecutionError CreateTimeoutError (string message)
    {
        return ExecutionError.Timeout(message, ExecutionErrorCodes.IpcTimeout);
    }

}
