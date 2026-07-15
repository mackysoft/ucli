namespace MackySoft.Ucli.UnityIntegration.Ipc.Clients;

/// <summary> Defines bounded cleanup timing for a Unity oneshot process. </summary>
internal sealed record UnityOneshotCleanupPolicy
{
    /// <summary> Gets the production cleanup timing policy. </summary>
    public static UnityOneshotCleanupPolicy Default { get; } = new(
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMilliseconds(50));

    /// <summary> Initializes validated cleanup timing. </summary>
    /// <param name="timeout"> The total cleanup timeout. </param>
    /// <param name="retryDelay"> The maximum delay between cleanup observations. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when either duration is not positive. </exception>
    public UnityOneshotCleanupPolicy (
        TimeSpan timeout,
        TimeSpan retryDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(retryDelay, TimeSpan.Zero);

        Timeout = timeout;
        RetryDelay = retryDelay;
    }

    /// <summary> Gets the total cleanup timeout. </summary>
    public TimeSpan Timeout { get; }

    /// <summary> Gets the maximum delay between cleanup observations. </summary>
    public TimeSpan RetryDelay { get; }
}
