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
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;

/// <summary> Represents daemon startup readiness probing result. </summary>
/// <param name="IsReady"> Whether daemon startup probe succeeded. </param>
/// <param name="Error"> The structured error when probe failed. </param>
internal sealed record DaemonStartupReadinessProbeResult (
    bool IsReady,
    ExecutionError? Error)
{
    /// <summary> Creates a successful readiness-probe result. </summary>
    /// <returns> The successful readiness-probe result. </returns>
    public static DaemonStartupReadinessProbeResult Ready ()
    {
        return new DaemonStartupReadinessProbeResult(true, null);
    }

    /// <summary> Creates a failed readiness-probe result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed readiness-probe result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonStartupReadinessProbeResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonStartupReadinessProbeResult(false, error);
    }
}
