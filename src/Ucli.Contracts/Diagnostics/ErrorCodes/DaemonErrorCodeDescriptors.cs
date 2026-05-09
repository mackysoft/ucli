namespace MackySoft.Ucli.Contracts;

internal static class DaemonErrorCodeDescriptors
{
    public static IReadOnlyList<UcliErrorCodeDescriptor> All { get; } =
    [
        UcliErrorCodeDescriptorFactory.Create(
            code: DaemonErrorCodes.DaemonEditorModeMismatch,
            category: "daemon",
            summary: "The requested Editor mode conflicts with an existing daemon session.",
            meaning: "A daemon is already associated with the project using a different Editor mode than the one requested by the command.",
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
            possiblePhases: ["modeDecision", "daemonSessionValidation"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["status", "payload.daemon", UcliErrorInspectTargets.DaemonStatusCommand, UcliErrorInspectTargets.DaemonListCommand],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Stop or reuse the existing daemon session, or rerun the command with the matching Editor mode."),
            ],
            relatedCodes: null),
    ];
}
