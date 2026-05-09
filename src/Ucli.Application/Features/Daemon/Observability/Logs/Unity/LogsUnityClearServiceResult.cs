using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;

/// <summary> Represents a <c>logs unity clear</c> execution result. </summary>
/// <param name="Output"> The successful command output. </param>
/// <param name="Error"> The structured error when execution fails. </param>
internal sealed record LogsUnityClearServiceResult (
    LogsUnityClearServiceOutput? Output,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether execution succeeded. </summary>
    public bool IsSuccess => Output is not null && Error is null;

    /// <summary> Creates a successful result. </summary>
    /// <param name="output"> The command output. </param>
    /// <returns> The successful result. </returns>
    public static LogsUnityClearServiceResult Success (LogsUnityClearServiceOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new LogsUnityClearServiceResult(output, null);
    }

    /// <summary> Creates a failed result. </summary>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The failed result. </returns>
    public static LogsUnityClearServiceResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new LogsUnityClearServiceResult(null, error);
    }
}
