using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Daemon;

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