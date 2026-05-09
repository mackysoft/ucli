using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Maps Unity project lock preflight outcomes to execution errors. </summary>
internal static class UnityProjectLockPreflightErrorFactory
{
    /// <summary> Creates a launch-blocking error for one preflight result. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="preflightResult"> The preflight result to map. </param>
    /// <returns> A launch-blocking error, or <see langword="null" /> when startup may continue. </returns>
    public static ExecutionError? CreateLaunchBlockingError (
        ResolvedUnityProjectContext unityProject,
        UnityProjectLockPreflightResult preflightResult)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(preflightResult);

        return preflightResult.Status switch
        {
            UnityProjectLockPreflightStatus.Unlocked or UnityProjectLockPreflightStatus.StaleLockCleared => null,
            UnityProjectLockPreflightStatus.ActiveLock => ExecutionError.InternalError(
                UnityProjectLockFailureMessage.CreateAlreadyOpen(unityProject.UnityProjectRoot, preflightResult.LockFilePath),
                UnityProcessErrorCodes.UnityProjectAlreadyOpen),
            UnityProjectLockPreflightStatus.Ambiguous => ExecutionError.InternalError(
                preflightResult.Message ?? UnityProjectLockFailureMessage.CreateAmbiguous(
                    unityProject.UnityProjectRoot,
                    preflightResult.LockFilePath ?? "<unknown>",
                    "Lock owner could not be determined safely."),
                UnityProcessErrorCodes.UnityProjectLockAmbiguous),
            UnityProjectLockPreflightStatus.CleanupFailed => ExecutionError.InternalError(
                preflightResult.Message ?? UnityProjectLockFailureMessage.CreateCleanupFailed(
                    preflightResult.LockFilePath ?? "<unknown>",
                    "Unknown cleanup failure."),
                UnityProcessErrorCodes.UnityProjectLockCleanupFailed),
            UnityProjectLockPreflightStatus.InspectionFailed => ExecutionError.InternalError(
                preflightResult.Message ?? "Unity project lock-file state could not be inspected.",
                UcliCoreErrorCodes.InternalError),
            _ => throw new ArgumentOutOfRangeException(nameof(preflightResult), preflightResult.Status, "Unknown Unity project lock preflight status."),
        };
    }
}
