using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;

/// <summary> Creates the shared error and diagnosis contract for GUI endpoint registration timeouts. </summary>
internal static class DaemonGuiEndpointNotRegisteredFailureFactory
{
    /// <summary> Creates the timeout error returned when a GUI endpoint is not registered within the start budget. </summary>
    public static ExecutionError CreateTimeoutError (
        string endpointOwnerDescription,
        string editorInstancePath,
        int? processId,
        ExecutionError waitError)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointOwnerDescription);
        ArgumentException.ThrowIfNullOrWhiteSpace(editorInstancePath);
        ArgumentNullException.ThrowIfNull(waitError);

        return ExecutionError.Timeout(
            $"Timed out while waiting for {endpointOwnerDescription} endpoint registration. " +
            $"reason={DaemonDiagnosisReasonValues.GuiEndpointNotRegistered} " +
            $"editorInstancePath={editorInstancePath} processId={processId}. " +
            waitError.Message,
            ExecutionErrorCodes.IpcTimeout);
    }

    /// <summary> Creates the diagnosis persisted and projected for a GUI endpoint registration timeout. </summary>
    public static DaemonDiagnosis CreateDiagnosis (
        string message,
        int? processId,
        string editorInstancePath,
        DateTimeOffset updatedAtUtc)
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
            SessionIssuedAtUtc: updatedAtUtc);
    }
}
