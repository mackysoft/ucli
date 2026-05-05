using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;

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
