namespace MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

/// <summary> Represents unavailable local daemon session metadata before an IPC endpoint is contacted. </summary>
internal sealed class DaemonSessionNotAvailableException : Exception
{
    /// <summary> Initializes a new instance of the <see cref="DaemonSessionNotAvailableException" /> class. </summary>
    /// <param name="message"> The message that explains why local daemon session metadata is unavailable. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="message" /> is <see langword="null" />. </exception>
    public DaemonSessionNotAvailableException (string message)
        : base(message ?? throw new ArgumentNullException(nameof(message)))
    { }
}
