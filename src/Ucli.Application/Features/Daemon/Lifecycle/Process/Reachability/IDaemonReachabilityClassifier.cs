namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Reachability;

/// <summary> Classifies exceptions by daemon reachability semantics. </summary>
internal interface IDaemonReachabilityClassifier
{
    /// <summary> Determines whether one exception means daemon endpoint is not running. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when endpoint is treated as not running; otherwise <see langword="false" />. </returns>
    bool IsNotRunning (Exception exception);

    /// <summary> Determines whether one exception rejects a session token that may have rotated. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when the endpoint specifically reports an invalid session token; otherwise <see langword="false" />. </returns>
    bool IsSessionTokenInvalid (Exception exception);
}
