namespace MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

/// <summary> Represents one ping-response contract failure returned from a reachable daemon endpoint. </summary>
internal sealed class DaemonPingResponseException : Exception
{
    /// <summary> Gets the daemon error code when one was provided by response payload; otherwise <see langword="null" />. </summary>
    public UcliErrorCode? ErrorCode { get; }

    /// <summary> Initializes a new instance of the <see cref="DaemonPingResponseException" /> class. </summary>
    /// <param name="message"> The exception message that explains why ping response is treated as failure. </param>
    /// <param name="errorCode"> The daemon error code when one exists in response payload. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="message" /> is <see langword="null" />. </exception>
    public DaemonPingResponseException (
        string message,
        UcliErrorCode? errorCode = null)
        : base(message ?? throw new ArgumentNullException(nameof(message)))
    {
        ErrorCode = errorCode;
    }
}
