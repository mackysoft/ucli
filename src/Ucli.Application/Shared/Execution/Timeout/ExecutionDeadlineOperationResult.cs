using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Shared.Execution.Timeout;

/// <summary> Represents a value produced within a deadline or a structured timeout error. </summary>
/// <typeparam name="T"> The operation result type. </typeparam>
internal sealed class ExecutionDeadlineOperationResult<T>
{
    private ExecutionDeadlineOperationResult (
        bool isSuccess,
        T? value,
        ExecutionError? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    /// <summary> Gets whether the operation completed within its deadline. </summary>
    public bool IsSuccess { get; }

    /// <summary> Gets the operation value on success; otherwise <see langword="default" />. </summary>
    public T? Value { get; }

    /// <summary> Gets the structured timeout error on failure; otherwise <see langword="null" />. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Creates a successful result whose value follows the nullability contract of <typeparamref name="T" />. </summary>
    public static ExecutionDeadlineOperationResult<T> Success (T value)
    {
        return new ExecutionDeadlineOperationResult<T>(
            isSuccess: true,
            value,
            error: null);
    }

    /// <summary> Creates a failed result. </summary>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static ExecutionDeadlineOperationResult<T> Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ExecutionDeadlineOperationResult<T>(
            isSuccess: false,
            value: default,
            error);
    }
}
