using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;

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
    public async ValueTask<DaemonStartResult?> TryAttachExistingGuiEditor (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        DaemonEditorMode? editorMode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var markerReadResult = await markerReader.Read(unityProject, cancellationToken).ConfigureAwait(false);
        if (!markerReadResult.IsSuccess)
        {
            return DaemonStartResult.Failure(markerReadResult.Error!);
        }

        if (!markerReadResult.Exists)
        {
            return null;
        }

        var marker = markerReadResult.Marker!;
        var probeResult = await processProbe.Probe(marker, cancellationToken).ConfigureAwait(false);
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

        var waitResult = await sessionRegistrationAwaiter.WaitForSession(
                unityProject,
                marker.ProcessId,
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (waitResult.IsSuccess)
        {
            return DaemonStartResult.AlreadyRunning(waitResult.Session!);
        }

        if (waitResult.Error!.Kind != ExecutionErrorKind.Timeout)
        {
            return DaemonStartResult.Failure(waitResult.Error);
        }

        var timeoutError = CreateGuiEndpointTimeoutError(marker, waitResult.Error);
        var diagnosisWriteResult = await daemonDiagnosisStore.Write(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                CreateGuiEndpointNotRegisteredDiagnosis(marker, timeoutError.Message),
                CancellationToken.None)
            .ConfigureAwait(false);
        if (!diagnosisWriteResult.IsSuccess)
        {
            return DaemonStartResult.Failure(CreateAugmentedPrimaryError(timeoutError, diagnosisWriteResult.Error!));
        }

        return DaemonStartResult.Failure(timeoutError);
    }

    private DaemonDiagnosis CreateGuiEndpointNotRegisteredDiagnosis (
        UnityEditorInstanceMarker marker,
        string message)
    {
        var updatedAtUtc = timeProvider.GetUtcNow();
        return new DaemonDiagnosis(
            Reason: DaemonDiagnosisReasonValues.GuiEndpointNotRegistered,
            Message: message,
            ReportedBy: DaemonDiagnosisReportedByValues.Cli,
            IsInferred: true,
            UpdatedAtUtc: updatedAtUtc,
            ProcessId: marker.ProcessId,
            EditorInstancePath: marker.MarkerPath,
            SessionIssuedAtUtc: updatedAtUtc);
    }

    private static ExecutionError CreateGuiEndpointTimeoutError (
        UnityEditorInstanceMarker marker,
        ExecutionError waitError)
    {
        return ExecutionError.Timeout(
            "Timed out while waiting for existing GUI Editor endpoint registration. " +
            $"reason={DaemonDiagnosisReasonValues.GuiEndpointNotRegistered} " +
            $"editorInstancePath={marker.MarkerPath} processId={marker.ProcessId}. " +
            waitError.Message,
            ExecutionErrorCodes.IpcTimeout);
    }

    private static ExecutionError CreateAugmentedPrimaryError (
        ExecutionError primaryError,
        ExecutionError diagnosisError)
    {
        return ExecutionError.Timeout(
            "Existing GUI Editor endpoint registration timed out and diagnosis persistence failed. " +
            $"StartError={primaryError.Message} DiagnosisError={diagnosisError.Message}",
            primaryError.Code);
    }
}
