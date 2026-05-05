namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Defines machine-readable validation error codes for static request validation. </summary>
internal static class ValidationErrorCodes
{
    /// <summary> Gets the error code used when protocolVersion differs from supported value. </summary>
    public const string ProtocolVersionMismatch = "PROTOCOL_VERSION_MISMATCH";

    /// <summary> Gets the error code used when requestId is not a valid UUID. </summary>
    public const string RequestIdInvalid = "REQUEST_ID_INVALID";

    /// <summary> Gets the error code used when steps is missing. </summary>
    public const string StepsRequired = "STEPS_REQUIRED";

    /// <summary> Gets the error code used when stepId is missing. </summary>
    public const string StepIdRequired = "STEP_ID_REQUIRED";

    /// <summary> Gets the error code used when stepId appears multiple times. </summary>
    public const string StepIdDuplicated = "STEP_ID_DUPLICATED";

    /// <summary> Gets the error code used when step kind is missing. </summary>
    public const string StepKindRequired = "STEP_KIND_REQUIRED";

    /// <summary> Gets the error code used when step kind is unsupported. </summary>
    public const string StepKindInvalid = "STEP_KIND_INVALID";

    /// <summary> Gets the error code used when op name is missing. </summary>
    public const string OperationNameRequired = "OPERATION_NAME_REQUIRED";

    /// <summary> Gets the error code used when op name is not registered. </summary>
    public const string OperationNotFound = "OPERATION_NOT_FOUND";

    /// <summary> Gets the error code used when operation is blocked by authorization rules. </summary>
    public const string OperationNotAllowed = "OPERATION_NOT_ALLOWED";

    /// <summary> Gets the error code used when an op step args object violates the registered schema. </summary>
    public const string OperationArgsInvalid = "OPERATION_ARGS_INVALID";

    /// <summary> Gets the error code used when an edit step violates DSL constraints. </summary>
    public const string EditStepInvalid = "EDIT_STEP_INVALID";
}
