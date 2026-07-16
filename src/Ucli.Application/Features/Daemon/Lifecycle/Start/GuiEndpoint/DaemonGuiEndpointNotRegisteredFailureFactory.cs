using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiEndpoint;

/// <summary> Creates the shared error and diagnosis contract for GUI endpoint registration timeouts. </summary>
internal static class DaemonGuiEndpointNotRegisteredFailureFactory
{
    /// <summary> Creates the timeout error returned when a GUI endpoint is not registered within the start budget. </summary>
    public static async ValueTask<DaemonStartResult> CreateFailureAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonCompensationOperationOwner operationOwner,
        IDaemonDiagnosisStore daemonDiagnosisStore,
        TimeProvider timeProvider,
        string endpointOwnerDescription,
        string editorInstancePath,
        int? processId,
        ExecutionError waitError,
        DateTimeOffset? processStartedAtUtc,
        string? unityLogPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(operationOwner);
        ArgumentNullException.ThrowIfNull(daemonDiagnosisStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(editorInstancePath);
        cancellationToken.ThrowIfCancellationRequested();

        var timeoutError = CreateTimeoutError(endpointOwnerDescription, processId, waitError);
        var diagnosis = CreateDiagnosis(
            timeoutError.Message,
            processId,
            editorInstancePath,
            timeProvider.GetUtcNow(),
            processStartedAtUtc,
            unityLogPath);
        var persistenceDeadline = ExecutionDeadline.Start(
            DaemonTimeouts.SupplementalPersistenceTimeout,
            timeProvider);
        var persistenceExecution = await operationOwner.ExecuteAsync(
                unityProject,
                DaemonOperationLane.SupplementalPersistence,
                persistenceDeadline,
                cancellationToken,
                "Timed out before GUI endpoint diagnosis persistence could begin.",
                "Timed out while persisting GUI endpoint diagnosis.",
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
            return DaemonStartResult.Failure(
                CreateAugmentedPrimaryError(endpointOwnerDescription, timeoutError, diagnosisWriteResult.Error!),
                diagnosis);
        }

        return DaemonStartResult.Failure(timeoutError, diagnosis);
    }

    private static ExecutionError CreateTimeoutError (
        string endpointOwnerDescription,
        int? processId,
        ExecutionError waitError)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointOwnerDescription);
        ArgumentNullException.ThrowIfNull(waitError);

        return ExecutionError.Timeout(
            $"Timed out while waiting for {endpointOwnerDescription} endpoint registration. " +
            $"reason={ContractLiteralCodec.ToValue(DaemonDiagnosisReason.GuiEndpointNotRegistered)} " +
            $"processId={processId}. " +
            waitError.Message,
            ExecutionErrorCodes.IpcTimeout);
    }

    private static DaemonDiagnosis CreateDiagnosis (
        string message,
        int? processId,
        string editorInstancePath,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? processStartedAtUtc,
        string? unityLogPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(editorInstancePath);

        return new DaemonDiagnosis(
            Reason: DaemonDiagnosisReason.GuiEndpointNotRegistered,
            Message: message,
            ReportedBy: DaemonDiagnosisReportedBy.Cli,
            IsInferred: true,
            UpdatedAtUtc: updatedAtUtc,
            ProcessId: processId,
            EditorInstancePath: editorInstancePath,
            SessionIssuedAtUtc: updatedAtUtc,
            ProcessStartedAtUtc: processStartedAtUtc,
            UnityLogPath: unityLogPath,
            StartupPhase: DaemonDiagnosisStartupPhase.EndpointRegistration,
            ActionRequired: DaemonDiagnosisActionRequired.InspectUnityLog,
            PrimaryDiagnostic: null);
    }

    private static ExecutionError CreateAugmentedPrimaryError (
        string endpointOwnerDescription,
        ExecutionError primaryError,
        ExecutionError diagnosisError)
    {
        return ExecutionError.Timeout(
            $"{endpointOwnerDescription} endpoint registration timed out and diagnosis persistence failed. " +
            $"StartError={primaryError.Message} DiagnosisError={diagnosisError.Message}",
            primaryError.Code);
    }
}
