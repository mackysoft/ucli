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
            && session.ProcessStartedAtUtc == observation.ProcessStartedAtUtc
            && string.Equals(session.EditorMode, observation.EditorMode, StringComparison.Ordinal);
    }
}
