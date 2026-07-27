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
    /// <param name="instancePath"> The RFC 6901 path of the related value, or <see langword="null" /> when not applicable. </param>
    /// <param name="startupFailure"> The structured startup failure detail when this failure occurred before a Unity process accepted the request. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="outcome" /> is incompatible with <paramref name="kind" /> or <paramref name="message" /> has no content. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="code" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="kind" /> is undefined. </exception>
    public ApplicationFailure (
        ApplicationFailureKind kind,
        ApplicationOutcome outcome,
        UcliCode code,
        string message,
        string? instancePath,
        StartupFailureDetail? startupFailure)
    {
        if (!IsDefinedKind(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Failure kind is not defined.");
        }

        ValidateOutcome(kind, outcome);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Kind = kind;
        Outcome = outcome;
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Message = message;
        InstancePath = instancePath;
        StartupFailure = startupFailure;
    }

    /// <summary> Gets the application failure classification. </summary>
    public ApplicationFailureKind Kind { get; }

    /// <summary> Gets the application outcome used for exit-code projection. </summary>
    public ApplicationOutcome Outcome { get; }

    /// <summary> Gets the machine-readable failure code. </summary>
    public UcliCode Code { get; }

    /// <summary> Gets the user-facing failure message. </summary>
    public string Message { get; }

    /// <summary> Gets the RFC 6901 path of the related value, or <see langword="null" /> when not applicable. </summary>
    public string? InstancePath { get; }

    /// <summary> Gets the structured startup failure detail when this failure occurred before a Unity process accepted the request. </summary>
    public StartupFailureDetail? StartupFailure { get; }

    /// <summary> Creates a classified failure using the default outcome and fallback error code for the specified kind. </summary>
    public static ApplicationFailure Create (
        ApplicationFailureKind kind,
        string message,
        UcliCode? code = null,
        string? instancePath = null,
        ApplicationOutcome? outcome = null,
        StartupFailureDetail? startupFailure = null)
    {
        var resolvedCode = ResolveCode(kind, code);
        return new ApplicationFailure(
            kind,
            outcome ?? ResolveOutcome(kind),
            resolvedCode,
            message,
            instancePath,
            startupFailure);
    }

    /// <summary> Creates an invalid-input failure. </summary>
    public static ApplicationFailure InvalidInput (
        string message,
        UcliCode? code = null,
        string? instancePath = null,
        StartupFailureDetail? startupFailure = null)
    {
        return Create(ApplicationFailureKind.InvalidInput, message, code, instancePath, startupFailure: startupFailure);
    }

    /// <summary> Creates a configuration failure. </summary>
    public static ApplicationFailure ConfigurationError (
        string message,
        UcliCode? code = null,
        string? instancePath = null)
    {
        return Create(ApplicationFailureKind.ConfigurationError, message, code, instancePath);
    }

    /// <summary> Creates an environment failure. </summary>
    public static ApplicationFailure EnvironmentError (
        string message,
        UcliCode? code = null,
        string? instancePath = null)
    {
        return Create(ApplicationFailureKind.EnvironmentError, message, code, instancePath);
    }

    /// <summary> Creates a Unity IPC failure. </summary>
    public static ApplicationFailure UnityIpcFailure (
        string message,
        UcliCode? code = null,
        string? instancePath = null,
        StartupFailureDetail? startupFailure = null)
    {
        return Create(ApplicationFailureKind.UnityIpcFailure, message, code, instancePath, startupFailure: startupFailure);
    }

    /// <summary> Creates an external-process failure. </summary>
    public static ApplicationFailure ExternalProcessFailure (
        string message,
        UcliCode? code = null,
        string? instancePath = null,
        ApplicationOutcome? outcome = null,
        StartupFailureDetail? startupFailure = null)
    {
        return Create(ApplicationFailureKind.ExternalProcessFailure, message, code, instancePath, outcome, startupFailure);
    }

    /// <summary> Creates a contract-violation failure. </summary>
    public static ApplicationFailure ContractViolation (
        string message,
        UcliCode? code = null,
        string? instancePath = null,
        StartupFailureDetail? startupFailure = null)
    {
        return Create(ApplicationFailureKind.ContractViolation, message, code, instancePath, startupFailure: startupFailure);
    }

    /// <summary> Creates a timeout failure. </summary>
    public static ApplicationFailure Timeout (
        string message,
        UcliCode? code = null,
        string? instancePath = null,
        StartupFailureDetail? startupFailure = null)
    {
        return Create(ApplicationFailureKind.Timeout, message, code, instancePath, startupFailure: startupFailure);
    }

    /// <summary> Creates a canceled failure. </summary>
    public static ApplicationFailure Canceled (
        string message,
        UcliCode? code = null,
        string? instancePath = null)
    {
        return Create(ApplicationFailureKind.Canceled, message, code, instancePath);
    }

    /// <summary> Creates an internal failure. </summary>
    public static ApplicationFailure InternalError (
        string message,
        UcliCode? code = null,
        string? instancePath = null,
        StartupFailureDetail? startupFailure = null)
    {
        return Create(ApplicationFailureKind.InternalError, message, code, instancePath, startupFailure: startupFailure);
    }

    /// <summary> Creates a failure from a structured execution error. </summary>
    public static ApplicationFailure FromExecutionError (
        ExecutionError error,
        UcliCode? code = null,
        string? instancePath = null,
        StartupFailureDetail? startupFailure = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        var resolvedCode = code ?? ExecutionErrorCodeMapper.ToCode(error);

        return error.Kind switch
        {
            ExecutionErrorKind.InvalidArgument => InvalidInput(error.Message, resolvedCode, instancePath, startupFailure),
            ExecutionErrorKind.Timeout when InvalidArgumentErrorCodeSet.Contains(resolvedCode) => InvalidInput(error.Message, resolvedCode, instancePath, startupFailure),
            ExecutionErrorKind.Timeout => Timeout(error.Message, resolvedCode, instancePath, startupFailure),
            ExecutionErrorKind.InternalError when InvalidArgumentErrorCodeSet.Contains(resolvedCode) => InvalidInput(error.Message, resolvedCode, instancePath, startupFailure),
            ExecutionErrorKind.InternalError => InternalError(error.Message, resolvedCode, instancePath, startupFailure),
            _ => throw new ArgumentOutOfRangeException(nameof(error), error.Kind, "Execution error kind is not defined."),
        };
    }

    /// <summary> Creates a failure by classifying an existing machine-readable code. </summary>
    public static ApplicationFailure FromCode (
        UcliCode? code,
        string message,
        string? instancePath = null,
        StartupFailureDetail? startupFailure = null)
    {
        var resolvedCode = code ?? UcliCoreErrorCodes.InternalError;

        if (resolvedCode == ExecutionErrorCodes.IpcTimeout)
        {
            return Timeout(message, resolvedCode, instancePath, startupFailure);
        }

        if (resolvedCode == ExecutionErrorCodes.Canceled)
        {
            return Canceled(message, resolvedCode, instancePath);
        }

        if (InvalidArgumentErrorCodeSet.Contains(resolvedCode))
        {
            return InvalidInput(message, resolvedCode, instancePath, startupFailure);
        }

        if (resolvedCode == UcliCoreErrorCodes.InternalError)
        {
            return InternalError(message, resolvedCode, instancePath, startupFailure);
        }

        return ContractViolation(message, resolvedCode, instancePath, startupFailure);
    }

    private static UcliCode ResolveCode (
        ApplicationFailureKind kind,
        UcliCode? code)
    {
        if (code is not null)
        {
            return code;
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

    private static ApplicationOutcome ResolveOutcome (ApplicationFailureKind kind)
    {
        return kind switch
        {
            ApplicationFailureKind.InvalidInput => ApplicationOutcome.InvalidArgument,
            ApplicationFailureKind.ConfigurationError => ApplicationOutcome.InvalidArgument,
            _ => ApplicationOutcome.ToolError,
        };
    }

    private static void ValidateOutcome (
        ApplicationFailureKind kind,
        ApplicationOutcome outcome)
    {
        var isValid = kind switch
        {
            ApplicationFailureKind.InvalidInput => outcome == ApplicationOutcome.InvalidArgument,
            ApplicationFailureKind.ConfigurationError => outcome == ApplicationOutcome.InvalidArgument,
            ApplicationFailureKind.ExternalProcessFailure => outcome is ApplicationOutcome.ToolError or ApplicationOutcome.InfrastructureError,
            _ => outcome == ApplicationOutcome.ToolError,
        };

        if (!isValid)
        {
            throw new ArgumentException("Failure outcome must match the failure kind.", nameof(outcome));
        }
    }
}
