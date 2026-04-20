namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process;

/// <summary> Classifies exceptions by daemon reachability semantics. </summary>
internal interface IDaemonReachabilityClassifier
{
    /// <summary> Determines whether one exception means daemon endpoint is not running. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when endpoint is treated as not running; otherwise <see langword="false" />. </returns>
    bool IsNotRunning (Exception exception);
}