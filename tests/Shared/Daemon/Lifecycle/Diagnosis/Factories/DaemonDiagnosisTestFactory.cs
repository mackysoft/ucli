using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.TestSupport;

internal static class DaemonDiagnosisTestFactory
{
    public static DaemonDiagnosis Create (
        DaemonDiagnosisReason reason = DaemonDiagnosisReason.ShutdownRequested,
        string message = "daemon shutdown completed",
        DaemonDiagnosisReportedBy reportedBy = DaemonDiagnosisReportedBy.Unity,
        bool isInferred = false,
        DateTimeOffset? updatedAtUtc = null,
        int? processId = 1234,
        AbsolutePath? editorInstancePath = null,
        DateTimeOffset? sessionIssuedAtUtc = null,
        DateTimeOffset? processStartedAtUtc = null,
        AbsolutePath? unityLogPath = null,
        DaemonDiagnosisStartupPhase? startupPhase = null,
        DaemonDiagnosisActionRequired? actionRequired = null,
        DaemonPrimaryDiagnostic? primaryDiagnostic = null)
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
            ProcessStartedAtUtc: processStartedAtUtc,
            UnityLogPath: unityLogPath,
            StartupPhase: startupPhase,
            ActionRequired: actionRequired,
            PrimaryDiagnostic: primaryDiagnostic);
    }

    public static DaemonDiagnosis CreateGuiEndpointNotRegistered ()
    {
        return Create(
            reason: DaemonDiagnosisReason.GuiEndpointNotRegistered,
            message: "GUI endpoint not registered.",
            reportedBy: DaemonDiagnosisReportedBy.Cli,
            isInferred: true,
            updatedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 3, 0, TimeSpan.Zero),
            editorInstancePath: AbsolutePath.Parse(Path.Combine(
                ProjectPathTestValues.RepositoryUnityProject,
                "Library",
                "EditorInstance.json")),
            sessionIssuedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 2, 0, TimeSpan.Zero));
    }
}
