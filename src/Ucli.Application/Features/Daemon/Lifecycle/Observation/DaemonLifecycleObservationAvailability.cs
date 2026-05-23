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
        // The lifecycle sidecar is written by Unity main-thread callbacks. The sidecar path intentionally requires
        // editorInstanceId so file ownership is deterministic; process start time is only a live-process guard here.
        return MatchesEditorInstance(observation, session)
            && IsFresh(observation, timeProvider)
            && IsMatchingLiveProcess(session, processIdentityAssessor);
    }

    private static bool MatchesEditorInstance (
        DaemonLifecycleObservation observation,
        DaemonSession session)
    {
        return session.ProcessId == observation.ProcessId
            && string.Equals(session.EditorMode, observation.EditorMode, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(session.EditorInstanceId)
            && !string.IsNullOrWhiteSpace(observation.EditorInstanceId)
            && string.Equals(session.EditorInstanceId, observation.EditorInstanceId, StringComparison.Ordinal);
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
