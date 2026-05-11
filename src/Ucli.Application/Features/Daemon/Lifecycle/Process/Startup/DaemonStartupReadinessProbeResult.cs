using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;

/// <summary> Represents daemon startup readiness probing result. </summary>
/// <param name="IsReady"> Whether daemon startup probe succeeded. </param>
/// <param name="Error"> The structured error when probe failed. </param>
/// <param name="LifecycleSnapshot"> The endpoint-registered lifecycle snapshot when probing succeeded. </param>
internal sealed record DaemonStartupReadinessProbeResult (
    bool IsReady,
    ExecutionError? Error,
    DaemonStartLifecycleSnapshot? LifecycleSnapshot)
{
    /// <summary> Creates a successful readiness-probe result. </summary>
    /// <param name="lifecycleSnapshot"> The endpoint-registered lifecycle snapshot. </param>
    /// <returns> The successful readiness-probe result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="lifecycleSnapshot" /> is <see langword="null" />. </exception>
    public static DaemonStartupReadinessProbeResult Ready (DaemonStartLifecycleSnapshot lifecycleSnapshot)
    {
        ArgumentNullException.ThrowIfNull(lifecycleSnapshot);
        return new DaemonStartupReadinessProbeResult(true, null, lifecycleSnapshot);
    }

    /// <summary> Creates a failed readiness-probe result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed readiness-probe result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonStartupReadinessProbeResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonStartupReadinessProbeResult(false, error, null);
    }
}
