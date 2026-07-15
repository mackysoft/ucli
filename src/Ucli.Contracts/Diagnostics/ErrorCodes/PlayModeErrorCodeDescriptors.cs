namespace MackySoft.Ucli.Contracts;

internal static class PlayModeErrorCodeDescriptors
{
    private static readonly UcliCommand[] PlayModeMutationCommands =
    [
        UcliCommandIds.Plan,
        UcliCommandIds.Call,
        UcliCommandIds.Eval,
    ];

    private static readonly UcliCommand[] PlayModeLifecycleCommands =
    [
        UcliCommandIds.PlayStatus,
        UcliCommandIds.PlayEnter,
        UcliCommandIds.PlayExit,
    ];

    private static readonly UcliCommand[] PlayModeTransitionCommands =
    [
        UcliCommandIds.PlayEnter,
        UcliCommandIds.PlayExit,
    ];

    private static readonly UcliCommand[] PlayModeMutationLifecycleCommands =
    [
        UcliCommandIds.PlayEnter,
        UcliCommandIds.PlayExit,
    ];

    private static readonly UcliCommand[] PlayModeEnterCommand =
    [
        UcliCommandIds.PlayEnter,
    ];

    private static readonly UcliCommand[] PlayModeExitCommand =
    [
        UcliCommandIds.PlayExit,
    ];

    public static IReadOnlyList<UcliErrorDescriptor> All { get; } =
    [
        UcliErrorDescriptorFactory.Create(
            code: PlayModeErrorCodes.PlayModeNotActive,
            category: "playMode",
            summary: "A Play Mode mutation was requested while Play Mode is not active.",
            meaning: "The operation requires a running Play Mode session but the target Unity Editor is not in Play Mode.",
            appliesTo: PlayModeMutationCommands,
            possiblePhases: ["operationAuthorization", "unityExecution"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClass.No,
            inspect: ["status", "payload.playMode", "payload.opResults"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Enter Play Mode with a GUI Editor session, then rerun the operation if it is still required."),
            ],
            relatedCodes: [PlayModeErrorCodes.PlayModeRequiresGuiEditor]),

        UcliErrorDescriptorFactory.Create(
            code: PlayModeErrorCodes.PlayModeRequiresGuiEditor,
            category: "playMode",
            summary: "Play Mode work requires a GUI Editor session.",
            meaning: "The operation depends on Play Mode state that cannot be provided by a batchmode or oneshot Editor session.",
            appliesTo:
            [
                UcliCommandIds.Plan,
                UcliCommandIds.Call,
                UcliCommandIds.Eval,
                UcliCommandIds.PlayStatus,
                UcliCommandIds.PlayEnter,
                UcliCommandIds.PlayExit,
            ],
            possiblePhases: ["modeDecision", "operationAuthorization", "playModeControl"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClass.No,
            inspect:
            [
                "status",
                "payload.daemon",
                "payload.playMode",
                "payload.snapshot.playMode",
                "payload.transition.before.playMode",
                "payload.transition.observed.playMode",
            ],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Start or attach a GUI Editor daemon session, then rerun the command."),
            ],
            relatedCodes:
            [
                PlayModeErrorCodes.PlayModeNotActive,
                DaemonErrorCodes.DaemonEditorModeMismatch,
            ]),

        UcliErrorDescriptorFactory.Create(
            code: PlayModeErrorCodes.PlayModePersistenceForbidden,
            category: "playMode",
            summary: "A Play Mode mutation attempted forbidden persistence.",
            meaning: "The operation would persist project or asset state from Play Mode where uCLI does not allow that side effect.",
            appliesTo: PlayModeMutationCommands,
            possiblePhases: ["operationAuthorization", "unityExecution", "readPostcondition"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClass.No,
            inspect: ["payload.opResults", "payload.opResults[].touched", "payload.readPostcondition"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Change the operation so it does not persist Play Mode state, or exit Play Mode and replan."),
            ],
            relatedCodes:
            [
                OperationAuthorizationErrorCodes.OperationNotAllowed,
                EditorLifecycleErrorCodes.EditorPlaymode,
            ]),

        CreateLifecycleControl(
            PlayModeErrorCodes.PlayModeSessionNotAvailable,
            PlayModeLifecycleCommands,
            "A GUI daemon session is not available for Play Mode control.",
            "The command requires an existing registered GUI daemon session and will not start or attach a Unity Editor process implicitly.",
            ["sessionResolution"],
            true,
            false,
            UcliErrorRetryClass.No,
            [
                "payload.daemonStatus",
                "payload.editorMode",
                "payload.snapshot.editorMode",
                "payload.transition.before.editorMode",
                "payload.transition.observed.editorMode",
                UcliErrorInspectTargets.DaemonStatusCommand,
            ],
            "Start or attach a GUI daemon session with daemon start --editorMode gui, then rerun the Play Mode command.",
            [PlayModeErrorCodes.PlayModeRequiresGuiEditor, DaemonErrorCodes.DaemonEndpointNotRegistered]),

        CreateLifecycleControl(
            PlayModeErrorCodes.PlayModeTransitionTimeout,
            PlayModeTransitionCommands,
            "A Play Mode transition timed out.",
            "Unity responded to the request but did not reach the requested Play Mode state before the transition timeout expired.",
            ["playModeControl", "transitionWait"],
            false,
            true,
            UcliErrorRetryClass.ContextDependent,
            [
                "payload.transition.before.playMode",
                "payload.transition.observed",
                "payload.transition.observed.playMode",
                "payload.transition.applicationState",
                UcliErrorInspectTargets.UnityErrorLogsCommand,
            ],
            "Inspect the latest playMode snapshot and Unity logs before deciding whether retrying the transition is safe.",
            [IpcTransportErrorCodes.IpcTimeout, PlayModeErrorCodes.PlayModeTransitionBlocked]),

        CreateLifecycleControl(
            PlayModeErrorCodes.PlayModeTransitionBlocked,
            PlayModeTransitionCommands,
            "A Play Mode transition was blocked.",
            "The requested Play Mode transition could not proceed because Unity reported a blocking Editor state other than a transition timeout.",
            ["playModeControl", "transitionWait"],
            true,
            false,
            UcliErrorRetryClass.ContextDependent,
            [
                "payload.transition.before.lifecycleState",
                "payload.transition.before.blockingReason",
                "payload.transition.before.playMode",
                "payload.transition.observed.lifecycleState",
                "payload.transition.observed.blockingReason",
                "payload.transition.observed.playMode",
                UcliErrorInspectTargets.UnityErrorLogsCommand,
            ],
            "Resolve the reported Editor blocker, then rerun the Play Mode command if the transition is still required.",
            [EditorLifecycleErrorCodes.EditorBusy, EditorLifecycleErrorCodes.EditorModalBlocked, PlayModeErrorCodes.PlayModeStateUnknown]),

        CreateLifecycleControl(
            PlayModeErrorCodes.PlayModeAlreadyChanging,
            PlayModeMutationLifecycleCommands,
            "Play Mode is already changing.",
            "The requested Play Mode transition was rejected because Unity is already entering or exiting Play Mode.",
            ["playModeControl"],
            true,
            false,
            UcliErrorRetryClass.WaitThenRetry,
            ["payload.transition.before.playMode", "payload.transition.observed.playMode"],
            "Wait for the current Play Mode transition to finish, then rerun the command if needed.",
            [PlayModeErrorCodes.PlayModeTransitionTimeout]),

        CreateLifecycleControl(
            PlayModeErrorCodes.PlayModeEnterRejected,
            PlayModeEnterCommand,
            "Unity rejected Play Mode enter.",
            "Unity did not accept the request to enter Play Mode.",
            ["playModeControl"],
            true,
            false,
            UcliErrorRetryClass.ContextDependent,
            [
                "payload.transition.before.lifecycleState",
                "payload.transition.before.playMode",
                "payload.transition.observed.lifecycleState",
                "payload.transition.observed.playMode",
                UcliErrorInspectTargets.UnityErrorLogsCommand,
            ],
            "Inspect the lifecycle snapshot and Unity logs, resolve the rejection cause, then retry only if entering Play Mode is still required.",
            [PlayModeErrorCodes.PlayModeTransitionBlocked, PlayModeErrorCodes.PlayModeStateUnknown]),

        CreateLifecycleControl(
            PlayModeErrorCodes.PlayModeExitRejected,
            PlayModeExitCommand,
            "Unity rejected Play Mode exit.",
            "Unity did not accept the request to exit Play Mode.",
            ["playModeControl"],
            true,
            false,
            UcliErrorRetryClass.ContextDependent,
            [
                "payload.transition.before.lifecycleState",
                "payload.transition.before.playMode",
                "payload.transition.observed.lifecycleState",
                "payload.transition.observed.playMode",
                UcliErrorInspectTargets.UnityErrorLogsCommand,
            ],
            "Inspect the lifecycle snapshot and Unity logs, resolve the rejection cause, then retry only if exiting Play Mode is still required.",
            [PlayModeErrorCodes.PlayModeTransitionBlocked, PlayModeErrorCodes.PlayModeStateUnknown]),

        CreateLifecycleControl(
            PlayModeErrorCodes.PlayModeStateUnknown,
            PlayModeLifecycleCommands,
            "Play Mode state is unknown.",
            "The Play Mode subsystem snapshot could not be classified into a stable stopped, entering, playing, or exiting state.",
            ["playModeControl", "lifecycleObservation"],
            null,
            true,
            UcliErrorRetryClass.ContextDependent,
            [
                "payload.snapshot.lifecycleState",
                "payload.snapshot.playMode",
                "payload.transition.before.lifecycleState",
                "payload.transition.before.playMode",
                "payload.transition.observed.lifecycleState",
                "payload.transition.observed.playMode",
                UcliErrorInspectTargets.UnityErrorLogsCommand,
            ],
            "Inspect daemon status and Unity logs, wait for a classified lifecycle state, then retry only after the state is understood.",
            [EditorLifecycleErrorCodes.EditorUnavailable, PlayModeErrorCodes.PlayModeTransitionBlocked]),
    ];

    private static UcliErrorDescriptor CreateLifecycleControl (
        UcliCode code,
        IReadOnlyList<UcliCommand> appliesTo,
        string summary,
        string meaning,
        IReadOnlyList<string> possiblePhases,
        bool? impliesNotApplied,
        bool mayBeIndeterminate,
        UcliErrorRetryClass safeToRetry,
        IReadOnlyList<string> inspect,
        string nextAction,
        IReadOnlyList<UcliCode> relatedCodes)
    {
        return UcliErrorDescriptorFactory.Create(
            code: code,
            category: "playMode",
            summary: summary,
            meaning: meaning,
            appliesTo: appliesTo,
            possiblePhases: possiblePhases,
            impliesNotApplied: impliesNotApplied,
            mayBeIndeterminate: mayBeIndeterminate,
            safeToRetry: safeToRetry,
            inspect: inspect,
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: nextAction),
            ],
            relatedCodes: relatedCodes);
    }
}
