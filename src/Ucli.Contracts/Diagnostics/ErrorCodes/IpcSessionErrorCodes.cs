namespace MackySoft.Ucli.Contracts;

/// <summary> Defines IPC session authentication error code values. </summary>
public static class IpcSessionErrorCodes
{
    /// <summary> Gets the error code emitted when a request omits <c>sessionToken</c>. </summary>
    public static readonly UcliCode SessionTokenRequired = new("SESSION_TOKEN_REQUIRED");

    /// <summary> Gets the error code emitted when a request contains an invalid <c>sessionToken</c>. </summary>
    public static readonly UcliCode SessionTokenInvalid = new("SESSION_TOKEN_INVALID");
}
