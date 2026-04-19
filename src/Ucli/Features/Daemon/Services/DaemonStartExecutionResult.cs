using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Services;

/// <summary> Represents the result of one daemon-start command workflow execution. </summary>
/// <param name="Output"> The daemon-start execution output on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured execution error on failure; otherwise <see langword="null" />. </param>
internal sealed record DaemonStartExecutionResult (
    DaemonStartExecutionOutput? Output,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether daemon-start workflow execution succeeded. </summary>
    public bool IsSuccess => Output is not null && Error is null;

    /// <summary> Creates a successful daemon-start execution result. </summary>
    /// <param name="output"> The normalized daemon-start execution output. </param>
    /// <returns> The successful daemon-start execution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="output" /> is <see langword="null" />. </exception>
    public static DaemonStartExecutionResult Success (DaemonStartExecutionOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new DaemonStartExecutionResult(output, null);
    }

    /// <summary> Creates a failed daemon-start execution result. </summary>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The failed daemon-start execution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonStartExecutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonStartExecutionResult(null, error);
    }
}