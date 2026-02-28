using System.Net.Sockets;

namespace MackySoft.Ucli.Execution;

/// <summary> Classifies daemon probe exceptions by operational meaning. </summary>
internal static class DaemonProbeExceptionClassifier
{
    /// <summary> Determines whether one canceled operation means internal probe timeout. </summary>
    /// <param name="exception"> The canceled-operation exception from ping processing. </param>
    /// <param name="commandCancellationToken"> The command-level cancellation token. </param>
    /// <param name="probeCancellationToken"> The probe timeout cancellation token. </param>
    /// <returns> <see langword="true" /> when probe timeout triggered; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="exception" /> is <see langword="null" />. </exception>
    public static bool IsProbeTimeout (
        OperationCanceledException exception,
        CancellationToken commandCancellationToken,
        CancellationToken probeCancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return !commandCancellationToken.IsCancellationRequested && probeCancellationToken.IsCancellationRequested;
    }

    /// <summary> Determines whether one exception means daemon endpoint is not reachable. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when endpoint is treated as not running; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="exception" /> is <see langword="null" />. </exception>
    public static bool IsNotRunning (Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is TimeoutException
            or IOException
            or SocketException
            or UnauthorizedAccessException;
    }
}