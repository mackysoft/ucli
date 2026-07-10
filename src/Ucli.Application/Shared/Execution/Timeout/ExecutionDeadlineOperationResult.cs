using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Shared.Execution.Timeout;

/// <summary> Represents a value produced within a deadline or a structured timeout error. </summary>
/// <typeparam name="T"> The operation result type. </typeparam>
/// <param name="Value"> The operation value on success. </param>
/// <param name="Error"> The structured timeout error on failure. </param>
internal readonly record struct ExecutionDeadlineOperationResult<T> (
    T? Value,
    ExecutionError? Error)
{
    /// <summary> Gets whether the operation completed within its deadline. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful result. </summary>
    public static ExecutionDeadlineOperationResult<T> Success (T value)
    {
        return new ExecutionDeadlineOperationResult<T>(value, null);
    }

    /// <summary> Creates a failed result. </summary>
    public static ExecutionDeadlineOperationResult<T> Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ExecutionDeadlineOperationResult<T>(default, error);
    }
}
