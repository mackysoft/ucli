using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.TestSupport;

internal static class DaemonDiagnosisTestFactory
{
    public static DaemonDiagnosis Create (
        string reason = DaemonDiagnosisReasonValues.ShutdownRequested,
        string message = "daemon shutdown completed",
        string reportedBy = DaemonDiagnosisReportedByValues.Unity,
        bool isInferred = false,
        DateTimeOffset? updatedAtUtc = null,
        int? processId = 1234,
        string? editorInstancePath = null,
        DateTimeOffset? sessionIssuedAtUtc = null,
        DateTimeOffset? processStartedAtUtc = null)
    {
        return new DaemonDiagnosis(
            Reason: reason,
            Message: message,
            ReportedBy: reportedBy,
            IsInferred: isInferred,
            UpdatedAtUtc: updatedAtUtc ?? new DateTimeOffset(2026, 03, 05, 4, 5, 6, TimeSpan.Zero),
            ProcessId: processId,
            EditorInstancePath: editorInstancePath,
            SessionIssuedAtUtc: sessionIssuedAtUtc ?? new DateTimeOffset(2026, 03, 05, 0, 0, 0, TimeSpan.Zero),
            ProcessStartedAtUtc: processStartedAtUtc);
    }

    public static DaemonDiagnosis CreateGuiEndpointNotRegistered ()
    {
        return Create(
            reason: DaemonDiagnosisReasonValues.GuiEndpointNotRegistered,
            message: "GUI endpoint not registered.",
            reportedBy: DaemonDiagnosisReportedByValues.Cli,
            isInferred: true,
            updatedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 3, 0, TimeSpan.Zero),
            editorInstancePath: "/repo/UnityProject/Library/EditorInstance.json",
            sessionIssuedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 2, 0, TimeSpan.Zero));
    }
}
