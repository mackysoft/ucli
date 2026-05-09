namespace MackySoft.Ucli.Contracts;

internal static class IpcSessionErrorCodeDescriptors
{
    public static IReadOnlyList<UcliErrorCodeDescriptor> All { get; } =
    [
        UcliErrorCodeDescriptorFactory.Create(
            code: IpcSessionErrorCodes.SessionTokenRequired,
            category: "session",
            summary: "The IPC request omitted the required session token.",
            meaning: "The Unity-side server requires a sessionToken and rejected the request before executing it.",
            appliesTo:
            [
                UcliCommandIds.DaemonStatus,
                UcliCommandIds.Plan,
                UcliCommandIds.Call,
                UcliCommandIds.Resolve,
                UcliCommandIds.Query,
                UcliCommandIds.Refresh,
                UcliCommandIds.Ops,
                UcliCommandIds.TestRun,
            ],
            possiblePhases: ["ipcAuthentication"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["ucli daemon status", "ucli daemon list"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Resolve the active daemon session through the CLI session resolver, then rerun the command."),
            ],
            relatedCodes: [IpcSessionErrorCodes.SessionTokenInvalid]),

        UcliErrorCodeDescriptorFactory.Create(
            code: IpcSessionErrorCodes.SessionTokenInvalid,
            category: "session",
            summary: "The IPC request contained an invalid session token.",
            meaning: "The Unity-side server rejected the request because the provided sessionToken does not match the active session.",
            appliesTo:
            [
                UcliCommandIds.DaemonStatus,
                UcliCommandIds.Plan,
                UcliCommandIds.Call,
                UcliCommandIds.Resolve,
                UcliCommandIds.Query,
                UcliCommandIds.Refresh,
                UcliCommandIds.Ops,
                UcliCommandIds.TestRun,
            ],
            possiblePhases: ["ipcAuthentication"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["ucli daemon status", "ucli daemon list"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Refresh daemon session state, then rerun through active session resolution."),
            ],
            relatedCodes: [IpcSessionErrorCodes.SessionTokenRequired]),
    ];
}
