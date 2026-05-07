namespace MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;

/// <summary> Defines machine-readable error codes for Unity process execution boundaries. </summary>
internal static class UnityProcessErrorCodes
{
    /// <summary> Gets the error code used when the Unity project is already open or locked by another Unity process. </summary>
    public static readonly UcliErrorCode UnityProjectAlreadyOpen = new UcliErrorCode("UNITY_PROJECT_ALREADY_OPEN");
}
