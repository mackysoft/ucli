using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Daemon.Command;

/// <summary> Represents the result of one daemon-cleanup command workflow execution. </summary>
/// <param name="Output"> The daemon-cleanup execution output on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured execution error on failure; otherwise <see langword="null" />. </param>
internal sealed record DaemonCleanupExecutionResult (
    DaemonCleanupExecutionOutput? Output,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether daemon-cleanup workflow execution succeeded. </summary>
    public bool IsSuccess => Output is not null && Error is null;

    /// <summary> Creates a successful daemon-cleanup execution result. </summary>
    /// <param name="output"> The normalized daemon-cleanup execution output. </param>
    /// <returns> The successful daemon-cleanup execution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="output" /> is <see langword="null" />. </exception>
    public static DaemonCleanupExecutionResult Success (DaemonCleanupExecutionOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new DaemonCleanupExecutionResult(output, null);
    }

    /// <summary> Creates a failed daemon-cleanup execution result. </summary>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The failed daemon-cleanup execution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonCleanupExecutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonCleanupExecutionResult(null, error);
    }
}