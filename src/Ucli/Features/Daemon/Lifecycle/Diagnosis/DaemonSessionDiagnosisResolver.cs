using MackySoft.Ucli.Contracts.Execution;
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
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.UnityIntegration.Project;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;

/// <summary> Resolves persisted or synthesized daemon diagnosis metadata for one observed daemon session. </summary>
internal sealed class DaemonSessionDiagnosisResolver : IDaemonSessionDiagnosisResolver
{
    private readonly IDaemonDiagnosisStore daemonDiagnosisStore;

    /// <summary> Initializes a new instance of the <see cref="DaemonSessionDiagnosisResolver" /> class. </summary>
    /// <param name="daemonDiagnosisStore"> The daemon diagnosis-store dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="daemonDiagnosisStore" /> is <see langword="null" />. </exception>
    public DaemonSessionDiagnosisResolver (IDaemonDiagnosisStore daemonDiagnosisStore)
    {
        this.daemonDiagnosisStore = daemonDiagnosisStore ?? throw new ArgumentNullException(nameof(daemonDiagnosisStore));
    }

    /// <inheritdoc />
    public async ValueTask<DaemonDiagnosis?> ResolveForSession (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        DaemonDiagnosis? persistedDiagnosis,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(session);

        if (persistedDiagnosis is not null
            && persistedDiagnosis.SessionIssuedAtUtc == session.IssuedAtUtc)
        {
            return persistedDiagnosis;
        }

        var processId = session.ProcessId;
        if (processId is not int resolvedProcessId || ProcessLivenessProbe.IsAlive(resolvedProcessId))
        {
            return null;
        }

        var diagnosis = new DaemonDiagnosis(
            Reason: DaemonDiagnosisReasonValues.ExternalTerminationSuspected,
            Message: "Daemon process is no longer alive and no persisted diagnosis matched the current session.",
            ReportedBy: DaemonDiagnosisReportedByValues.Cli,
            IsInferred: true,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            ProcessId: resolvedProcessId,
            SessionIssuedAtUtc: session.IssuedAtUtc);

        var writeResult = await daemonDiagnosisStore.Write(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                diagnosis,
                CancellationToken.None)
            .ConfigureAwait(false);
        if (!writeResult.IsSuccess)
        {
            // NOTE: Synthesized diagnosis is supplemental metadata for later inspection.
            // Observation should still return the inferred diagnosis even if sidecar persistence fails.
        }

        return diagnosis;
    }
}