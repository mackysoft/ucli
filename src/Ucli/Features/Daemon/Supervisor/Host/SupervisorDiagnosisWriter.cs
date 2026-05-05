using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Host;

/// <summary> Persists supervisor-observed daemon diagnosis records. </summary>
internal sealed class SupervisorDiagnosisWriter
{
    private readonly IDaemonDiagnosisStore daemonDiagnosisStore;

    /// <summary> Initializes a new instance of the <see cref="SupervisorDiagnosisWriter" /> class. </summary>
    /// <param name="daemonDiagnosisStore"> The daemon diagnosis-store dependency. </param>
    public SupervisorDiagnosisWriter (IDaemonDiagnosisStore daemonDiagnosisStore)
    {
        this.daemonDiagnosisStore = daemonDiagnosisStore ?? throw new ArgumentNullException(nameof(daemonDiagnosisStore));
    }

    /// <summary> Writes one non-inferred diagnosis emitted by the supervisor runtime. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The daemon session associated with the diagnosis. </param>
    /// <param name="reason"> The diagnosis reason value. </param>
    /// <param name="message"> The diagnosis message body. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    public async ValueTask<DaemonDiagnosisStoreOperationResult> WriteUnexpected (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        string reason,
        string message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(session);

        var diagnosis = new DaemonDiagnosis(
            Reason: reason,
            Message: message,
            ReportedBy: DaemonDiagnosisReportedByValues.Cli,
            IsInferred: false,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            ProcessId: session.ProcessId,
            SessionIssuedAtUtc: session.IssuedAtUtc);
        return await daemonDiagnosisStore.Write(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                diagnosis,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
