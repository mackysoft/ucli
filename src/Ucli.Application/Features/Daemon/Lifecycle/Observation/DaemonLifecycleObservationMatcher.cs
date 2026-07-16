using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;

/// <summary> Validates lifecycle observations against the runtime generation identity defined by session ownership. </summary>
internal static class DaemonLifecycleObservationMatcher
{
    /// <summary> Determines whether one lifecycle observation belongs to the specified daemon session runtime generation. </summary>
    public static bool MatchesSession (
        DaemonLifecycleObservation observation,
        DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(session);

        if (session.ProcessId != observation.ProcessId
            || session.EditorMode != observation.State.EditorMode)
        {
            return false;
        }

        return session.OwnerKind switch
        {
            DaemonSessionOwnerKind.User => session.EditorInstanceId == observation.EditorInstanceId,
            DaemonSessionOwnerKind.Cli => session.ProcessStartedAtUtc.HasValue
                && DaemonProcessStartTimeMatcher.Matches(
                    observation.ProcessStartedAtUtc,
                    session.ProcessStartedAtUtc.Value),
            _ => false,
        };
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
            && session.EditorInstanceId == observation.EditorInstanceId;
    }
}
