namespace MackySoft.Ucli.Contracts;

/// <summary> Defines execute request coordination error code values. </summary>
public static class ExecuteRequestErrorCodes
{
    /// <summary> Gets the error code emitted when one request-id is reused with different request content. </summary>
    public static readonly UcliErrorCode RequestIdConflict = new("REQUEST_ID_CONFLICT");
}
