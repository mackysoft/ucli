using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process;

/// <summary> Implements daemon reachability classification using host-side IPC exception rules. </summary>
internal sealed class DaemonReachabilityClassifier : IDaemonReachabilityClassifier
{
    /// <inheritdoc />
    public bool IsNotRunning (Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return DaemonProbeExceptionClassifier.IsNotRunning(exception);
    }
}
