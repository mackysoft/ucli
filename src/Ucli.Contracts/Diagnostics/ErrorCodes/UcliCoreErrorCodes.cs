namespace MackySoft.Ucli.Contracts;

/// <summary> Defines shared uCLI core error code values. </summary>
public static class UcliCoreErrorCodes
{
    /// <summary> Gets the error code emitted when request arguments are invalid. </summary>
    public static readonly UcliCode InvalidArgument = new("INVALID_ARGUMENT");

    /// <summary> Gets the error code emitted when required workspace initialization has not been completed. </summary>
    public static readonly UcliCode NotInitialized = new("NOT_INITIALIZED");

    /// <summary> Gets the error code emitted when command execution is not yet implemented. </summary>
    public static readonly UcliCode CommandNotImplemented = new("COMMAND_NOT_IMPLEMENTED");

    /// <summary> Gets the error code emitted when an unexpected internal failure occurs. </summary>
    public static readonly UcliCode InternalError = new("INTERNAL_ERROR");
}
