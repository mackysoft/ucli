using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;

/// <summary> Represents the result of daemon stop operation. </summary>
internal sealed record DaemonStopResult
{
    private DaemonStopResult (
        DaemonStopStatus? status,
        ExecutionError? error)
    {
        Status = status;
        Error = error;
    }

    /// <summary> Gets the daemon stop outcome on success; otherwise <see langword="null" />. </summary>
    public DaemonStopStatus? Status { get; }

    /// <summary> Gets the structured error when stop fails; otherwise <see langword="null" />. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Gets a value indicating whether daemon stop operation succeeded. </summary>
    [MemberNotNullWhen(true, nameof(Status))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Status.HasValue;

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
        return new DaemonStopResult(status: null, error);
    }
}
