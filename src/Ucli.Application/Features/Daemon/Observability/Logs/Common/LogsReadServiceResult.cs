using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;

/// <summary> Represents one <c>logs * read</c> service execution result. </summary>
/// <param name="Error"> The structured execution error when command failed; otherwise <see langword="null" />. </param>
internal sealed record LogsReadServiceResult (
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether execution succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful service result. </summary>
    /// <returns> The successful service result. </returns>
    public static LogsReadServiceResult Success ()
    {
        return new LogsReadServiceResult(Error: null);
    }

    /// <summary> Creates a failed service result. </summary>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The failed service result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static LogsReadServiceResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new LogsReadServiceResult(error);
    }
}
