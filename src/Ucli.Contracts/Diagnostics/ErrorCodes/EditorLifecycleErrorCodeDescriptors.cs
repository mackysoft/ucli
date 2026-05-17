namespace MackySoft.Ucli.Contracts;

internal static class EditorLifecycleErrorCodeDescriptors
{
    private static readonly UcliCommand[] UnityExecutionCommands =
    [
        UcliCommandIds.Status,
        UcliCommandIds.DaemonStatus,
        UcliCommandIds.Plan,
        UcliCommandIds.Call,
        UcliCommandIds.Resolve,
        UcliCommandIds.Query,
        UcliCommandIds.Refresh,
        UcliCommandIds.Ops,
        UcliCommandIds.TestRun,
    ];

    public static IReadOnlyList<UcliErrorDescriptor> All { get; } =
    [
        CreateTransient(
            EditorLifecycleErrorCodes.EditorStarting,
            "Unity Editor startup is still in progress.",
            "The target Unity Editor has not finished startup and cannot reliably accept execution requests.",
            "readinessWait"),

        CreateTransient(
            EditorLifecycleErrorCodes.EditorBusy,
            "Unity Editor is busy with internal work.",
            "The target Unity Editor reported a busy lifecycle state and cannot accept the requested work yet.",
            "readinessWait"),

        CreateTransient(
            EditorLifecycleErrorCodes.EditorCompiling,
            "Unity Editor is compiling scripts.",
            "Compilation is active, so script-backed operations cannot be executed until the Editor returns to a ready state.",
            "readinessWait"),

        CreateTransient(
            EditorLifecycleErrorCodes.EditorDomainReloading,
            "Unity Editor is reloading the AppDomain.",
            "Domain reload is active or has disconnected the IPC session; a completed response is not available during this lifecycle transition.",
            "readinessWait"),

        CreateTransient(
            EditorLifecycleErrorCodes.EditorRecovering,
            "Unity Editor is recovering its IPC session.",
            "The Unity process is still alive but the daemon endpoint is temporarily unavailable during Editor lifecycle recovery.",
            "readinessWait"),

        CreateTransient(
            EditorLifecycleErrorCodes.EditorReimporting,
            "Unity Editor is refreshing or reimporting assets.",
            "Asset refresh or reimport work is active, so Unity cannot reliably accept execution requests until the Editor returns to a ready state.",
            "readinessWait"),

        UcliErrorDescriptorFactory.Create(
            code: EditorLifecycleErrorCodes.EditorCompileFailed,
            category: "lifecycle",
            summary: "Unity script compilation failed.",
            meaning: "Unity reported one or more script compilation errors, so script-backed operations cannot execute until the project compiles successfully.",
            appliesTo: UnityExecutionCommands,
            possiblePhases: ["readinessWait", "scriptCompilation"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["status", "payload.lifecycleState", "payload.primaryDiagnostic", UcliErrorInspectTargets.UnityErrorLogsCommand],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Fix the reported Unity compiler diagnostics, wait for lifecycleState=ready, then retry."),
            ],
            relatedCodes:
            [
                EditorLifecycleErrorCodes.EditorCompiling,
                EditorLifecycleErrorCodes.EditorSafeMode,
            ]),

        UcliErrorDescriptorFactory.Create(
            code: EditorLifecycleErrorCodes.EditorPlaymode,
            category: "lifecycle",
            summary: "Unity Editor is in Play Mode.",
            meaning: "The requested command requires Edit Mode or a mutation policy that is incompatible with the current Play Mode state.",
            appliesTo: UnityExecutionCommands,
            possiblePhases: ["readinessWait", "operationAuthorization"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect: ["status", "payload.lifecycleState", "payload.playMode"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: "the command requires Edit Mode",
                    Action: "Exit Play Mode, wait for lifecycleState=ready, then retry."),
                new UcliErrorNextActionDescriptor(
                    When: "the command is explicitly a Play Mode command",
                    Action: "Check the operation-specific Play Mode requirements before retrying."),
            ],
            relatedCodes:
            [
                PlayModeErrorCodes.PlayModeNotActive,
                PlayModeErrorCodes.PlayModePersistenceForbidden,
            ]),

        UcliErrorDescriptorFactory.Create(
            code: EditorLifecycleErrorCodes.EditorModalBlocked,
            category: "lifecycle",
            summary: "A modal dialog blocks Unity Editor execution.",
            meaning: "Unity cannot process the request because a modal dialog or blocking prompt requires user interaction.",
            appliesTo: UnityExecutionCommands,
            possiblePhases: ["readinessWait", "unityExecution"],
            impliesNotApplied: null,
            mayBeIndeterminate: true,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect: ["status", "payload.lifecycleState", UcliErrorInspectTargets.UnityErrorLogsCommand],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Resolve the Unity modal dialog manually, then inspect state before retrying."),
            ],
            relatedCodes: [EditorLifecycleErrorCodes.EditorBusy]),

        UcliErrorDescriptorFactory.Create(
            code: EditorLifecycleErrorCodes.EditorSafeMode,
            category: "lifecycle",
            summary: "Unity Editor is in Safe Mode.",
            meaning: "Unity has entered Safe Mode, so normal project operations are blocked until compile or import failures are resolved.",
            appliesTo: UnityExecutionCommands,
            possiblePhases: ["readinessWait", "pluginVerify"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["status", UcliErrorInspectTargets.UnityErrorLogsCommand],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Fix the Unity project errors that caused Safe Mode, reopen the project, then retry."),
            ],
            relatedCodes: [EditorLifecycleErrorCodes.EditorCompiling]),

        UcliErrorDescriptorFactory.Create(
            code: EditorLifecycleErrorCodes.EditorShuttingDown,
            category: "lifecycle",
            summary: "Unity Editor shutdown is in progress.",
            meaning: "The target Unity Editor is closing and cannot accept new work through the current session.",
            appliesTo: UnityExecutionCommands,
            possiblePhases: ["readinessWait", "ipcDispatch"],
            impliesNotApplied: null,
            mayBeIndeterminate: true,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect: ["status", UcliErrorInspectTargets.DaemonStatusCommand, UcliErrorInspectTargets.DaemonErrorLogsCommand, UcliErrorInspectTargets.UnityErrorLogsCommand],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Wait for shutdown to complete, start or select a live Editor session, then inspect state before retrying."),
            ],
            relatedCodes: null),

        UcliErrorDescriptorFactory.Create(
            code: EditorLifecycleErrorCodes.EditorUnavailable,
            category: "lifecycle",
            summary: "Unity Editor lifecycle state is unavailable.",
            meaning: "The client could not obtain a reliable lifecycle state from the daemon endpoint, and no recovery evidence was sufficient to classify the Editor as ready or waitable.",
            appliesTo: UnityExecutionCommands,
            possiblePhases: ["readinessWait", "ipcProbe", "daemonRecovery"],
            impliesNotApplied: null,
            mayBeIndeterminate: true,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect: ["status", UcliErrorInspectTargets.DaemonStatusCommand, UcliErrorInspectTargets.DaemonErrorLogsCommand, UcliErrorInspectTargets.UnityErrorLogsCommand],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Inspect daemon status and Unity logs, resolve any visible Unity prompt or project blocker, then retry only after lifecycleState becomes ready."),
            ],
            relatedCodes:
            [
                EditorLifecycleErrorCodes.EditorRecovering,
                EditorLifecycleErrorCodes.EditorModalBlocked,
                EditorLifecycleErrorCodes.EditorShuttingDown,
            ]),
    ];

    private static UcliErrorDescriptor CreateTransient (
        UcliCodeValue code,
        string summary,
        string meaning,
        string phase)
    {
        return UcliErrorDescriptorFactory.Create(
            code: code,
            category: "lifecycle",
            summary: summary,
            meaning: meaning,
            appliesTo: UnityExecutionCommands,
            possiblePhases: [phase],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.WaitThenRetry,
            inspect: ["status", "payload.lifecycleState", UcliErrorInspectTargets.DaemonStatusCommand],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Wait until lifecycleState=ready, then rerun the appropriate command."),
            ],
            relatedCodes: new[]
            {
                EditorLifecycleErrorCodes.EditorBusy,
                EditorLifecycleErrorCodes.EditorCompiling,
                EditorLifecycleErrorCodes.EditorDomainReloading,
                EditorLifecycleErrorCodes.EditorRecovering,
                EditorLifecycleErrorCodes.EditorReimporting,
            }.Where(relatedCode => relatedCode != code).ToArray());
    }
}
