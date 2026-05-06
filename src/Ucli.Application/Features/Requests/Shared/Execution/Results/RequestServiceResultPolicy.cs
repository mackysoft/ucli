using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;

/// <summary> Provides invariant checks for request service result models. </summary>
internal static class RequestServiceResultPolicy
{
    private static readonly IReadOnlyList<OperationExecutionError> EmptyErrorList = Array.AsReadOnly(Array.Empty<OperationExecutionError>());

    /// <summary> Gets the canonical empty error collection for successful results. </summary>
    public static IReadOnlyList<OperationExecutionError> EmptyErrors => EmptyErrorList;

    /// <summary> Validates one success message. </summary>
    public static void ValidateSuccessMessage (string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
    }

    /// <summary> Validates one failure message. </summary>
    public static void ValidateFailureMessage (string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
    }

    /// <summary> Resolves one failure message from errors and a fallback message. </summary>
    public static string ResolveFailureMessage (
        IReadOnlyList<OperationExecutionError> errors,
        string fallbackMessage)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ValidateFailureMessage(fallbackMessage);

        for (var i = 0; i < errors.Count; i++)
        {
            var error = errors[i];
            if (error != null && !string.IsNullOrWhiteSpace(error.Message))
            {
                return error.Message;
            }
        }

        return fallbackMessage;
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
        IReadOnlyList<OperationExecutionError> errors,
        ApplicationOutcome outcome)
    {
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

        return Array.AsReadOnly(snapshot);
    }

    /// <summary> Creates one operation execution error from a structured execution error. </summary>
    public static OperationExecutionError FromExecutionError (
        ExecutionError error,
        string? errorCode = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentException.ThrowIfNullOrWhiteSpace(error.Message, nameof(error));

        return new OperationExecutionError(
            ResolveErrorCode(string.IsNullOrWhiteSpace(errorCode)
                ? ExecutionErrorCodeMapper.ToCode(error.Kind)
                : errorCode),
            error.Message,
            null);
    }

    /// <summary> Creates one operation execution error from a transport failure. </summary>
    public static OperationExecutionError FromTransportFailure (
        string? errorCode,
        string message,
        string? opId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new OperationExecutionError(
            ResolveErrorCode(errorCode),
            message,
            opId);
    }

    /// <summary> Normalizes one operation execution error from an external result boundary. </summary>
    public static OperationExecutionError NormalizeError (
        OperationExecutionError error,
        string fallbackMessage)
    {
        ArgumentNullException.ThrowIfNull(error);

        var message = string.IsNullOrWhiteSpace(error.Message)
            ? fallbackMessage
            : error.Message;
        ArgumentException.ThrowIfNullOrWhiteSpace(message, nameof(fallbackMessage));

        return new OperationExecutionError(
            ResolveErrorCode(error.Code),
            message,
            error.OpId);
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
    public static ApplicationOutcome ResolveOutcome (
        ExecutionError error,
        string? errorCode = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        return ResolveOutcome(string.IsNullOrWhiteSpace(errorCode)
            ? ExecutionErrorCodeMapper.ToCode(error.Kind)
            : errorCode);
    }

    /// <summary> Resolves the application outcome for one machine-readable error code. </summary>
    public static ApplicationOutcome ResolveOutcome (string errorCode)
    {
        return string.Equals(
                ResolveErrorCode(errorCode),
                ExecutionErrorCodeMapper.ToCode(ExecutionErrorKind.InvalidArgument),
                StringComparison.Ordinal)
            ? ApplicationOutcome.InvalidArgument
            : ApplicationOutcome.ToolError;
    }

    /// <summary> Resolves the machine-readable error code used for request failures. </summary>
    public static string ResolveErrorCode (string? errorCode)
    {
        return string.IsNullOrWhiteSpace(errorCode)
            ? ExecutionErrorCodeMapper.ToCode(ExecutionErrorKind.InternalError)
            : errorCode;
    }
}
