namespace MackySoft.Ucli.Contracts;

internal static class DaemonErrorCodeDescriptors
{
    private static IReadOnlyList<UcliCommand> StartupObservationCommands { get; } =
    [
        UcliCommandIds.DaemonStart,
        UcliCommandIds.Plan,
        UcliCommandIds.Call,
        UcliCommandIds.Eval,
        UcliCommandIds.Resolve,
        UcliCommandIds.Query,
        UcliCommandIds.Refresh,
        UcliCommandIds.Ops,
        UcliCommandIds.TestRun,
    ];

    public static IReadOnlyList<UcliErrorDescriptor> All { get; } =
    [
        UcliErrorDescriptorFactory.Create(
            code: DaemonErrorCodes.DaemonEditorModeMismatch,
            category: "daemon",
            summary: "The requested Editor mode conflicts with an existing daemon session.",
            meaning: "A daemon is already associated with the project using a different Editor mode than the one requested by the command.",
            appliesTo:
            [
                UcliCommandIds.DaemonStart,
                UcliCommandIds.Plan,
                UcliCommandIds.Call,
                UcliCommandIds.Eval,
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

        UcliErrorDescriptorFactory.Create(
            code: DaemonErrorCodes.DaemonStartupBlocked,
            category: "daemon",
            summary: "Unity daemon startup was blocked by a known Unity startup condition.",
            meaning: "The daemon start operation could not complete because Unity reported, or the launcher observed, a terminal startup blocker before the daemon endpoint became available.",
            appliesTo: StartupObservationCommands,
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

        UcliErrorDescriptorFactory.Create(
            code: DaemonErrorCodes.DaemonStartProcessExited,
            category: "daemon",
            summary: "Unity exited before daemon startup completed.",
            meaning: "The Unity process ended before the daemon endpoint and session registration were established.",
            appliesTo: StartupObservationCommands,
            possiblePhases: ["daemonStartup", "processLaunch", "endpointRegistration"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.Unknown,
            inspect: ["payload.diagnosis", UcliErrorInspectTargets.UnityErrorLogsCommand],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Inspect Unity logs and startup diagnosis before deciding whether to retry."),
            ],
            relatedCodes: [DaemonErrorCodes.DaemonStartupBlocked, DaemonErrorCodes.DaemonEndpointNotRegistered]),

        UcliErrorDescriptorFactory.Create(
            code: DaemonErrorCodes.DaemonEndpointNotRegistered,
            category: "daemon",
            summary: "Daemon endpoint was not registered before the startup timeout.",
            meaning: "uCLI observed a Unity process but did not receive a daemon endpoint registration before the start budget expired.",
            appliesTo: StartupObservationCommands,
            possiblePhases: ["endpointRegistration", "daemonStartup"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.Unknown,
            inspect: ["payload.diagnosis", UcliErrorInspectTargets.DaemonStatusCommand, UcliErrorInspectTargets.UnityErrorLogsCommand],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Inspect daemon status and Unity logs to determine whether Unity is still starting or blocked."),
            ],
            relatedCodes: [IpcTransportErrorCodes.IpcTimeout, DaemonErrorCodes.DaemonStartupBlocked]),

        UcliErrorDescriptorFactory.Create(
            code: DaemonErrorCodes.DaemonSessionNotAvailable,
            category: "daemon",
            summary: "No daemon session is available for the requested project.",
            meaning: "The command requires an active daemon session, but no session metadata or reachable daemon endpoint is available for the resolved Unity project.",
            appliesTo:
            [
                UcliCommandIds.LogsDaemonRead,
                UcliCommandIds.LogsUnityRead,
                UcliCommandIds.LogsUnityClear,
            ],
            possiblePhases: ["daemonSessionResolution", "ipcDispatch", "logRead"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect: [UcliErrorInspectTargets.DaemonStatusCommand, UcliErrorInspectTargets.DaemonListCommand],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Start the daemon for the project, or rerun the command with the intended projectPath."),
            ],
            relatedCodes: [DaemonErrorCodes.DaemonEndpointNotRegistered, IpcTransportErrorCodes.IpcTimeout]),

        CreateStartupProjectFixDescriptor(
            DaemonErrorCodes.EditorCompileErrors,
            "Unity startup is blocked by script compile errors.",
            "Unity reported script compilation errors before daemon endpoint registration completed.",
            "scriptCompilation",
            "Fix the Unity compiler diagnostics, then start the daemon again.",
            [EditorLifecycleErrorCodes.EditorCompileFailed, EditorLifecycleErrorCodes.EditorSafeMode]),

        CreateStartupProjectFixDescriptor(
            DaemonErrorCodes.PackageResolutionFailed,
            "Unity startup is blocked by package resolution failure.",
            "Unity package resolution failed before daemon endpoint registration completed.",
            "packageResolution",
            "Resolve Unity package dependencies, then start the daemon again.",
            null),

        CreateStartupProjectFixDescriptor(
            DaemonErrorCodes.UcliPluginDependencyMissing,
            "uCLI plugin dependencies are missing during startup.",
            "Unity could not load the uCLI plugin because one or more required plugin dependencies are missing.",
            "pluginBootstrap",
            "Restore or reinstall the uCLI Unity plugin dependencies, then start the daemon again.",
            [DaemonErrorCodes.UcliPluginCompileFailed]),

        CreateStartupProjectFixDescriptor(
            DaemonErrorCodes.UcliPluginCompileFailed,
            "uCLI plugin compilation failed during startup.",
            "Unity could not compile the uCLI plugin before daemon endpoint registration completed.",
            "pluginBootstrap",
            "Fix the uCLI plugin compilation errors, then start the daemon again.",
            [DaemonErrorCodes.UcliPluginDependencyMissing, EditorLifecycleErrorCodes.EditorCompileFailed]),

        CreateStartupProjectFixDescriptor(
            DaemonErrorCodes.PrecompiledAssemblyConflict,
            "Unity startup is blocked by a precompiled assembly conflict.",
            "Unity detected conflicting precompiled assemblies before daemon endpoint registration completed.",
            "scriptCompilation",
            "Remove or rename the conflicting precompiled assembly, then start the daemon again.",
            [DaemonErrorCodes.EditorCompileErrors]),
    ];

    private static UcliErrorDescriptor CreateStartupProjectFixDescriptor (
        UcliCode code,
        string summary,
        string meaning,
        string phase,
        string nextAction,
        IReadOnlyList<UcliCode>? relatedCodes)
    {
        return UcliErrorDescriptorFactory.Create(
            code: code,
            category: "daemon",
            summary: summary,
            meaning: meaning,
            appliesTo: [UcliCommandIds.DaemonStart],
            possiblePhases: ["daemonStartup", phase],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["payload.diagnosis", "payload.diagnosis.primaryDiagnostic", UcliErrorInspectTargets.UnityErrorLogsCommand],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: nextAction),
            ],
            relatedCodes: relatedCodes);
    }
}
