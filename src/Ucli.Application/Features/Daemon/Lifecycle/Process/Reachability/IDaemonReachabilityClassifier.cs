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

    /// <summary> Determines whether transport failed before any request bytes were written. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when the same logical request can be retried safely. </returns>
    bool IsRetryableBeforeRequestWrite (Exception exception);

    /// <summary> Determines whether one exception means a daemon request exceeded its processing deadline. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when the request timed out; otherwise <see langword="false" />. </returns>
    bool IsRequestTimeout (Exception exception);

    /// <summary> Determines whether a read-only request may be replayed after response delivery was interrupted. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when request transmission may have completed but replay is safe. </returns>
    bool IsRecoverableResponseInterruption (Exception exception);
}
