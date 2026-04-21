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
using MackySoft.Ucli.Shared.Context.Project;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Evaluates whether invalid daemon session artifacts must be skipped as unsafe without stopping live processes. </summary>
internal sealed class DaemonInvalidSessionCleanupSafetyEvaluator : IDaemonInvalidSessionCleanupSafetyEvaluator
{
    private readonly IDaemonProcessIdentityAssessor daemonProcessIdentityAssessor;

    /// <summary> Initializes a new instance of the <see cref="DaemonInvalidSessionCleanupSafetyEvaluator" /> class. </summary>
    /// <param name="daemonProcessIdentityAssessor"> The daemon process-identity assessor dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="daemonProcessIdentityAssessor" /> is <see langword="null" />. </exception>
    public DaemonInvalidSessionCleanupSafetyEvaluator (IDaemonProcessIdentityAssessor daemonProcessIdentityAssessor)
    {
        this.daemonProcessIdentityAssessor = daemonProcessIdentityAssessor ?? throw new ArgumentNullException(nameof(daemonProcessIdentityAssessor));
    }

    /// <summary> Determines whether invalid daemon session artifacts must be skipped as unsafe. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The parsed invalid daemon session snapshot when available; otherwise <see langword="null" />. </param>
    /// <returns>
    /// <para> <see langword="true" /> when the parsed invalid session still provides enough live-process evidence that cleanup must be skipped immediately as unsafe. </para>
    /// <para> <see langword="false" /> when this evaluator does not force an unsafe skip; callers still need separate endpoint evidence before destructive cleanup. </para>
    /// </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    public bool RequiresUnsafeSkip (
        ResolvedUnityProjectContext unityProject,
        DaemonSession? session)
    {
        ArgumentNullException.ThrowIfNull(unityProject);

        if (session == null)
        {
            return false;
        }

        if (!string.Equals(session.ProjectFingerprint, unityProject.ProjectFingerprint, StringComparison.Ordinal))
        {
            return false;
        }

        // NOTE:
        // Invalid session snapshots are not trusted enough to authorize destructive cleanup of the
        // canonical endpoint. Snapshot process identity is therefore used only to force an unsafe
        // skip when it still points to a plausible live daemon for the current project.
        if (session.ProcessId is not int processId || processId <= 0 || session.IssuedAtUtc == default)
        {
            return false;
        }

        var identityAssessment = daemonProcessIdentityAssessor.AssessByProcessId(processId, session.IssuedAtUtc);
        return identityAssessment.Status switch
        {
            DaemonProcessIdentityAssessmentStatus.NotRunning => false,
            DaemonProcessIdentityAssessmentStatus.DifferentProcess => false,
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess => true,
            DaemonProcessIdentityAssessmentStatus.Uncertain => true,
            _ => throw new ArgumentOutOfRangeException(nameof(identityAssessment), identityAssessment.Status, "Unsupported daemon process identity assessment status."),
        };
    }
}