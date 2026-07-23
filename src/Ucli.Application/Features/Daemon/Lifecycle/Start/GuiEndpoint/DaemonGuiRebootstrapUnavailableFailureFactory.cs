using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiEndpoint;

/// <summary> Creates the shared failure contract used when a GUI process cannot accept rebootstrap requests. </summary>
internal static class DaemonGuiRebootstrapUnavailableFailureFactory
{
    public static async ValueTask<DaemonStartResult> CreateFailureAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonCompensationOperationOwner operationOwner,
        IDaemonDiagnosisStore daemonDiagnosisStore,
        TimeProvider timeProvider,
        string editorInstancePath,
        int processId,
        DateTimeOffset? processStartedAtUtc,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        ExecutionError rebootstrapError,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(operationOwner);
        ArgumentNullException.ThrowIfNull(daemonDiagnosisStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(editorInstancePath);
        ArgumentNullException.ThrowIfNull(rebootstrapError);

        var error = ExecutionError.InternalError(
            $"GUI daemon rebootstrap is unavailable. reason={TextVocabulary.GetText(DaemonDiagnosisReason.GuiRebootstrapUnavailable)} processId={processId}. {rebootstrapError.Message}",
            DaemonErrorCodes.DaemonEndpointNotRegistered);
        var diagnosis = new DaemonDiagnosis(
            Reason: DaemonDiagnosisReason.GuiRebootstrapUnavailable,
            Message: error.Message,
            ReportedBy: DaemonDiagnosisReportedBy.Cli,
            IsInferred: true,
            UpdatedAtUtc: timeProvider.GetUtcNow(),
            ProcessId: processId,
            EditorInstancePath: editorInstancePath,
            SessionIssuedAtUtc: timeProvider.GetUtcNow(),
            ProcessStartedAtUtc: processStartedAtUtc,
            UnityLogPath: null,
            StartupPhase: DaemonDiagnosisStartupPhase.EndpointRegistration,
            ActionRequired: DaemonDiagnosisActionRequired.InspectUnityLog,
            PrimaryDiagnostic: null);
        var persistenceDeadline = ExecutionDeadline.Start(
            DaemonTimeouts.SupplementalPersistenceTimeout,
            timeProvider);
        var persistenceExecution = await operationOwner.ExecuteAsync(
                unityProject,
                DaemonOperationLane.SupplementalPersistence,
                persistenceDeadline,
                cancellationToken,
                "Timed out before GUI rebootstrap diagnosis persistence could begin.",
                "Timed out while persisting GUI rebootstrap diagnosis.",
                (_, ownedCancellationToken) => daemonDiagnosisStore.WriteAsync(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    diagnosis,
                    ownedCancellationToken))
            .ConfigureAwait(false);
        var diagnosisWriteResult = persistenceExecution.IsSuccess
            ? persistenceExecution.Value!
            : DaemonDiagnosisStoreOperationResult.Failure(persistenceExecution.Error!);
        if (!diagnosisWriteResult.IsSuccess)
        {
            error = ExecutionError.InternalError(
                $"GUI daemon rebootstrap is unavailable and diagnosis persistence failed. StartError={error.Message} DiagnosisError={diagnosisWriteResult.Error!.Message}",
                error.Code);
        }

        var policyResolution = DaemonStartupBlockedProcessPolicyResolver.Resolve(
            onStartupBlocked,
            DaemonEditorMode.Gui,
            DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            processId);
        var startup = new DaemonStartupObservation(
            StartupStatus: DaemonStartupStatus.Failed,
            StartupBlockingReason: DaemonStartupBlockingReason.EndpointNotRegistered,
            LaunchAttemptId: null,
            ProcessAction: policyResolution.ProcessActionWhenNotTerminated,
            RetryDisposition: DaemonStartupRetryDisposition.Unknown,
            EditorMode: DaemonEditorMode.Gui,
            OwnerKind: DaemonSessionOwnerKind.User,
            CanShutdownProcess: false,
            ProcessId: processId,
            StartedAtUtc: processStartedAtUtc,
            ElapsedMilliseconds: null,
            ArtifactPath: null);

        return DaemonStartResult.Failure(error, diagnosis, startup);
    }
}
