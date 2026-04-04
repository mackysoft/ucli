namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Defines machine-readable error kinds for request-contract reads. </summary>
internal enum IpcRequestContractReadErrorKind
{
    /// <summary> No error. </summary>
    None = 0,

    /// <summary> The request root must be a JSON object. </summary>
    RequestMustBeObject,

    /// <summary> The request contains one unknown top-level property. </summary>
    UnknownRequestProperty,

    /// <summary> Required property <c>protocolVersion</c> is missing. </summary>
    ProtocolVersionMissing,

    /// <summary> Property <c>protocolVersion</c> exists but is not an integer. </summary>
    ProtocolVersionTypeMismatch,

    /// <summary> Property <c>requestId</c> violates string-contract constraints. </summary>
    RequestIdContractViolation,

    /// <summary> Property <c>requestId</c> is not UUID format <c>D</c>. </summary>
    RequestIdFormatMismatch,

    /// <summary> Required property <c>steps</c> is missing. </summary>
    StepsMissing,

    /// <summary> Property <c>steps</c> exists but is not an array. </summary>
    StepsTypeMismatch,

    /// <summary> One step element must be an object for the current profile. </summary>
    StepMustBeObject,

    /// <summary> Property <c>kind</c> violates string-contract constraints. </summary>
    StepKindContractViolation,

    /// <summary> Property <c>kind</c> is not one supported step discriminator. </summary>
    StepKindUnsupported,

    /// <summary> One step object contains an unknown property. </summary>
    UnknownStepProperty,

    /// <summary> Property <c>id</c> violates string-contract constraints. </summary>
    StepIdContractViolation,

    /// <summary> Property <c>op</c> violates string-contract constraints. </summary>
    StepOpContractViolation,

    /// <summary> Property <c>args</c> violates object-contract constraints. </summary>
    StepArgsContractViolation,

    /// <summary> Property <c>on</c> violates object-contract constraints. </summary>
    StepOnContractViolation,

    /// <summary> Property <c>select</c> violates object-contract constraints. </summary>
    StepSelectContractViolation,

    /// <summary> Property <c>actions</c> violates array-contract constraints. </summary>
    StepActionsContractViolation,

    /// <summary> One action element under <c>actions</c> must be an object. </summary>
    StepActionMustBeObject,

    /// <summary> Property <c>commit</c> violates string-contract constraints. </summary>
    StepCommitContractViolation,

    /// <summary> Step identifier is duplicated in the same request. </summary>
    DuplicatedStepId,
}