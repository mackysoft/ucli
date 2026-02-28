namespace MackySoft.Ucli.Execution;

/// <summary> Represents one ping-response contract failure returned from a reachable daemon endpoint. </summary>
internal sealed class DaemonPingResponseException : Exception
{

    /// <summary> Initializes a new instance of the <see cref="DaemonPingResponseException" /> class. </summary>
    /// <param name="message"> The exception message that explains why ping response is treated as failure. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="message" /> is <see langword="null" />. </exception>
    public DaemonPingResponseException (string message)
        : base(message ?? throw new ArgumentNullException(nameof(message)))
    { }
}