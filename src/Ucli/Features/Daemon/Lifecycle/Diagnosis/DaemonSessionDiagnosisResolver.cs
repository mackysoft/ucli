using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Execution;

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
    public async ValueTask<DaemonDiagnosis?> ResolveForSessionAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        DaemonDiagnosis? persistedDiagnosis,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(session);

        if (persistedDiagnosis is not null
            && IsPersistedDiagnosisForSession(persistedDiagnosis, session))
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
            EditorInstancePath: null,
            SessionIssuedAtUtc: session.IssuedAtUtc,
            ProcessStartedAtUtc: session.ProcessStartedAtUtc);

        var writeResult = await daemonDiagnosisStore.WriteAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                diagnosis,
                cancellationToken)
            .ConfigureAwait(false);
        if (!writeResult.IsSuccess)
        {
            // NOTE: Synthesized diagnosis is supplemental metadata for later inspection.
            // Observation should still return the inferred diagnosis even if sidecar persistence fails.
        }

        return diagnosis;
    }

    private static bool IsPersistedDiagnosisForSession (
        DaemonDiagnosis persistedDiagnosis,
        DaemonSession session)
    {
        if (persistedDiagnosis.SessionIssuedAtUtc != session.IssuedAtUtc)
        {
            return false;
        }

        return persistedDiagnosis.ProcessStartedAtUtc is null
            || session.ProcessStartedAtUtc is null
            || persistedDiagnosis.ProcessStartedAtUtc == session.ProcessStartedAtUtc;
    }
}
