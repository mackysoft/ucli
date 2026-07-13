using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiEndpoint;

/// <summary> Creates the shared error and diagnosis contract for GUI endpoint registration timeouts. </summary>
internal static class DaemonGuiEndpointNotRegisteredFailureFactory
{
    /// <summary> Creates the timeout error returned when a GUI endpoint is not registered within the start budget. </summary>
    public static async ValueTask<DaemonStartResult> CreateFailureAsync (
        ResolvedUnityProjectContext unityProject,
        IDaemonDiagnosisStore daemonDiagnosisStore,
        TimeProvider timeProvider,
        string endpointOwnerDescription,
        string editorInstancePath,
        int? processId,
        ExecutionError waitError,
        DateTimeOffset? processStartedAtUtc = null,
        string? unityLogPath = null)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(daemonDiagnosisStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(editorInstancePath);

        var timeoutError = CreateTimeoutError(endpointOwnerDescription, processId, waitError);
        var diagnosis = CreateDiagnosis(
            timeoutError.Message,
            processId,
            editorInstancePath,
            timeProvider.GetUtcNow(),
            processStartedAtUtc,
            unityLogPath);
        var diagnosisWriteResult = await daemonDiagnosisStore.WriteAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                diagnosis,
                CancellationToken.None)
            .ConfigureAwait(false);
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
            $"reason={DaemonDiagnosisReasonValues.GuiEndpointNotRegistered} " +
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
            Reason: DaemonDiagnosisReasonValues.GuiEndpointNotRegistered,
            Message: message,
            ReportedBy: DaemonDiagnosisReportedByValues.Cli,
            IsInferred: true,
            UpdatedAtUtc: updatedAtUtc,
            ProcessId: processId,
            EditorInstancePath: editorInstancePath,
            SessionIssuedAtUtc: updatedAtUtc,
            ProcessStartedAtUtc: processStartedAtUtc,
            UnityLogPath: unityLogPath,
            StartupPhase: DaemonDiagnosisStartupPhase.EndpointRegistration,
            ActionRequired: DaemonDiagnosisActionRequiredValues.InspectUnityLog);
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
