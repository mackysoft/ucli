using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Services;

/// <summary> Represents the result of one daemon-stop command workflow execution. </summary>
/// <param name="Output"> The daemon-stop execution output on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured execution error on failure; otherwise <see langword="null" />. </param>
internal sealed record DaemonStopExecutionResult (
    DaemonStopExecutionOutput? Output,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether daemon-stop workflow execution succeeded. </summary>
    public bool IsSuccess => Output is not null && Error is null;

    /// <summary> Creates a successful daemon-stop execution result. </summary>
    /// <param name="output"> The normalized daemon-stop execution output. </param>
    /// <returns> The successful daemon-stop execution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="output" /> is <see langword="null" />. </exception>
    public static DaemonStopExecutionResult Success (DaemonStopExecutionOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new DaemonStopExecutionResult(output, null);
    }

    /// <summary> Creates a failed daemon-stop execution result. </summary>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The failed daemon-stop execution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonStopExecutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonStopExecutionResult(null, error);
    }
}