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

        UcliErrorCodeDescriptorFactory.Create(
            code: DaemonErrorCodes.DaemonStartupBlocked,
            category: "daemon",
            summary: "Unity daemon startup was blocked by a known Unity startup condition.",
            meaning: "The daemon start operation could not complete because Unity reported, or the launcher observed, a terminal startup blocker before the daemon endpoint became available.",
            appliesTo: [UcliCommandIds.DaemonStart],
            possiblePhases: ["daemonStartup", "guiBootstrap", "scriptCompilation", "packageResolution", "userAction"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect: ["payload.diagnosis", "payload.primaryDiagnostic", UcliErrorInspectTargets.DaemonStatusCommand, UcliErrorInspectTargets.UnityErrorLogsCommand],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: "payload.diagnosis.reason identifies a project error",
                    Action: "Resolve the reported Unity project error, then start the daemon again."),
                new UcliErrorNextActionDescriptor(
                    When: "payload.diagnosis.actionRequired identifies a Unity dialog or manual action",
                    Action: "Resolve the Unity Editor prompt manually, then inspect daemon status before retrying."),
            ],
            relatedCodes:
            [
                EditorLifecycleErrorCodes.EditorCompileFailed,
                EditorLifecycleErrorCodes.EditorModalBlocked,
                EditorLifecycleErrorCodes.EditorSafeMode,
                IpcTransportErrorCodes.IpcTimeout,
            ]),
    ];
}
