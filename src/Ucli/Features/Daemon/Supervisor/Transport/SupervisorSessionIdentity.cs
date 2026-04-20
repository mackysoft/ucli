using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Transport;

/// <summary> Provides identity comparison helpers for daemon sessions observed by the supervisor runtime. </summary>
internal static class SupervisorSessionIdentity
{
    /// <summary> Determines whether two daemon sessions identify the same supervisor-owned runtime instance. </summary>
    /// <param name="left"> The first daemon session. </param>
    /// <param name="right"> The second daemon session. </param>
    /// <returns> <see langword="true" /> when both sessions identify the same runtime instance; otherwise <see langword="false" />. </returns>
    public static bool IsSameSession (
        DaemonSession left,
        DaemonSession right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return string.Equals(left.SessionToken, right.SessionToken, StringComparison.Ordinal)
            && string.Equals(left.ProjectFingerprint, right.ProjectFingerprint, StringComparison.Ordinal)
            && left.IssuedAtUtc == right.IssuedAtUtc;
    }
}