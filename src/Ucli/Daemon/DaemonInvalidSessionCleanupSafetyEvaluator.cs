using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon;

/// <summary> Evaluates whether invalid daemon session artifacts can be cleaned safely without stopping live processes. </summary>
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

    /// <summary> Determines whether invalid daemon session artifacts can be cleaned safely. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The parsed invalid daemon session snapshot when available; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when cleanup is safe; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    public bool CanCleanup (
        ResolvedUnityProjectContext unityProject,
        DaemonSession? session)
    {
        ArgumentNullException.ThrowIfNull(unityProject);

        if (session == null)
        {
            return false;
        }

        // NOTE:
        // Safe cleanup must not delete the canonical endpoint unless the previous daemon identity can
        // be disproven. Broken metadata, including fingerprint mismatch, is therefore unsafe unless
        // process identity still proves the previous daemon is gone.
        if (session.ProcessId is not int processId || processId <= 0 || session.IssuedAtUtc == default)
        {
            return false;
        }

        var identityAssessment = daemonProcessIdentityAssessor.AssessByProcessId(processId, session.IssuedAtUtc);
        return identityAssessment.Status switch
        {
            DaemonProcessIdentityAssessmentStatus.NotRunning => true,
            DaemonProcessIdentityAssessmentStatus.DifferentProcess => true,
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess => false,
            DaemonProcessIdentityAssessmentStatus.Uncertain => false,
            _ => throw new ArgumentOutOfRangeException(nameof(identityAssessment), identityAssessment.Status, "Unsupported daemon process identity assessment status."),
        };
    }
}