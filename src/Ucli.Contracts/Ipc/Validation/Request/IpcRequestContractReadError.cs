namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Represents one request-contract read error. </summary>
/// <param name="Kind"> The machine-readable error kind. </param>
/// <param name="StepIndex"> The step index when the error is step-scoped; otherwise <c>-1</c>. </param>
/// <param name="UnknownPropertyName"> The unknown property name when the error is unknown-property related. </param>
/// <param name="StepId"> The step identifier context when available. </param>
/// <param name="DuplicatedStepId"> The duplicated step identifier for duplicate-id errors. </param>
/// <param name="DiagnosticMessage"> The nested detailed diagnostic message for structural contract violations. </param>
/// <param name="JsonStringReadError"> The nested JSON string read error for string-contract violations. </param>
/// <param name="StepPropertyReadErrorKind"> The nested object/array property read error kind for step contract violations. </param>
internal readonly record struct IpcRequestContractReadError (
    IpcRequestContractReadErrorKind Kind,
    int StepIndex,
    string? UnknownPropertyName,
    string? StepId,
    string? DuplicatedStepId,
    string? DiagnosticMessage,
    JsonStringReadError JsonStringReadError,
    StepPropertyReadErrorKind StepPropertyReadErrorKind)
{
    /// <summary> Gets an empty error value that indicates success. </summary>
    public static IpcRequestContractReadError None => new(
        Kind: IpcRequestContractReadErrorKind.None,
        StepIndex: -1,
        UnknownPropertyName: null,
        StepId: null,
        DuplicatedStepId: null,
        DiagnosticMessage: null,
        JsonStringReadError: JsonStringReadError.None,
        StepPropertyReadErrorKind: StepPropertyReadErrorKind.None);

    public static IpcRequestContractReadError RequestMustBeObject ()
    {
        return Create(IpcRequestContractReadErrorKind.RequestMustBeObject);
    }

    public static IpcRequestContractReadError UnknownRequestProperty (string unknownPropertyName)
    {
        return Create(
            IpcRequestContractReadErrorKind.UnknownRequestProperty,
            unknownPropertyName: unknownPropertyName);
    }

    public static IpcRequestContractReadError ProtocolVersionMissing ()
    {
        return Create(IpcRequestContractReadErrorKind.ProtocolVersionMissing);
    }

    public static IpcRequestContractReadError ProtocolVersionTypeMismatch ()
    {
        return Create(IpcRequestContractReadErrorKind.ProtocolVersionTypeMismatch);
    }

    public static IpcRequestContractReadError RequestIdContractViolation (JsonStringReadError jsonStringReadError)
    {
        return Create(
            IpcRequestContractReadErrorKind.RequestIdContractViolation,
            jsonStringReadError: jsonStringReadError);
    }

    public static IpcRequestContractReadError RequestIdFormatMismatch ()
    {
        return Create(IpcRequestContractReadErrorKind.RequestIdFormatMismatch);
    }

    public static IpcRequestContractReadError StepsMissing ()
    {
        return Create(IpcRequestContractReadErrorKind.StepsMissing);
    }

    public static IpcRequestContractReadError StepsTypeMismatch ()
    {
        return Create(IpcRequestContractReadErrorKind.StepsTypeMismatch);
    }

    public static IpcRequestContractReadError StepMustBeObject (int stepIndex)
    {
        return Create(IpcRequestContractReadErrorKind.StepMustBeObject, stepIndex: stepIndex);
    }

    public static IpcRequestContractReadError StepKindContractViolation (
        int stepIndex,
        JsonStringReadError jsonStringReadError)
    {
        return Create(
            IpcRequestContractReadErrorKind.StepKindContractViolation,
            stepIndex: stepIndex,
            jsonStringReadError: jsonStringReadError);
    }

    public static IpcRequestContractReadError StepKindUnsupported (
        int stepIndex,
        string stepKind)
    {
        return Create(
            IpcRequestContractReadErrorKind.StepKindUnsupported,
            stepIndex: stepIndex,
            unknownPropertyName: stepKind);
    }

    public static IpcRequestContractReadError UnknownStepProperty (
        int stepIndex,
        string unknownPropertyName)
    {
        return Create(
            IpcRequestContractReadErrorKind.UnknownStepProperty,
            stepIndex: stepIndex,
            unknownPropertyName: unknownPropertyName);
    }

    public static IpcRequestContractReadError StepIdContractViolation (
        int stepIndex,
        JsonStringReadError jsonStringReadError)
    {
        return Create(
            IpcRequestContractReadErrorKind.StepIdContractViolation,
            stepIndex: stepIndex,
            jsonStringReadError: jsonStringReadError);
    }

    public static IpcRequestContractReadError StepOpContractViolation (
        int stepIndex,
        string? stepId,
        JsonStringReadError jsonStringReadError)
    {
        return Create(
            IpcRequestContractReadErrorKind.StepOpContractViolation,
            stepIndex: stepIndex,
            stepId: stepId,
            jsonStringReadError: jsonStringReadError);
    }

    public static IpcRequestContractReadError StepArgsContractViolation (
        int stepIndex,
        string? stepId,
        StepPropertyReadErrorKind propertyReadErrorKind)
    {
        return Create(
            IpcRequestContractReadErrorKind.StepArgsContractViolation,
            stepIndex: stepIndex,
            stepId: stepId,
            stepPropertyReadErrorKind: propertyReadErrorKind);
    }

    public static IpcRequestContractReadError StepOnContractViolation (
        int stepIndex,
        string? stepId,
        StepPropertyReadErrorKind propertyReadErrorKind)
    {
        return Create(
            IpcRequestContractReadErrorKind.StepOnContractViolation,
            stepIndex: stepIndex,
            stepId: stepId,
            stepPropertyReadErrorKind: propertyReadErrorKind);
    }

    public static IpcRequestContractReadError StepSelectContractViolation (
        int stepIndex,
        string? stepId,
        StepPropertyReadErrorKind propertyReadErrorKind)
    {
        return Create(
            IpcRequestContractReadErrorKind.StepSelectContractViolation,
            stepIndex: stepIndex,
            stepId: stepId,
            stepPropertyReadErrorKind: propertyReadErrorKind);
    }

    public static IpcRequestContractReadError StepActionsContractViolation (
        int stepIndex,
        string? stepId,
        StepPropertyReadErrorKind propertyReadErrorKind)
    {
        return Create(
            IpcRequestContractReadErrorKind.StepActionsContractViolation,
            stepIndex: stepIndex,
            stepId: stepId,
            stepPropertyReadErrorKind: propertyReadErrorKind);
    }

    public static IpcRequestContractReadError StepActionMustBeObject (
        int stepIndex,
        string? stepId)
    {
        return Create(
            IpcRequestContractReadErrorKind.StepActionMustBeObject,
            stepIndex: stepIndex,
            stepId: stepId);
    }

    public static IpcRequestContractReadError StepCommitContractViolation (
        int stepIndex,
        string? stepId,
        JsonStringReadError jsonStringReadError)
    {
        return Create(
            IpcRequestContractReadErrorKind.StepCommitContractViolation,
            stepIndex: stepIndex,
            stepId: stepId,
            jsonStringReadError: jsonStringReadError);
    }

    public static IpcRequestContractReadError StepEditContractViolation (
        int stepIndex,
        string? stepId,
        string diagnosticMessage)
    {
        return Create(
            IpcRequestContractReadErrorKind.StepEditContractViolation,
            stepIndex: stepIndex,
            stepId: stepId,
            diagnosticMessage: diagnosticMessage);
    }

    public static IpcRequestContractReadError DuplicatedStepIdError (
        int stepIndex,
        string duplicatedStepId)
    {
        return Create(
            IpcRequestContractReadErrorKind.DuplicatedStepId,
            stepIndex: stepIndex,
            stepId: duplicatedStepId,
            duplicatedStepId: duplicatedStepId);
    }

    private static IpcRequestContractReadError Create (
        IpcRequestContractReadErrorKind kind,
        int stepIndex = -1,
        string? unknownPropertyName = null,
        string? stepId = null,
        string? duplicatedStepId = null,
        string? diagnosticMessage = null,
        JsonStringReadError jsonStringReadError = default,
        StepPropertyReadErrorKind stepPropertyReadErrorKind = StepPropertyReadErrorKind.None)
    {
        if (jsonStringReadError == default)
        {
            jsonStringReadError = JsonStringReadError.None;
        }

        return new IpcRequestContractReadError(
            Kind: kind,
            StepIndex: stepIndex,
            UnknownPropertyName: unknownPropertyName,
            StepId: stepId,
            DuplicatedStepId: duplicatedStepId,
            DiagnosticMessage: diagnosticMessage,
            JsonStringReadError: jsonStringReadError,
            StepPropertyReadErrorKind: stepPropertyReadErrorKind);
    }
}