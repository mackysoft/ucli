namespace MackySoft.Ucli.Operations;

/// <summary> Defines machine-readable validation error codes for static request validation. </summary>
internal static class ValidationErrorCodes
{
    /// <summary> Gets the error code used when protocolVersion differs from supported value. </summary>
    public const string ProtocolVersionMismatch = "PROTOCOL_VERSION_MISMATCH";

    /// <summary> Gets the error code used when requestId is not a valid UUID. </summary>
    public const string RequestIdInvalid = "REQUEST_ID_INVALID";

    /// <summary> Gets the error code used when ops is missing or empty. </summary>
    public const string OpsRequired = "OPS_REQUIRED";

    /// <summary> Gets the error code used when opId is missing. </summary>
    public const string OpIdRequired = "OP_ID_REQUIRED";

    /// <summary> Gets the error code used when opId appears multiple times. </summary>
    public const string OpIdDuplicated = "OP_ID_DUPLICATED";

    /// <summary> Gets the error code used when op name is missing. </summary>
    public const string OpNameRequired = "OP_NAME_REQUIRED";

    /// <summary> Gets the error code used when op name is not registered. </summary>
    public const string OperationNotFound = "OPERATION_NOT_FOUND";

    /// <summary> Gets the error code used when operation is blocked by authorization rules. </summary>
    public const string OperationNotAllowed = "OPERATION_NOT_ALLOWED";
}