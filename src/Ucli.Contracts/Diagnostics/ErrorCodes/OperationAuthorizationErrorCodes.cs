namespace MackySoft.Ucli.Contracts;

/// <summary> Defines operation authorization error code values. </summary>
public static class OperationAuthorizationErrorCodes
{
    /// <summary> Gets the error code emitted when an operation is blocked by policy or explicit CLI guards. </summary>
    public static readonly UcliCode OperationNotAllowed = new("OPERATION_NOT_ALLOWED");
}
