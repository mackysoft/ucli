namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Represents one <c>execute</c> arguments contract read error. </summary>
/// <param name="Kind"> The machine-readable error kind. </param>
/// <param name="StepIndex"> The step index when the error is step-scoped; otherwise <c>-1</c>. </param>
/// <param name="UnknownPropertyName"> The unknown property name when the error is unknown-property related. </param>
/// <param name="StepId"> The step identifier context when available. </param>
/// <param name="DuplicatedStepId"> The duplicated step identifier for duplicate-id errors. </param>
/// <param name="DiagnosticMessage"> The nested detailed diagnostic message for structural contract violations. </param>
/// <param name="JsonStringReadError"> The nested JSON string read error for string-contract violations. </param>
/// <param name="StepPropertyReadErrorKind"> The nested object/array property read error kind for step contract violations. </param>
internal readonly record struct IpcExecuteArgumentsContractReadError (
    IpcExecuteArgumentsContractReadErrorKind Kind,
    int StepIndex,
    string? UnknownPropertyName,
    IpcExecuteStepId? StepId,
    IpcExecuteStepId? DuplicatedStepId,
    string? DiagnosticMessage,
    JsonStringReadError JsonStringReadError,
    StepPropertyReadErrorKind StepPropertyReadErrorKind)
{
    /// <summary> Gets an empty error value that indicates success. </summary>
    public static IpcExecuteArgumentsContractReadError None => new(
        Kind: IpcExecuteArgumentsContractReadErrorKind.None,
        StepIndex: -1,
        UnknownPropertyName: null,
        StepId: null,
        DuplicatedStepId: null,
        DiagnosticMessage: null,
        JsonStringReadError: JsonStringReadError.None,
        StepPropertyReadErrorKind: StepPropertyReadErrorKind.None);

    public static IpcExecuteArgumentsContractReadError ArgumentsMustBeObject ()
    {
        return Create(IpcExecuteArgumentsContractReadErrorKind.ArgumentsMustBeObject);
    }

    public static IpcExecuteArgumentsContractReadError UnknownArgumentsProperty (string unknownPropertyName)
    {
        return Create(
            IpcExecuteArgumentsContractReadErrorKind.UnknownArgumentsProperty,
            unknownPropertyName: unknownPropertyName);
    }

    public static IpcExecuteArgumentsContractReadError ProtocolVersionMissing ()
    {
        return Create(IpcExecuteArgumentsContractReadErrorKind.ProtocolVersionMissing);
    }

    public static IpcExecuteArgumentsContractReadError ProtocolVersionTypeMismatch ()
    {
        return Create(IpcExecuteArgumentsContractReadErrorKind.ProtocolVersionTypeMismatch);
    }

    public static IpcExecuteArgumentsContractReadError StepsMissing ()
    {
        return Create(IpcExecuteArgumentsContractReadErrorKind.StepsMissing);
    }

    public static IpcExecuteArgumentsContractReadError StepsTypeMismatch ()
    {
        return Create(IpcExecuteArgumentsContractReadErrorKind.StepsTypeMismatch);
    }

    public static IpcExecuteArgumentsContractReadError StepMustBeObject (int stepIndex)
    {
        return Create(IpcExecuteArgumentsContractReadErrorKind.StepMustBeObject, stepIndex: stepIndex);
    }

    public static IpcExecuteArgumentsContractReadError StepKindContractViolation (
        int stepIndex,
        JsonStringReadError jsonStringReadError)
    {
        return Create(
            IpcExecuteArgumentsContractReadErrorKind.StepKindContractViolation,
            stepIndex: stepIndex,
            jsonStringReadError: jsonStringReadError);
    }

    public static IpcExecuteArgumentsContractReadError StepKindUnsupported (
        int stepIndex,
        string stepKind)
    {
        return Create(
            IpcExecuteArgumentsContractReadErrorKind.StepKindUnsupported,
            stepIndex: stepIndex,
            unknownPropertyName: stepKind);
    }

    public static IpcExecuteArgumentsContractReadError UnknownStepProperty (
        int stepIndex,
        string unknownPropertyName)
    {
        return Create(
            IpcExecuteArgumentsContractReadErrorKind.UnknownStepProperty,
            stepIndex: stepIndex,
            unknownPropertyName: unknownPropertyName);
    }

    public static IpcExecuteArgumentsContractReadError StepIdContractViolation (
        int stepIndex,
        JsonStringReadError jsonStringReadError)
    {
        return Create(
            IpcExecuteArgumentsContractReadErrorKind.StepIdContractViolation,
            stepIndex: stepIndex,
            jsonStringReadError: jsonStringReadError);
    }

    public static IpcExecuteArgumentsContractReadError StepOpContractViolation (
        int stepIndex,
        IpcExecuteStepId? stepId,
        JsonStringReadError jsonStringReadError)
    {
        return Create(
            IpcExecuteArgumentsContractReadErrorKind.StepOpContractViolation,
            stepIndex: stepIndex,
            stepId: stepId,
            jsonStringReadError: jsonStringReadError);
    }

    public static IpcExecuteArgumentsContractReadError StepArgsContractViolation (
        int stepIndex,
        IpcExecuteStepId? stepId,
        StepPropertyReadErrorKind propertyReadErrorKind)
    {
        return Create(
            IpcExecuteArgumentsContractReadErrorKind.StepArgsContractViolation,
            stepIndex: stepIndex,
            stepId: stepId,
            stepPropertyReadErrorKind: propertyReadErrorKind);
    }

    public static IpcExecuteArgumentsContractReadError StepOnContractViolation (
        int stepIndex,
        IpcExecuteStepId? stepId,
        StepPropertyReadErrorKind propertyReadErrorKind)
    {
        return Create(
            IpcExecuteArgumentsContractReadErrorKind.StepOnContractViolation,
            stepIndex: stepIndex,
            stepId: stepId,
            stepPropertyReadErrorKind: propertyReadErrorKind);
    }

    public static IpcExecuteArgumentsContractReadError StepSelectContractViolation (
        int stepIndex,
        IpcExecuteStepId? stepId,
        StepPropertyReadErrorKind propertyReadErrorKind)
    {
        return Create(
            IpcExecuteArgumentsContractReadErrorKind.StepSelectContractViolation,
            stepIndex: stepIndex,
            stepId: stepId,
            stepPropertyReadErrorKind: propertyReadErrorKind);
    }

    public static IpcExecuteArgumentsContractReadError StepActionsContractViolation (
        int stepIndex,
        IpcExecuteStepId? stepId,
        StepPropertyReadErrorKind propertyReadErrorKind)
    {
        return Create(
            IpcExecuteArgumentsContractReadErrorKind.StepActionsContractViolation,
            stepIndex: stepIndex,
            stepId: stepId,
            stepPropertyReadErrorKind: propertyReadErrorKind);
    }

    public static IpcExecuteArgumentsContractReadError StepActionMustBeObject (
        int stepIndex,
        IpcExecuteStepId? stepId)
    {
        return Create(
            IpcExecuteArgumentsContractReadErrorKind.StepActionMustBeObject,
            stepIndex: stepIndex,
            stepId: stepId);
    }

    public static IpcExecuteArgumentsContractReadError StepCommitContractViolation (
        int stepIndex,
        IpcExecuteStepId? stepId,
        JsonStringReadError jsonStringReadError)
    {
        return Create(
            IpcExecuteArgumentsContractReadErrorKind.StepCommitContractViolation,
            stepIndex: stepIndex,
            stepId: stepId,
            jsonStringReadError: jsonStringReadError);
    }

    public static IpcExecuteArgumentsContractReadError StepEditContractViolation (
        int stepIndex,
        IpcExecuteStepId? stepId,
        string diagnosticMessage)
    {
        return Create(
            IpcExecuteArgumentsContractReadErrorKind.StepEditContractViolation,
            stepIndex: stepIndex,
            stepId: stepId,
            diagnosticMessage: diagnosticMessage);
    }

    public static IpcExecuteArgumentsContractReadError DuplicatedStepIdError (
        int stepIndex,
        IpcExecuteStepId duplicatedStepId)
    {
        return Create(
            IpcExecuteArgumentsContractReadErrorKind.DuplicatedStepId,
            stepIndex: stepIndex,
            stepId: duplicatedStepId,
            duplicatedStepId: duplicatedStepId);
    }

    private static IpcExecuteArgumentsContractReadError Create (
        IpcExecuteArgumentsContractReadErrorKind kind,
        int stepIndex = -1,
        string? unknownPropertyName = null,
        IpcExecuteStepId? stepId = null,
        IpcExecuteStepId? duplicatedStepId = null,
        string? diagnosticMessage = null,
        JsonStringReadError jsonStringReadError = default,
        StepPropertyReadErrorKind stepPropertyReadErrorKind = StepPropertyReadErrorKind.None)
    {
        if (jsonStringReadError == default)
        {
            jsonStringReadError = JsonStringReadError.None;
        }

        return new IpcExecuteArgumentsContractReadError(
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
