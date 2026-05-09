namespace MackySoft.Ucli.Contracts;

internal static class IpcTransportErrorCodeDescriptors
{
    public static IReadOnlyList<UcliErrorCodeDescriptor> All { get; } =
    [
        UcliErrorCodeDescriptorFactory.Create(
            code: IpcTransportErrorCodes.IpcTimeout,
            category: "transport",
            summary: "The command timeout budget was exhausted.",
            meaning: "The CLI did not receive a completed IPC response before the timeout budget expired.",
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
            possiblePhases: ["readinessWait", "ipcDispatch", "unityExecution", "responseRead"],
            impliesNotApplied: false,
            mayBeIndeterminate: true,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect:
            [
                "status",
                "payload.executionState",
                "payload.opResults",
                "payload.opResults[].applied",
                "payload.opResults[].changed",
                "payload.opResults[].touched",
                "payload.diagnosis",
                "logs daemon",
                "logs unity",
            ],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: "request was not accepted",
                    Action: "Retry may be safe after checking daemon status."),
                new UcliErrorNextActionDescriptor(
                    When: "executionState is indeterminate",
                    Action: "Do not blindly retry. Inspect opResults, touched resources, and logs."),
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
