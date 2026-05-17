namespace MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

internal static class UnityExecutionModeDecisionErrorCodeDescriptors
{
    public static IReadOnlyList<UcliErrorDescriptor> All { get; } =
    [
        UcliErrorDescriptorFactory.Create(
            code: UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
            category: "daemon",
            summary: "Daemon mode was requested but no reachable daemon is running.",
            meaning: "The command requires an existing daemon connection, but uCLI could not resolve a live daemon session for the project.",
            appliesTo:
            [
                UcliCommandIds.Status,
                UcliCommandIds.Ready,
                UcliCommandIds.DaemonStatus,
                UcliCommandIds.Plan,
                UcliCommandIds.Call,
                UcliCommandIds.Resolve,
                UcliCommandIds.Query,
                UcliCommandIds.Refresh,
                UcliCommandIds.TestRun,
            ],
            possiblePhases: ["modeDecision", "daemonReachability"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect: ["status", UcliErrorInspectTargets.DaemonStatusCommand, UcliErrorInspectTargets.DaemonListCommand],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Start a daemon or rerun the command with a mode that can launch Unity."),
            ],
            relatedCodes: [DaemonErrorCodes.DaemonEditorModeMismatch]),

        UcliErrorDescriptorFactory.Create(
            code: UnityExecutionModeDecisionErrorCodes.DaemonRunningOneshotForbidden,
            category: "daemon",
            summary: "Oneshot mode was requested while a daemon is reachable.",
            meaning: "The command policy forbids starting a separate oneshot Unity process when an active daemon session already owns the project.",
            appliesTo:
            [
                UcliCommandIds.Ready,
                UcliCommandIds.Plan,
                UcliCommandIds.Call,
                UcliCommandIds.Resolve,
                UcliCommandIds.Query,
                UcliCommandIds.Refresh,
                UcliCommandIds.TestRun,
            ],
            possiblePhases: ["modeDecision", "daemonReachability"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["status", UcliErrorInspectTargets.DaemonStatusCommand, UcliErrorInspectTargets.DaemonListCommand],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Use daemon mode, stop the daemon intentionally, or change the command mode policy."),
            ],
            relatedCodes: [DaemonErrorCodes.DaemonEditorModeMismatch]),
    ];
}
