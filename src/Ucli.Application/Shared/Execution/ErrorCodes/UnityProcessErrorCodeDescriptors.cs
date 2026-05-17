namespace MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;

internal static class UnityProcessErrorCodeDescriptors
{
    private static readonly UcliCommand[] UnityProcessCommands =
    [
        UcliCommandIds.DaemonStart,
        UcliCommandIds.Plan,
        UcliCommandIds.Call,
        UcliCommandIds.Resolve,
        UcliCommandIds.Query,
        UcliCommandIds.Refresh,
        UcliCommandIds.TestRun,
    ];

    public static IReadOnlyList<UcliErrorDescriptor> All { get; } =
    [
        UcliErrorDescriptorFactory.Create(
            code: UnityProcessErrorCodes.UnityProjectAlreadyOpen,
            category: "unityProcess",
            summary: "The Unity project is already open or locked by another process.",
            meaning: "uCLI cannot start the requested Unity process because the project is locked by another Unity instance.",
            appliesTo: UnityProcessCommands,
            possiblePhases: ["processLaunch", "projectLockCheck"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["status", UcliErrorInspectTargets.DaemonListCommand, UcliErrorInspectTargets.DaemonErrorLogsCommand],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Close or reuse the existing Unity process for this project before retrying."),
            ],
            relatedCodes:
            [
                UnityProcessErrorCodes.UnityProjectLockAmbiguous,
                UnityProcessErrorCodes.UnityProjectLockCleanupFailed,
                DaemonErrorCodes.DaemonEditorModeMismatch,
            ]),

        UcliErrorDescriptorFactory.Create(
            code: UnityProcessErrorCodes.UnityProjectLockAmbiguous,
            category: "unityProcess",
            summary: "Unity project lock-file ownership could not be determined safely.",
            meaning: "uCLI found a Unity project lock file but could not prove whether it belongs to a live Unity process for the same project.",
            appliesTo: UnityProcessCommands,
            possiblePhases: ["processLaunch", "projectLockCheck"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["projectPath", "status", UcliErrorInspectTargets.DaemonListCommand, UcliErrorInspectTargets.DaemonErrorLogsCommand],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Inspect the lock-file owner and close the matching Unity process, or remove the stale lock only after confirming it is not owned by a live Unity process."),
            ],
            relatedCodes:
            [
                UnityProcessErrorCodes.UnityProjectAlreadyOpen,
                UnityProcessErrorCodes.UnityProjectLockCleanupFailed,
            ]),

        UcliErrorDescriptorFactory.Create(
            code: UnityProcessErrorCodes.UnityProjectLockCleanupFailed,
            category: "unityProcess",
            summary: "Stale Unity project lock-file cleanup failed.",
            meaning: "uCLI determined the Unity project lock file was stale but could not remove it before starting Unity.",
            appliesTo: UnityProcessCommands,
            possiblePhases: ["processLaunch", "projectLockCheck"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect: ["projectPath", "status", UcliErrorInspectTargets.DaemonListCommand, UcliErrorInspectTargets.DaemonErrorLogsCommand],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Fix filesystem permissions or remove the confirmed stale lock file, then retry."),
            ],
            relatedCodes:
            [
                UnityProcessErrorCodes.UnityProjectAlreadyOpen,
                UnityProcessErrorCodes.UnityProjectLockAmbiguous,
            ]),
    ];
}
