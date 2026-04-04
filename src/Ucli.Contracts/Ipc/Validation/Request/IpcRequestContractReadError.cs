namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Represents one request-contract read error. </summary>
/// <param name="Kind"> The machine-readable error kind. </param>
/// <param name="StepIndex"> The step index when the error is step-scoped; otherwise <c>-1</c>. </param>
/// <param name="UnknownPropertyName"> The unknown property name when the error is unknown-property related. </param>
/// <param name="StepId"> The step identifier context when available. </param>
/// <param name="DuplicatedStepId"> The duplicated step identifier for duplicate-id errors. </param>
/// <param name="JsonStringReadError"> The nested JSON string read error for string-contract violations. </param>
/// <param name="StepPropertyReadErrorKind"> The nested object/array property read error kind for step contract violations. </param>
internal readonly record struct IpcRequestContractReadError (
    IpcRequestContractReadErrorKind Kind,
    int StepIndex,
    string? UnknownPropertyName,
    string? StepId,
    string? DuplicatedStepId,
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
        JsonStringReadError: JsonStringReadError.None,
        StepPropertyReadErrorKind: StepPropertyReadErrorKind.None);

    public static IpcRequestContractReadError RequestMustBeObject ()
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.RequestMustBeObject,
            StepIndex: -1,
            UnknownPropertyName: null,
            StepId: null,
            DuplicatedStepId: null,
            JsonStringReadError: JsonStringReadError.None,
            StepPropertyReadErrorKind: StepPropertyReadErrorKind.None);
    }

    public static IpcRequestContractReadError UnknownRequestProperty (string unknownPropertyName)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.UnknownRequestProperty,
            StepIndex: -1,
            UnknownPropertyName: unknownPropertyName,
            StepId: null,
            DuplicatedStepId: null,
            JsonStringReadError: JsonStringReadError.None,
            StepPropertyReadErrorKind: StepPropertyReadErrorKind.None);
    }

    public static IpcRequestContractReadError ProtocolVersionMissing ()
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.ProtocolVersionMissing,
            StepIndex: -1,
            UnknownPropertyName: null,
            StepId: null,
            DuplicatedStepId: null,
            JsonStringReadError: JsonStringReadError.None,
            StepPropertyReadErrorKind: StepPropertyReadErrorKind.None);
    }

    public static IpcRequestContractReadError ProtocolVersionTypeMismatch ()
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.ProtocolVersionTypeMismatch,
            StepIndex: -1,
            UnknownPropertyName: null,
            StepId: null,
            DuplicatedStepId: null,
            JsonStringReadError: JsonStringReadError.None,
            StepPropertyReadErrorKind: StepPropertyReadErrorKind.None);
    }

    public static IpcRequestContractReadError RequestIdContractViolation (JsonStringReadError jsonStringReadError)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.RequestIdContractViolation,
            StepIndex: -1,
            UnknownPropertyName: null,
            StepId: null,
            DuplicatedStepId: null,
            JsonStringReadError: jsonStringReadError,
            StepPropertyReadErrorKind: StepPropertyReadErrorKind.None);
    }

    public static IpcRequestContractReadError RequestIdFormatMismatch ()
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.RequestIdFormatMismatch,
            StepIndex: -1,
            UnknownPropertyName: null,
            StepId: null,
            DuplicatedStepId: null,
            JsonStringReadError: JsonStringReadError.None,
            StepPropertyReadErrorKind: StepPropertyReadErrorKind.None);
    }

    public static IpcRequestContractReadError StepsMissing ()
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.StepsMissing,
            StepIndex: -1,
            UnknownPropertyName: null,
            StepId: null,
            DuplicatedStepId: null,
            JsonStringReadError: JsonStringReadError.None,
            StepPropertyReadErrorKind: StepPropertyReadErrorKind.None);
    }

    public static IpcRequestContractReadError StepsTypeMismatch ()
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.StepsTypeMismatch,
            StepIndex: -1,
            UnknownPropertyName: null,
            StepId: null,
            DuplicatedStepId: null,
            JsonStringReadError: JsonStringReadError.None,
            StepPropertyReadErrorKind: StepPropertyReadErrorKind.None);
    }

    public static IpcRequestContractReadError StepMustBeObject (int stepIndex)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.StepMustBeObject,
            StepIndex: stepIndex,
            UnknownPropertyName: null,
            StepId: null,
            DuplicatedStepId: null,
            JsonStringReadError: JsonStringReadError.None,
            StepPropertyReadErrorKind: StepPropertyReadErrorKind.None);
    }

    public static IpcRequestContractReadError StepKindContractViolation (
        int stepIndex,
        JsonStringReadError jsonStringReadError)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.StepKindContractViolation,
            StepIndex: stepIndex,
            UnknownPropertyName: null,
            StepId: null,
            DuplicatedStepId: null,
            JsonStringReadError: jsonStringReadError,
            StepPropertyReadErrorKind: StepPropertyReadErrorKind.None);
    }

    public static IpcRequestContractReadError StepKindUnsupported (
        int stepIndex,
        string stepKind)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.StepKindUnsupported,
            StepIndex: stepIndex,
            UnknownPropertyName: stepKind,
            StepId: null,
            DuplicatedStepId: null,
            JsonStringReadError: JsonStringReadError.None,
            StepPropertyReadErrorKind: StepPropertyReadErrorKind.None);
    }

    public static IpcRequestContractReadError UnknownStepProperty (
        int stepIndex,
        string unknownPropertyName)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.UnknownStepProperty,
            StepIndex: stepIndex,
            UnknownPropertyName: unknownPropertyName,
            StepId: null,
            DuplicatedStepId: null,
            JsonStringReadError: JsonStringReadError.None,
            StepPropertyReadErrorKind: StepPropertyReadErrorKind.None);
    }

    public static IpcRequestContractReadError StepIdContractViolation (
        int stepIndex,
        JsonStringReadError jsonStringReadError)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.StepIdContractViolation,
            StepIndex: stepIndex,
            UnknownPropertyName: null,
            StepId: null,
            DuplicatedStepId: null,
            JsonStringReadError: jsonStringReadError,
            StepPropertyReadErrorKind: StepPropertyReadErrorKind.None);
    }

    public static IpcRequestContractReadError StepOpContractViolation (
        int stepIndex,
        string? stepId,
        JsonStringReadError jsonStringReadError)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.StepOpContractViolation,
            StepIndex: stepIndex,
            UnknownPropertyName: null,
            StepId: stepId,
            DuplicatedStepId: null,
            JsonStringReadError: jsonStringReadError,
            StepPropertyReadErrorKind: StepPropertyReadErrorKind.None);
    }

    public static IpcRequestContractReadError StepArgsContractViolation (
        int stepIndex,
        string? stepId,
        StepPropertyReadErrorKind propertyReadErrorKind)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.StepArgsContractViolation,
            StepIndex: stepIndex,
            UnknownPropertyName: null,
            StepId: stepId,
            DuplicatedStepId: null,
            JsonStringReadError: JsonStringReadError.None,
            StepPropertyReadErrorKind: propertyReadErrorKind);
    }

    public static IpcRequestContractReadError StepOnContractViolation (
        int stepIndex,
        string? stepId,
        StepPropertyReadErrorKind propertyReadErrorKind)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.StepOnContractViolation,
            StepIndex: stepIndex,
            UnknownPropertyName: null,
            StepId: stepId,
            DuplicatedStepId: null,
            JsonStringReadError: JsonStringReadError.None,
            StepPropertyReadErrorKind: propertyReadErrorKind);
    }

    public static IpcRequestContractReadError StepSelectContractViolation (
        int stepIndex,
        string? stepId,
        StepPropertyReadErrorKind propertyReadErrorKind)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.StepSelectContractViolation,
            StepIndex: stepIndex,
            UnknownPropertyName: null,
            StepId: stepId,
            DuplicatedStepId: null,
            JsonStringReadError: JsonStringReadError.None,
            StepPropertyReadErrorKind: propertyReadErrorKind);
    }

    public static IpcRequestContractReadError StepActionsContractViolation (
        int stepIndex,
        string? stepId,
        StepPropertyReadErrorKind propertyReadErrorKind)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.StepActionsContractViolation,
            StepIndex: stepIndex,
            UnknownPropertyName: null,
            StepId: stepId,
            DuplicatedStepId: null,
            JsonStringReadError: JsonStringReadError.None,
            StepPropertyReadErrorKind: propertyReadErrorKind);
    }

    public static IpcRequestContractReadError StepActionMustBeObject (
        int stepIndex,
        string? stepId)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.StepActionMustBeObject,
            StepIndex: stepIndex,
            UnknownPropertyName: null,
            StepId: stepId,
            DuplicatedStepId: null,
            JsonStringReadError: JsonStringReadError.None,
            StepPropertyReadErrorKind: StepPropertyReadErrorKind.None);
    }

    public static IpcRequestContractReadError StepCommitContractViolation (
        int stepIndex,
        string? stepId,
        JsonStringReadError jsonStringReadError)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.StepCommitContractViolation,
            StepIndex: stepIndex,
            UnknownPropertyName: null,
            StepId: stepId,
            DuplicatedStepId: null,
            JsonStringReadError: jsonStringReadError,
            StepPropertyReadErrorKind: StepPropertyReadErrorKind.None);
    }

    public static IpcRequestContractReadError DuplicatedStepIdError (
        int stepIndex,
        string duplicatedStepId)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.DuplicatedStepId,
            StepIndex: stepIndex,
            UnknownPropertyName: null,
            StepId: duplicatedStepId,
            DuplicatedStepId: duplicatedStepId,
            JsonStringReadError: JsonStringReadError.None,
            StepPropertyReadErrorKind: StepPropertyReadErrorKind.None);
    }
}