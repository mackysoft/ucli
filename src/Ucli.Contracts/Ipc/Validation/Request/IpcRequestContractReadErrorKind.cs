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

    /// <summary> Required property <c>ops</c> is missing. </summary>
    OperationsMissing,

    /// <summary> Property <c>ops</c> exists but is not an array. </summary>
    OperationsTypeMismatch,

    /// <summary> One operation element must be an object for the current profile. </summary>
    OperationMustBeObject,

    /// <summary> One operation object contains an unknown property. </summary>
    UnknownOperationProperty,

    /// <summary> Property <c>id</c> violates string-contract constraints. </summary>
    OperationIdContractViolation,

    /// <summary> Property <c>op</c> violates string-contract constraints. </summary>
    OperationNameContractViolation,

    /// <summary> Property <c>args</c> violates required object-contract constraints. </summary>
    OperationArgsContractViolation,

    /// <summary> Property <c>as</c> violates optional string-contract constraints. </summary>
    OperationAliasContractViolation,

    /// <summary> Property <c>expect</c> violates expectation constraints. </summary>
    OperationExpectationContractViolation,

    /// <summary> Operation identifier is duplicated in the same request. </summary>
    DuplicatedOperationId,
}