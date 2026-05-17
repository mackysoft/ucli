namespace MackySoft.Ucli.Contracts;

internal static class IpcTransportErrorCodeDescriptors
{
    public static IReadOnlyList<UcliErrorDescriptor> All { get; } =
    [
        UcliErrorDescriptorFactory.Create(
            code: IpcTransportErrorCodes.IpcTimeout,
            category: "transport",
            summary: "The command timeout budget was exhausted.",
            meaning: "The command did not complete before its timeout budget expired. For IPC-backed commands, the CLI may not have received a completed IPC response.",
            appliesTo:
            [
                UcliCommandIds.Status,
                UcliCommandIds.Ready,
                UcliCommandIds.Validate,
                UcliCommandIds.DaemonStart,
                UcliCommandIds.DaemonStop,
                UcliCommandIds.DaemonCleanup,
                UcliCommandIds.DaemonStatus,
                UcliCommandIds.DaemonList,
                UcliCommandIds.LogsDaemonRead,
                UcliCommandIds.LogsUnityRead,
                UcliCommandIds.LogsUnityClear,
                UcliCommandIds.Test,
                UcliCommandIds.TestRun,
                UcliCommandIds.Plan,
                UcliCommandIds.Call,
                UcliCommandIds.Resolve,
                UcliCommandIds.Query,
                UcliCommandIds.Refresh,
                UcliCommandIds.Ops,
            ],
            possiblePhases: ["readinessWait", "ipcDispatch", "unityExecution", "responseRead", "daemonLifecycle", "worktreeDiscovery", "logRead"],
            impliesNotApplied: false,
            mayBeIndeterminate: true,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect:
            [
                "status",
                "payload.opResults",
                "payload.opResults[].applied",
                "payload.opResults[].changed",
                "payload.opResults[].touched",
                "payload.readPostcondition",
                "payload.diagnosis",
                UcliErrorInspectTargets.DaemonErrorLogsCommand,
                UcliErrorInspectTargets.UnityErrorLogsCommand,
            ],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: "request was not accepted",
                    Action: "Retry may be safe after checking daemon status."),
                new UcliErrorNextActionDescriptor(
                    When: "operation acceptance or completion is unclear",
                    Action: "Do not blindly retry. Inspect opResults, readPostcondition, touched resources, and logs."),
                new UcliErrorNextActionDescriptor(
                    When: "lifecycle is compiling or busy",
                    Action: "Wait for lifecycleState=ready, then rerun the appropriate command."),
            ],
            relatedCodes:
            [
                EditorLifecycleErrorCodes.EditorCompiling,
                EditorLifecycleErrorCodes.EditorDomainReloading,
                EditorLifecycleErrorCodes.EditorBusy,
            ]),
    ];
}
