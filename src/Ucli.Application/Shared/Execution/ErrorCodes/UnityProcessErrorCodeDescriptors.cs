namespace MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;

internal static class UnityProcessErrorCodeDescriptors
{
    public static IReadOnlyList<UcliErrorCodeDescriptor> All { get; } =
    [
        UcliErrorCodeDescriptorFactory.Create(
            code: UnityProcessErrorCodes.UnityProjectAlreadyOpen,
            category: "unityProcess",
            summary: "The Unity project is already open or locked by another process.",
            meaning: "uCLI cannot start the requested Unity process because the project is locked by another Unity instance.",
            appliesTo:
            [
                UcliCommandIds.DaemonStart,
                UcliCommandIds.Plan,
                UcliCommandIds.Call,
                UcliCommandIds.Resolve,
                UcliCommandIds.Query,
                UcliCommandIds.Refresh,
                UcliCommandIds.TestRun,
            ],
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
            relatedCodes: [DaemonErrorCodes.DaemonEditorModeMismatch]),
    ];
}
