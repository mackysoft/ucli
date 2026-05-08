using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;

/// <summary> Provides invariant checks for request service result models. </summary>
internal static class RequestServiceResultPolicy
{
    private static readonly IReadOnlyList<ApplicationFailure> EmptyFailureList = Array.AsReadOnly(Array.Empty<ApplicationFailure>());

    /// <summary> Gets the canonical empty failure collection for successful results. </summary>
    public static IReadOnlyList<ApplicationFailure> EmptyErrors => EmptyFailureList;

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
        IReadOnlyList<ApplicationFailure> errors,
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

    /// <summary> Validates one failure result and returns an immutable failure snapshot. </summary>
    public static IReadOnlyList<ApplicationFailure> RequireFailureErrors (IReadOnlyList<ApplicationFailure> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
        {
            throw new ArgumentException("Failure errors must not be empty.", nameof(errors));
        }

        var snapshot = new ApplicationFailure[errors.Count];
        for (var i = 0; i < errors.Count; i++)
        {
            var error = errors[i];
            if (error == null)
            {
                throw new ArgumentException("Failure errors must not contain null entries.", nameof(errors));
            }

            if (!error.Code.IsValid)
            {
                throw new ArgumentException("Failure error code must not be empty.", nameof(errors));
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(error.Message, nameof(errors));
            if (error.Outcome == ApplicationOutcome.Success)
            {
                throw new ArgumentException("Failure outcome must not be success.", nameof(errors));
            }

            snapshot[i] = error;
        }

        return Array.AsReadOnly(snapshot);
    }

    /// <summary> Creates one application failure from a structured execution error. </summary>
    public static ApplicationFailure FromExecutionError (
        ExecutionError error,
        UcliErrorCode? errorCode = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentException.ThrowIfNullOrWhiteSpace(error.Message, nameof(error));

        return ApplicationFailure.FromExecutionError(error, errorCode);
    }

    /// <summary> Creates one application failure from a transport failure. </summary>
    public static ApplicationFailure FromTransportFailure (
        UcliErrorCode? errorCode,
        string? message,
        string? opId = null,
        string? fallbackMessage = null)
    {
        var normalizedMessage = string.IsNullOrWhiteSpace(message)
            ? fallbackMessage
            : message;
        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            normalizedMessage = "Request execution failed.";
        }

        return ApplicationFailure.FromCode(
            ResolveErrorCode(errorCode),
            normalizedMessage,
            opId);
    }

    /// <summary> Converts one Unity request boundary failure into an application failure. </summary>
    public static ApplicationFailure FromUnityRequestFailure (UnityRequestFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        if (failure.Code == ExecutionErrorCodes.IpcTimeout)
        {
            return ApplicationFailure.Timeout(failure.Message, failure.Code);
        }

        if (ApplicationFailureOutcomeResolver.IsInvalidArgumentCode(failure.Code))
        {
            return ApplicationFailure.InvalidInput(failure.Message, failure.Code);
        }

        return ApplicationFailure.UnityIpcFailure(failure.Message, failure.Code);
    }

    /// <summary> Normalizes one operation execution error from an external result boundary. </summary>
    public static ApplicationFailure NormalizeError (
        OperationExecutionError error,
        string fallbackMessage)
    {
        ArgumentNullException.ThrowIfNull(error);

        var message = string.IsNullOrWhiteSpace(error.Message)
            ? fallbackMessage
            : error.Message;
        ArgumentException.ThrowIfNullOrWhiteSpace(message, nameof(fallbackMessage));

        return ApplicationFailure.FromCode(
            ResolveErrorCode(error.Code),
            message,
            error.OpId);
    }

    /// <summary> Converts raw operation execution errors into application failures. </summary>
    public static IReadOnlyList<ApplicationFailure> FromOperationErrors (
        IReadOnlyList<OperationExecutionError> errors,
        string fallbackMessage)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
        {
            throw new ArgumentException("Operation errors must not be empty.", nameof(errors));
        }

        var failures = new ApplicationFailure[errors.Count];
        for (var i = 0; i < errors.Count; i++)
        {
            failures[i] = NormalizeError(errors[i], fallbackMessage);
        }

        return failures;
    }

    /// <summary> Converts static validation errors into application failures. </summary>
    public static IReadOnlyList<ApplicationFailure> FromValidationErrors (
        IReadOnlyList<ValidationError> validationErrors)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (validationErrors.Count == 0)
        {
            throw new ArgumentException("Validation errors must not be empty.", nameof(validationErrors));
        }

        var errors = new ApplicationFailure[validationErrors.Count];
        for (var i = 0; i < validationErrors.Count; i++)
        {
            var validationError = validationErrors[i];
            if (validationError == null)
            {
                throw new ArgumentException("Validation errors must not contain null entries.", nameof(validationErrors));
            }

            if (!validationError.Code.IsValid)
            {
                throw new ArgumentException("Validation error code must not be empty.", nameof(validationErrors));
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(validationError.Message, nameof(validationErrors));
            errors[i] = ApplicationFailure.InvalidInput(validationError.Message, validationError.Code, validationError.OpId);
        }

        return errors;
    }

    /// <summary> Resolves the application outcome for one application failure collection. </summary>
    public static ApplicationOutcome ResolveFailureOutcome (IReadOnlyList<ApplicationFailure> errors)
    {
        return ApplicationFailureOutcomeResolver.Resolve(errors);
    }

    /// <summary> Resolves the machine-readable error code used for request failures. </summary>
    public static UcliErrorCode ResolveErrorCode (UcliErrorCode? errorCode)
    {
        return errorCode.HasValue && errorCode.Value.IsValid
            ? errorCode.Value
            : ExecutionErrorCodeMapper.ToCode(ExecutionErrorKind.InternalError);
    }

    /// <summary> Resolves the machine-readable error code used for request failures. </summary>
    public static UcliErrorCode ResolveErrorCode (UcliErrorCode errorCode)
    {
        return errorCode.IsValid
            ? errorCode
            : ExecutionErrorCodeMapper.ToCode(ExecutionErrorKind.InternalError);
    }
}
