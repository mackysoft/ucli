using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

namespace MackySoft.Ucli.Features.Daemon.Runtime;

/// <summary> Implements daemon reachability classification using shared probe exception rules. </summary>
internal sealed class DaemonReachabilityClassifier : IDaemonReachabilityClassifier
{
    /// <summary> Determines whether one exception means daemon endpoint is not running. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when endpoint is treated as not running; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="exception" /> is <see langword="null" />. </exception>
    public bool IsNotRunning (Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return DaemonProbeExceptionClassifier.IsNotRunning(exception);
    }
}