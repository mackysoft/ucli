namespace MackySoft.Ucli.Contracts;

internal static class IpcProtocolErrorCodeDescriptors
{
    private static readonly UcliCommand[] IpcCommands =
    [
        UcliCommandIds.DaemonStart,
        UcliCommandIds.DaemonStatus,
        UcliCommandIds.Plan,
        UcliCommandIds.Call,
        UcliCommandIds.Resolve,
        UcliCommandIds.Query,
        UcliCommandIds.Refresh,
        UcliCommandIds.Ops,
        UcliCommandIds.TestRun,
    ];

    public static IReadOnlyList<UcliErrorCodeDescriptor> All { get; } =
    [
        UcliErrorCodeDescriptorFactory.Create(
            code: IpcProtocolErrorCodes.ProtocolVersionMismatch,
            category: "ipc",
            summary: "The IPC protocol versions are incompatible.",
            meaning: "The CLI and Unity-side server do not agree on the supported protocol version for the request.",
            appliesTo: IpcCommands,
            possiblePhases: ["ipcHandshake", "staticValidation"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["protocolVersion", "status", "ucli daemon status"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Update the CLI and Unity package so both sides use the same protocol version."),
            ],
            relatedCodes: null),

        UcliErrorCodeDescriptorFactory.Create(
            code: IpcProtocolErrorCodes.IpcMethodNotSupported,
            category: "ipc",
            summary: "The requested IPC method is not supported.",
            meaning: "The Unity-side server does not expose the IPC method needed by the command.",
            appliesTo: IpcCommands,
            possiblePhases: ["ipcDispatch"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["command", "payload.method", "ucli daemon status"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Update the Unity package or use a command supported by the running server."),
            ],
            relatedCodes: [IpcProtocolErrorCodes.ProtocolVersionMismatch]),

        UcliErrorCodeDescriptorFactory.Create(
            code: IpcProtocolErrorCodes.IpcFrameTooLarge,
            category: "ipc",
            summary: "The IPC frame exceeds the configured size limit.",
            meaning: "The serialized request or response is larger than the IPC protocol is willing to accept.",
            appliesTo: IpcCommands,
            possiblePhases: ["ipcDispatch", "responseRead"],
            impliesNotApplied: null,
            mayBeIndeterminate: true,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect: ["payload", "errors[].message", "logs daemon", "logs unity"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: "the request frame was rejected before dispatch",
                    Action: "Reduce request size and retry."),
                new UcliErrorNextActionDescriptor(
                    When: "the response frame exceeded the limit",
                    Action: "Do not assume the operation was not applied; inspect touched resources and logs."),
            ],
            relatedCodes: null),
    ];
}
