using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Daemon.Command;

/// <summary> Represents the result of one daemon-status command workflow execution. </summary>
/// <param name="Output"> The daemon-status execution output on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured execution error on failure; otherwise <see langword="null" />. </param>
internal sealed record DaemonStatusExecutionResult (
    DaemonStatusExecutionOutput? Output,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether daemon-status workflow execution succeeded. </summary>
    public bool IsSuccess => Output is not null && Error is null;

    /// <summary> Creates a successful daemon-status execution result. </summary>
    /// <param name="output"> The normalized daemon-status execution output. </param>
    /// <returns> The successful daemon-status execution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="output" /> is <see langword="null" />. </exception>
    public static DaemonStatusExecutionResult Success (DaemonStatusExecutionOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new DaemonStatusExecutionResult(output, null);
    }

    /// <summary> Creates a failed daemon-status execution result. </summary>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The failed daemon-status execution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonStatusExecutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonStatusExecutionResult(null, error);
    }
}