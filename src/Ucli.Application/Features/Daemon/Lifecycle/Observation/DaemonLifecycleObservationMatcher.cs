using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Text;

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
            || !ContractLiteralCodec.Matches(observation.EditorMode, session.EditorMode))
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
}
