using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;

/// <summary> Evaluates whether a persisted lifecycle observation can stand in for a delayed daemon IPC response. </summary>
internal static class DaemonLifecycleObservationAvailability
{
    /// <summary> Gets the maximum age accepted for sidecar-backed daemon state projection. </summary>
    public static TimeSpan FreshnessWindow { get; } = TimeSpan.FromSeconds(5);

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
        // The lifecycle sidecar is written by Unity main-thread callbacks. It may replace an IPC response only
        // when it is fresh and tied to the same live GUI process; otherwise an old file could hide a dead daemon.
        return DaemonLifecycleObservationMatcher.MatchesSession(observation, session)
            && IsFresh(observation, timeProvider)
            && IsMatchingLiveProcess(session, processIdentityAssessor);
    }

    private static bool IsFresh (
        DaemonLifecycleObservation observation,
        TimeProvider timeProvider)
    {
        var age = timeProvider.GetUtcNow() - observation.ObservedAtUtc.ToUniversalTime();
        return age.Duration() <= FreshnessWindow;
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
