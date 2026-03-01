using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Daemon;

/// <summary> Represents the result of daemon stop operation. </summary>
/// <param name="Status"> The daemon stop outcome. </param>
/// <param name="Error"> The structured error when stop fails; otherwise <see langword="null" />. </param>
internal sealed record DaemonStopResult (
    DaemonStopStatus Status,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether daemon stop operation succeeded. </summary>
    public bool IsSuccess => (Status == DaemonStopStatus.Stopped || Status == DaemonStopStatus.NotRunning) && Error is null;

    /// <summary> Creates a successful stopped result. </summary>
    /// <returns> The successful stopped result. </returns>
    public static DaemonStopResult Stopped ()
    {
        return new DaemonStopResult(DaemonStopStatus.Stopped, null);
    }

    /// <summary> Creates a not-running result. </summary>
    /// <returns> The not-running result. </returns>
    public static DaemonStopResult NotRunning ()
    {
        return new DaemonStopResult(DaemonStopStatus.NotRunning, null);
    }

    /// <summary> Creates a failed stop result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed stop result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonStopResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonStopResult(DaemonStopStatus.Failed, error);
    }
}