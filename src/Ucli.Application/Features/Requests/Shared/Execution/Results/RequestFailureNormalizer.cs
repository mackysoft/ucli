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

        return failures.Count == 0
            ? fallbackMessage
            : failures[0].Message;
    }

    /// <summary> Creates one application failure from a transport failure. </summary>
    public static ApplicationFailure FromTransportFailure (
        UcliCode? errorCode,
        string? message,
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
            normalizedMessage);
    }

    /// <summary> Converts one Unity request boundary failure into an application failure. </summary>
    public static ApplicationFailure FromUnityRequestFailure (UnityRequestFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        if (failure.Code == ExecutionErrorCodes.IpcTimeout)
        {
            return ApplicationFailure.Timeout(failure.Message, failure.Code, startupFailure: failure.StartupFailure);
        }

        if (InvalidArgumentErrorCodeSet.Contains(failure.Code))
        {
            return ApplicationFailure.InvalidInput(failure.Message, failure.Code, startupFailure: failure.StartupFailure);
        }

        return ApplicationFailure.UnityIpcFailure(failure.Message, failure.Code, startupFailure: failure.StartupFailure);
    }

    /// <summary> Converts one validated operation execution error into an application failure. </summary>
    public static ApplicationFailure FromOperationError (OperationExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return ApplicationFailure.FromCode(
            error.Code,
            error.Message,
            error.OpId);
    }

    /// <summary> Converts raw operation execution errors into application failures. </summary>
    public static IReadOnlyList<ApplicationFailure> FromOperationErrors (
        IReadOnlyList<OperationExecutionError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
        {
            throw new ArgumentException("Operation errors must not be empty.", nameof(errors));
        }

        var failures = new ApplicationFailure[errors.Count];
        for (var i = 0; i < errors.Count; i++)
        {
            failures[i] = FromOperationError(errors[i]);
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

            errors[i] = ApplicationFailure.InvalidInput(validationError.Message, validationError.Code, validationError.OpId);
        }

        return errors;
    }
}
