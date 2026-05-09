using MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;

namespace MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;

internal static class ExecutionErrorCodeDescriptors
{
    public static IReadOnlyList<UcliErrorCodeDescriptor> All { get; } =
    [
        ApplicationErrorCodeDescriptorFactory.Create(
            code: ExecutionErrorCodes.Canceled,
            category: "execution",
            summary: "Command execution was canceled.",
            meaning: "The command stopped because cancellation was requested by the host, caller, or execution deadline coordination.",
            appliesTo:
            [
                UcliCommandIds.DaemonStart,
                UcliCommandIds.DaemonStop,
                UcliCommandIds.DaemonCleanup,
                UcliCommandIds.Plan,
                UcliCommandIds.Call,
                UcliCommandIds.Resolve,
                UcliCommandIds.Query,
                UcliCommandIds.Refresh,
                UcliCommandIds.TestRun,
            ],
            possiblePhases: ["preflight", "execution", "ipcDispatch", "unityExecution"],
            impliesNotApplied: null,
            mayBeIndeterminate: true,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect: ["status", "payload", "errors", "logs daemon", "logs unity"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Check whether the request reached Unity before retrying."),
            ],
            relatedCodes: [IpcTransportErrorCodes.IpcTimeout]),
    ];
}
