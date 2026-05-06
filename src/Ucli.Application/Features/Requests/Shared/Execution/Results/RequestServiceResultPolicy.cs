using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;

/// <summary> Provides invariant checks for request service result models. </summary>
internal static class RequestServiceResultPolicy
{
    private static readonly IReadOnlyList<OperationExecutionError> EmptyErrorList = Array.Empty<OperationExecutionError>();

    /// <summary> Gets the canonical empty error collection for successful results. </summary>
    public static IReadOnlyList<OperationExecutionError> EmptyErrors => EmptyErrorList;

    /// <summary> Validates one success message. </summary>
    public static void ValidateSuccessMessage (string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
    }

    /// <summary> Validates and returns one required success output payload. </summary>
    public static TOutput RequireSuccessOutput<TOutput> (
        TOutput? output,
        string paramName)
        where TOutput : class
    {
        ArgumentNullException.ThrowIfNull(output, paramName);
        return output;
    }

    /// <summary> Validates one failure result and returns an immutable error snapshot. </summary>
    public static IReadOnlyList<OperationExecutionError> RequireFailureErrors (
        string message,
        IReadOnlyList<OperationExecutionError> errors,
        ApplicationOutcome outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(errors);
        if (outcome == ApplicationOutcome.Success)
        {
            throw new ArgumentException("Failure outcome must not be success.", nameof(outcome));
        }

        if (errors.Count == 0)
        {
            throw new ArgumentException("Failure errors must not be empty.", nameof(errors));
        }

        var snapshot = new OperationExecutionError[errors.Count];
        for (var i = 0; i < errors.Count; i++)
        {
            var error = errors[i];
            if (error == null)
            {
                throw new ArgumentException("Failure errors must not contain null entries.", nameof(errors));
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(error.Code, nameof(errors));
            ArgumentException.ThrowIfNullOrWhiteSpace(error.Message, nameof(errors));
            snapshot[i] = error;
        }

        return snapshot;
    }

    /// <summary> Creates one operation execution error from a structured execution error. </summary>
    public static OperationExecutionError FromExecutionError (
        ExecutionError error,
        string? errorCode = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentException.ThrowIfNullOrWhiteSpace(error.Message, nameof(error));

        return new OperationExecutionError(
            string.IsNullOrWhiteSpace(errorCode)
                ? ExecutionErrorCodeMapper.ToCode(error.Kind)
                : errorCode,
            error.Message,
            null);
    }

    /// <summary> Converts static validation errors into operation execution errors. </summary>
    public static IReadOnlyList<OperationExecutionError> FromValidationErrors (
        IReadOnlyList<ValidationError> validationErrors)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (validationErrors.Count == 0)
        {
            throw new ArgumentException("Validation errors must not be empty.", nameof(validationErrors));
        }

        var errors = new OperationExecutionError[validationErrors.Count];
        for (var i = 0; i < validationErrors.Count; i++)
        {
            var validationError = validationErrors[i];
            if (validationError == null)
            {
                throw new ArgumentException("Validation errors must not contain null entries.", nameof(validationErrors));
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(validationError.Code, nameof(validationErrors));
            ArgumentException.ThrowIfNullOrWhiteSpace(validationError.Message, nameof(validationErrors));
            errors[i] = new OperationExecutionError(validationError.Code, validationError.Message, validationError.OpId);
        }

        return errors;
    }

    /// <summary> Resolves the application outcome for one structured execution error. </summary>
    public static ApplicationOutcome ResolveOutcome (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Kind == ExecutionErrorKind.InvalidArgument
            ? ApplicationOutcome.InvalidArgument
            : ApplicationOutcome.ToolError;
    }
}
