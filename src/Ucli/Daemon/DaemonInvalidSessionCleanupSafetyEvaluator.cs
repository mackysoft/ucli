using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon;

/// <summary> Evaluates whether invalid daemon session artifacts can be cleaned safely without stopping live processes. </summary>
internal sealed class DaemonInvalidSessionCleanupSafetyEvaluator : IDaemonInvalidSessionCleanupSafetyEvaluator
{
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
            return true;
        }

        if (!string.Equals(session.ProjectFingerprint, unityProject.ProjectFingerprint, StringComparison.Ordinal))
        {
            return true;
        }

        if (session.ProcessId is not int processId || processId <= 0 || session.IssuedAtUtc == default)
        {
            return true;
        }

        var identityAssessment = DaemonProcessIdentityProbe.Assess(processId, session.IssuedAtUtc);
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