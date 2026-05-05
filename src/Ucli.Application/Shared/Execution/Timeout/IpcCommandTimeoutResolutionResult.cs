using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Shared.Execution.Timeout;

/// <summary> Represents one IPC-command-timeout resolution result. </summary>
/// <param name="Timeout"> The resolved timeout on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error on failure; otherwise <see langword="null" />. </param>
internal sealed record IpcCommandTimeoutResolutionResult (
    TimeSpan? Timeout,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether timeout resolution succeeded. </summary>
    public bool IsSuccess => Timeout.HasValue && Error is null;

    /// <summary> Creates a successful timeout-resolution result. </summary>
    /// <param name="timeout"> The resolved timeout. </param>
    /// <returns> The successful result. </returns>
    public static IpcCommandTimeoutResolutionResult Success (TimeSpan timeout)
    {
        return new IpcCommandTimeoutResolutionResult(timeout, null);
    }

    /// <summary> Creates a failed timeout-resolution result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static IpcCommandTimeoutResolutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new IpcCommandTimeoutResolutionResult(null, error);
    }
}
