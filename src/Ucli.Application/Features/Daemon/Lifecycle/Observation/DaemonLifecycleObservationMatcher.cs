using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;

/// <summary> Validates lifecycle observations against daemon session identity. </summary>
internal static class DaemonLifecycleObservationMatcher
{
    /// <summary> Determines whether one lifecycle observation belongs to the specified daemon session process. </summary>
    public static bool MatchesSession (
        DaemonLifecycleObservation observation,
        DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(session);

        return session.ProcessId == observation.ProcessId
            && MatchesProcessIdentity(session, observation)
            && session.EditorMode == observation.State.EditorMode;
    }

    /// <summary> Determines whether one lifecycle observation belongs to the specified daemon session editor instance. </summary>
    public static bool MatchesSessionByEditorInstance (
        DaemonLifecycleObservation observation,
        DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(session);

        // NOTE:
        // Domain-reload recovery must use deterministic editor-instance identity. Process start time remains a
        // live-process guard elsewhere, but it must not prove ownership of a recovering lifecycle sidecar.
        return session.ProcessId == observation.ProcessId
            && session.EditorMode == observation.State.EditorMode
            && MatchesEditorInstance(session, observation);
    }

    private static bool MatchesProcessIdentity (
        DaemonSession session,
        DaemonLifecycleObservation observation)
    {
        var hasSessionEditorInstanceId = !string.IsNullOrWhiteSpace(session.EditorInstanceId);
        var hasObservationEditorInstanceId = !string.IsNullOrWhiteSpace(observation.EditorInstanceId);

        // NOTE: editorInstanceId is the stable daemon identity across Unity domain reload.
        // Process start time is only a legacy fallback when neither artifact carries that id.
        if (hasSessionEditorInstanceId && hasObservationEditorInstanceId)
        {
            return string.Equals(session.EditorInstanceId, observation.EditorInstanceId, StringComparison.Ordinal);
        }

        if (hasSessionEditorInstanceId || hasObservationEditorInstanceId)
        {
            return false;
        }

        return session.ProcessStartedAtUtc.HasValue
            && DaemonProcessStartTimeMatcher.Matches(
                observation.ProcessStartedAtUtc,
                session.ProcessStartedAtUtc.Value);
    }

    private static bool MatchesEditorInstance (
        DaemonSession session,
        DaemonLifecycleObservation observation)
    {
        return !string.IsNullOrWhiteSpace(session.EditorInstanceId)
            && !string.IsNullOrWhiteSpace(observation.EditorInstanceId)
            && string.Equals(session.EditorInstanceId, observation.EditorInstanceId, StringComparison.Ordinal);
    }
}
