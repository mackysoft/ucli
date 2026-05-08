using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Shared.Execution;

/// <summary> Represents one classified application failure before CLI output projection. </summary>
internal sealed record ApplicationFailure
{
    /// <summary> Initializes a new instance of the <see cref="ApplicationFailure" /> class. </summary>
    /// <param name="kind"> The application failure classification. </param>
    /// <param name="outcome"> The application outcome used for exit-code projection. </param>
    /// <param name="code"> The machine-readable failure code. </param>
    /// <param name="message"> The user-facing failure message. </param>
    /// <param name="opId"> The operation identifier associated with the failure, or <see langword="null" /> when not applicable. </param>
    public ApplicationFailure (
        ApplicationFailureKind kind,
        ApplicationOutcome outcome,
        UcliErrorCode code,
        string message,
        string? opId = null)
    {
        if (!IsDefinedKind(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Failure kind is not defined.");
        }

        if (outcome == ApplicationOutcome.Success)
        {
            throw new ArgumentException("Failure outcome must not be success.", nameof(outcome));
        }

        if (!code.IsValid)
        {
            throw new ArgumentException("Failure code must not be empty.", nameof(code));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Kind = kind;
        Outcome = outcome;
        Code = code;
        Message = message;
        OpId = opId;
    }

    /// <summary> Gets the application failure classification. </summary>
    public ApplicationFailureKind Kind { get; }

    /// <summary> Gets the application outcome used for exit-code projection. </summary>
    public ApplicationOutcome Outcome { get; }

    /// <summary> Gets the machine-readable failure code. </summary>
    public UcliErrorCode Code { get; }

    /// <summary> Gets the user-facing failure message. </summary>
    public string Message { get; }

    /// <summary> Gets the operation identifier associated with the failure, or <see langword="null" /> when not applicable. </summary>
    public string? OpId { get; }

    /// <summary> Creates a classified failure using the default outcome and fallback error code for the specified kind. </summary>
    public static ApplicationFailure Create (
        ApplicationFailureKind kind,
        string message,
        UcliErrorCode? code = null,
        string? opId = null,
        ApplicationOutcome? outcome = null)
    {
        var resolvedCode = ResolveCode(kind, code);
        return new ApplicationFailure(
            kind,
            outcome ?? ResolveOutcome(kind, resolvedCode),
            resolvedCode,
            message,
            opId);
    }

    /// <summary> Creates an invalid-input failure. </summary>
    public static ApplicationFailure InvalidInput (
        string message,
        UcliErrorCode? code = null,
        string? opId = null)
    {
        return Create(ApplicationFailureKind.InvalidInput, message, code, opId);
    }

    /// <summary> Creates a configuration failure. </summary>
    public static ApplicationFailure ConfigurationError (
        string message,
        UcliErrorCode? code = null,
        string? opId = null)
    {
        return Create(ApplicationFailureKind.ConfigurationError, message, code, opId);
    }

    /// <summary> Creates an environment failure. </summary>
    public static ApplicationFailure EnvironmentError (
        string message,
        UcliErrorCode? code = null,
        string? opId = null)
    {
        return Create(ApplicationFailureKind.EnvironmentError, message, code, opId);
    }

    /// <summary> Creates a Unity IPC failure. </summary>
    public static ApplicationFailure UnityIpcFailure (
        string message,
        UcliErrorCode? code = null,
        string? opId = null,
        ApplicationOutcome? outcome = null)
    {
        return Create(ApplicationFailureKind.UnityIpcFailure, message, code, opId, outcome);
    }

    /// <summary> Creates an external-process failure. </summary>
    public static ApplicationFailure ExternalProcessFailure (
        string message,
        UcliErrorCode? code = null,
        string? opId = null,
        ApplicationOutcome? outcome = null)
    {
        return Create(ApplicationFailureKind.ExternalProcessFailure, message, code, opId, outcome);
    }

    /// <summary> Creates a contract-violation failure. </summary>
    public static ApplicationFailure ContractViolation (
        string message,
        UcliErrorCode? code = null,
        string? opId = null)
    {
        return Create(ApplicationFailureKind.ContractViolation, message, code, opId);
    }

    /// <summary> Creates a timeout failure. </summary>
    public static ApplicationFailure Timeout (
        string message,
        UcliErrorCode? code = null,
        string? opId = null)
    {
        return Create(ApplicationFailureKind.Timeout, message, code, opId);
    }

    /// <summary> Creates a canceled failure. </summary>
    public static ApplicationFailure Canceled (
        string message,
        UcliErrorCode? code = null,
        string? opId = null)
    {
        return Create(ApplicationFailureKind.Canceled, message, code, opId);
    }

    /// <summary> Creates an internal failure. </summary>
    public static ApplicationFailure InternalError (
        string message,
        UcliErrorCode? code = null,
        string? opId = null)
    {
        return Create(ApplicationFailureKind.InternalError, message, code, opId);
    }

    /// <summary> Creates a failure from a structured execution error. </summary>
    public static ApplicationFailure FromExecutionError (
        ExecutionError error,
        UcliErrorCode? code = null,
        string? opId = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        var resolvedCode = code.HasValue && code.Value.IsValid
            ? code.Value
            : ExecutionErrorCodeMapper.ToCode(error);

        if (ApplicationFailureOutcomeResolver.IsInvalidArgumentCode(resolvedCode))
        {
            return InvalidInput(error.Message, resolvedCode, opId);
        }

        return error.Kind switch
        {
            ExecutionErrorKind.InvalidArgument => InvalidInput(error.Message, resolvedCode, opId),
            ExecutionErrorKind.Timeout => Timeout(error.Message, resolvedCode, opId),
            ExecutionErrorKind.InternalError => InternalError(error.Message, resolvedCode, opId),
            _ => InternalError(error.Message, resolvedCode, opId),
        };
    }

    /// <summary> Creates a failure by classifying an existing machine-readable code. </summary>
    public static ApplicationFailure FromCode (
        UcliErrorCode? code,
        string message,
        string? opId = null)
    {
        var resolvedCode = code.HasValue && code.Value.IsValid
            ? code.Value
            : UcliCoreErrorCodes.InternalError;

        if (resolvedCode == ExecutionErrorCodes.IpcTimeout)
        {
            return Timeout(message, resolvedCode, opId);
        }

        if (resolvedCode == ExecutionErrorCodes.Canceled)
        {
            return Canceled(message, resolvedCode, opId);
        }

        if (ApplicationFailureOutcomeResolver.IsInvalidArgumentCode(resolvedCode))
        {
            return InvalidInput(message, resolvedCode, opId);
        }

        if (resolvedCode == UcliCoreErrorCodes.InternalError)
        {
            return InternalError(message, resolvedCode, opId);
        }

        return ContractViolation(message, resolvedCode, opId);
    }

    private static UcliErrorCode ResolveCode (
        ApplicationFailureKind kind,
        UcliErrorCode? code)
    {
        if (code.HasValue && code.Value.IsValid)
        {
            return code.Value;
        }

        return kind switch
        {
            ApplicationFailureKind.InvalidInput => UcliCoreErrorCodes.InvalidArgument,
            ApplicationFailureKind.ConfigurationError => UcliCoreErrorCodes.InvalidArgument,
            ApplicationFailureKind.Timeout => ExecutionErrorCodes.IpcTimeout,
            ApplicationFailureKind.Canceled => ExecutionErrorCodes.Canceled,
            ApplicationFailureKind.InternalError => UcliCoreErrorCodes.InternalError,
            _ => UcliCoreErrorCodes.InternalError,
        };
    }

    private static bool IsDefinedKind (ApplicationFailureKind kind)
    {
        return kind is ApplicationFailureKind.InvalidInput
            or ApplicationFailureKind.ConfigurationError
            or ApplicationFailureKind.EnvironmentError
            or ApplicationFailureKind.UnityIpcFailure
            or ApplicationFailureKind.ExternalProcessFailure
            or ApplicationFailureKind.ContractViolation
            or ApplicationFailureKind.Timeout
            or ApplicationFailureKind.Canceled
            or ApplicationFailureKind.InternalError;
    }

    private static ApplicationOutcome ResolveOutcome (
        ApplicationFailureKind kind,
        UcliErrorCode code)
    {
        return kind switch
        {
            ApplicationFailureKind.InvalidInput => ApplicationOutcome.InvalidArgument,
            ApplicationFailureKind.ConfigurationError => ApplicationOutcome.InvalidArgument,
            _ => ApplicationFailureOutcomeResolver.Resolve(code),
        };
    }
}
