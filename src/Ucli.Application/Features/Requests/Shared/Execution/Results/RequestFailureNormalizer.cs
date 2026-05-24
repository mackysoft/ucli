using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;

/// <summary> Converts request boundary failures into classified application failures. </summary>
internal static class RequestFailureNormalizer
{
    /// <summary> Resolves one failure message from failures and a fallback message. </summary>
    public static string ResolveMessage (
        IReadOnlyList<ApplicationFailure> failures,
        string fallbackMessage)
    {
        ArgumentNullException.ThrowIfNull(failures);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackMessage);

        for (var i = 0; i < failures.Count; i++)
        {
            var failure = failures[i];
            if (failure != null && !string.IsNullOrWhiteSpace(failure.Message))
            {
                return failure.Message;
            }
        }

        return fallbackMessage;
    }

    /// <summary> Creates one application failure from a transport failure. </summary>
    public static ApplicationFailure FromTransportFailure (
        UcliCode? errorCode,
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
            errorCode,
            normalizedMessage,
            opId);
    }

    /// <summary> Converts one Unity request boundary failure into an application failure. </summary>
    public static ApplicationFailure FromUnityRequestFailure (UnityRequestFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        if (failure.Code == ExecutionErrorCodes.IpcTimeout)
        {
            return ApplicationFailure.Timeout(failure.Message, failure.Code, startupFailure: failure.StartupFailure);
        }

        if (ApplicationFailureOutcomeResolver.IsInvalidArgumentCode(failure.Code))
        {
            return ApplicationFailure.InvalidInput(failure.Message, failure.Code, startupFailure: failure.StartupFailure);
        }

        return ApplicationFailure.UnityIpcFailure(failure.Message, failure.Code, startupFailure: failure.StartupFailure);
    }

    /// <summary> Normalizes one operation execution error from an external result boundary. </summary>
    public static ApplicationFailure FromOperationError (
        OperationExecutionError error,
        string fallbackMessage)
    {
        ArgumentNullException.ThrowIfNull(error);

        var message = string.IsNullOrWhiteSpace(error.Message)
            ? fallbackMessage
            : error.Message;
        ArgumentException.ThrowIfNullOrWhiteSpace(message, nameof(fallbackMessage));

        return ApplicationFailure.FromCode(
            error.Code,
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
            failures[i] = FromOperationError(errors[i], fallbackMessage);
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
}
