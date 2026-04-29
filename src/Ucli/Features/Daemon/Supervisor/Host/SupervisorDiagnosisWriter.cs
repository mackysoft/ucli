using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Shared.Context.Project;

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
