namespace MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;

/// <summary> Defines machine-readable error codes for Unity process execution boundaries. </summary>
internal static class UnityProcessErrorCodes
{
    /// <summary> Gets the error code used when the Unity project is already open or locked by another Unity process. </summary>
    public static readonly UcliErrorCode UnityProjectAlreadyOpen = new UcliErrorCode("UNITY_PROJECT_ALREADY_OPEN");

    /// <summary> Gets the error code used when Unity lock-file ownership cannot be determined safely. </summary>
    public static readonly UcliErrorCode UnityProjectLockAmbiguous = new UcliErrorCode("UNITY_PROJECT_LOCK_AMBIGUOUS");

    /// <summary> Gets the error code used when stale Unity lock-file cleanup fails. </summary>
    public static readonly UcliErrorCode UnityProjectLockCleanupFailed = new UcliErrorCode("UNITY_PROJECT_LOCK_CLEANUP_FAILED");
}
