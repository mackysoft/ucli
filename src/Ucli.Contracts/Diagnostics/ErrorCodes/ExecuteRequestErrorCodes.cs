namespace MackySoft.Ucli.Contracts;

/// <summary> Defines execute request coordination error code values. </summary>
public static class ExecuteRequestErrorCodes
{
    /// <summary> Gets the error code emitted when one request-id is reused with different request content. </summary>
    public static readonly UcliCode RequestIdConflict = new("REQUEST_ID_CONFLICT");

    /// <summary> Gets the diagnostic code emitted when hierarchy paths cannot represent GameObject names containing slashes. </summary>
    public static readonly UcliCode HierarchyPathUnrepresentableObjects = new("HIERARCHY_PATH_UNREPRESENTABLE_OBJECTS");

    /// <summary> Gets the error code emitted when runtime operation results violate declared assurance facts. </summary>
    public static readonly UcliCode OperationContractViolation = new("OPERATION_CONTRACT_VIOLATION");
}
