using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;

/// <summary> Evaluates whether a persisted lifecycle observation can stand in for a delayed daemon IPC response. </summary>
internal static class DaemonLifecycleObservationAvailability
{
    /// <summary> Determines whether the observation belongs to the session, is fresh, and still points to the live process. </summary>
    public static bool IsUsableForSession (
        DaemonLifecycleObservation observation,
        DaemonSession session,
        IDaemonProcessIdentityAssessor processIdentityAssessor,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(processIdentityAssessor);
        ArgumentNullException.ThrowIfNull(timeProvider);

        // NOTE:
        // User-owned GUI sessions are correlated by Editor instance identity. CLI-owned launched sessions are
        // correlated by the process identity captured by the launcher because their prelaunch session has no
        // Editor instance identity yet.
        return DaemonLifecycleObservationMatcher.MatchesSession(observation, session)
            && IsFresh(observation, timeProvider)
            && IsMatchingLiveProcess(session, processIdentityAssessor);
    }

    private static bool IsFresh (
        DaemonLifecycleObservation observation,
        TimeProvider timeProvider)
    {
        var age = timeProvider.GetUtcNow() - observation.ObservedAtUtc.ToUniversalTime();
        return age.Duration() <= DaemonLifecycleObservationTimings.FreshnessWindow;
    }

    private static bool IsMatchingLiveProcess (
        DaemonSession session,
        IDaemonProcessIdentityAssessor processIdentityAssessor)
    {
        if (session.ProcessId is not int processId)
        {
            return false;
        }

        return processIdentityAssessor.AssessByProcessId(processId, session.ProcessStartedAtUtc).Status
            == DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess;
    }
}
