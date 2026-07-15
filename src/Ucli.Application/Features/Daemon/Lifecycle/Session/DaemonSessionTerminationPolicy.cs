namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Resolves safe daemon process termination targets from validated session metadata. </summary>
internal static class DaemonSessionTerminationPolicy
{
    /// <summary> Tries to resolve a process termination target from one validated runtime session. </summary>
    /// <param name="session"> The validated runtime session. </param>
    /// <param name="target"> The safe process termination target when available. </param>
    /// <returns> <see langword="true" /> when a target is available; otherwise <see langword="false" />. </returns>
    public static bool TryGetTerminationTarget (
        DaemonSession session,
        out DaemonProcessTerminationTarget target)
    {
        ArgumentNullException.ThrowIfNull(session);

        target = default;
        if (!session.CanShutdownProcess || session.ProcessId is not int processId)
        {
            return false;
        }

        target = new DaemonProcessTerminationTarget(processId, session.ProcessStartedAtUtc);
        return true;
    }

}
