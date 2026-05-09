namespace MackySoft.Ucli.Contracts;

internal static class PlayModeErrorCodeDescriptors
{
    public static IReadOnlyList<UcliErrorCodeDescriptor> All { get; } =
    [
        UcliErrorCodeDescriptorFactory.Create(
            code: PlayModeErrorCodes.PlayModeNotActive,
            category: "playMode",
            summary: "A Play Mode mutation was requested while Play Mode is not active.",
            meaning: "The operation requires a running Play Mode session but the target Unity Editor is not in Play Mode.",
            appliesTo: [UcliCommandIds.Plan, UcliCommandIds.Call],
            possiblePhases: ["operationAuthorization", "unityExecution"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["status", "payload.playMode", "payload.opResults"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Enter Play Mode with a GUI Editor session, then rerun the operation if it is still required."),
            ],
            relatedCodes: [PlayModeErrorCodes.PlayModeRequiresGuiEditor]),

        UcliErrorCodeDescriptorFactory.Create(
            code: PlayModeErrorCodes.PlayModeRequiresGuiEditor,
            category: "playMode",
            summary: "A Play Mode mutation requires a GUI Editor session.",
            meaning: "The operation depends on Play Mode state that cannot be provided by a batchmode or oneshot Editor session.",
            appliesTo: [UcliCommandIds.Plan, UcliCommandIds.Call],
            possiblePhases: ["modeDecision", "operationAuthorization"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["status", "payload.daemon", "payload.playMode"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Use a GUI Editor daemon session that is in Play Mode."),
            ],
            relatedCodes:
            [
                PlayModeErrorCodes.PlayModeNotActive,
                DaemonErrorCodes.DaemonEditorModeMismatch,
            ]),

        UcliErrorCodeDescriptorFactory.Create(
            code: PlayModeErrorCodes.PlayModePersistenceForbidden,
            category: "playMode",
            summary: "A Play Mode mutation attempted forbidden persistence.",
            meaning: "The operation would persist project or asset state from Play Mode where uCLI does not allow that side effect.",
            appliesTo: [UcliCommandIds.Plan, UcliCommandIds.Call],
            possiblePhases: ["operationAuthorization", "unityExecution", "readPostcondition"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
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
    ];
}
