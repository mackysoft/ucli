namespace MackySoft.Ucli.Contracts;

internal static class GameViewRecordingErrorCodeDescriptors
{
    private static readonly UcliCommand[] AllRecordingCommands =
    [
        UcliCommandIds.RecordingStart,
        UcliCommandIds.RecordingStatus,
        UcliCommandIds.RecordingStop,
    ];

    private static readonly UcliCommand[] StartCommand = [UcliCommandIds.RecordingStart];

    private static readonly UcliCommand[] LookupCommands =
    [
        UcliCommandIds.RecordingStatus,
        UcliCommandIds.RecordingStop,
    ];

    public static IReadOnlyList<UcliErrorDescriptor> All { get; } =
    [
        CreatePreflight(
            GameViewRecordingErrorCodes.Unavailable,
            "Unity Recorder is not available for this project.",
            "The resolved project dependencies do not contain the Unity Recorder package required to start GameView recording.",
            "Install a supported Unity Recorder package version, allow Unity to resolve packages, then retry."),
        CreatePreflight(
            GameViewRecordingErrorCodes.RecorderUnsupported,
            "The resolved Unity Recorder version is unsupported.",
            "The resolved Unity Recorder package version is outside the version range supported by the bundled uCLI adapter.",
            "Resolve a Recorder package version in the supported range reported by recording status."),
        CreatePreflight(
            GameViewRecordingErrorCodes.AdapterFaulted,
            "The Recorder adapter is missing for a supported package.",
            "A supported Recorder package is resolved, but the corresponding uCLI adapter was not registered in the connected Unity runtime.",
            "Inspect Unity compilation errors and restart the GUI Editor after correcting the adapter registration failure."),
        CreatePreflight(
            GameViewRecordingErrorCodes.RequiresGuiSession,
            "GameView recording requires a registered GUI Editor session.",
            "No compatible GUI Editor daemon session is registered for the resolved project.",
            "Start or attach a GUI Editor daemon session for the project, then retry."),
        CreatePreflight(
            GameViewRecordingErrorCodes.RequiresPlayMode,
            "GameView recording requires stable Play Mode.",
            "The connected Unity Editor is not in stable Play Mode, so a recording interval cannot begin.",
            "Enter Play Mode, wait until it is stable, then retry."),
        CreatePreflight(
            GameViewRecordingErrorCodes.PlayModeTransitioning,
            "GameView recording cannot start during a Play Mode transition.",
            "Unity is entering or exiting Play Mode and cannot fix a stable GameView recording target.",
            "Wait for the Play Mode transition to complete, then retry."),
        CreatePreflight(
            GameViewRecordingErrorCodes.EditorPaused,
            "GameView recording cannot start while the Editor is paused.",
            "The connected Unity Editor is paused and does not satisfy recording admission.",
            "Resume the Unity Editor, then retry."),
        CreatePreflight(
            GameViewRecordingErrorCodes.RequestedSizeUnsupported,
            "The requested GameView recording size is unsupported.",
            "The runtime could not apply and verify the exact requested even width and height without substitution.",
            "Choose a resolution within the limits reported by recording status, then retry."),
        CreatePreflight(
            GameViewRecordingErrorCodes.EncoderUnsupported,
            "The requested recording profile is unsupported.",
            "The current runtime cannot provide the requested integer frame rate with the fixed MP4 H.264 recording profile.",
            "Choose a frame rate within the reported limits or use a runtime listed by the adapter compatibility table."),
        Create(
            GameViewRecordingErrorCodes.IdConflict,
            "The recording identifier is bound to another request.",
            "The supplied recording id already identifies a recording with a different normalized request digest.",
            StartCommand,
            ["startRegistration"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            UcliErrorRetryClass.No,
            "Reuse the identifier only with its original request, or start the new request with a new identifier."),
        Create(
            GameViewRecordingErrorCodes.Conflict,
            "Another recording holds the runtime exclusion.",
            "A different active or recovering GameView recording owns the shared GameView state in this Unity runtime.",
            StartCommand,
            ["recordingExclusion"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            UcliErrorRetryClass.ContextDependent,
            "Inspect or stop the existing recording, wait for its cleanup to finish, then retry."),
        Create(
            GameViewRecordingErrorCodes.NotFound,
            "The specified recording was not found.",
            "The durable recording registry has no entry for the supplied recording identifier.",
            LookupCommands,
            ["recordingLookup"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            UcliErrorRetryClass.No,
            "Use the recording id returned by start, or omit it from status to inspect the current non-terminal recording."),
        Create(
            GameViewRecordingErrorCodes.MonitoringTimeout,
            "Recording monitoring reached its deadline.",
            "The caller stopped waiting before it observed a terminal result; the returned durable checkpoint reports the latest state available to that call.",
            StartCommand,
            ["terminalMonitoring"],
            impliesNotApplied: false,
            mayBeIndeterminate: false,
            UcliErrorRetryClass.ContextDependent,
            "Use the returned recording id with recording status or recording stop."),
        Create(
            GameViewRecordingErrorCodes.BindingMismatch,
            "The recording start binding no longer identifies the connected Unity runtime.",
            "The connected Unity process, recording runtime, or Editor generation differs from the facts fixed before the recording start was dispatched.",
            AllRecordingCommands,
            ["recordingDispatch"],
            impliesNotApplied: true,
            mayBeIndeterminate: true,
            UcliErrorRetryClass.No,
            "Inspect the recording terminal result; start a new recording only after the prior recording has reached a terminal state."),
        Create(
            GameViewRecordingErrorCodes.DispatchDeadlineExceeded,
            "The recording request reached Unity after its fixed dispatch deadline.",
            "Unity rejected the request before invoking Recorder because its immutable dispatch deadline had elapsed.",
            AllRecordingCommands,
            ["recordingDispatch"],
            impliesNotApplied: true,
            mayBeIndeterminate: true,
            UcliErrorRetryClass.No,
            "Inspect the recording terminal result; a new recording requires a new identifier and start binding."),
        CreateTerminal(
            GameViewRecordingErrorCodes.FinalizationFailed,
            "The recording video could not be finalized.",
            "Stopping, MP4 finalization, content validation, or immutable video publication failed.",
            mayBeIndeterminate: false,
            "Inspect the recording diagnostics and partial-output artifact when present."),
        CreateTerminal(
            GameViewRecordingErrorCodes.CleanupFailed,
            "Recording cleanup failed.",
            "At least one owned Unity state could not be restored or one acquired recording resource could not be released.",
            mayBeIndeterminate: false,
            "Inspect the cleanup artifact and restore the reported Unity state or resource before another recording."),
        CreateTerminal(
            GameViewRecordingErrorCodes.Interrupted,
            "The Unity runtime interrupted the recording.",
            "Play Mode exit, domain reload, Editor exit, or adapter unload ended the requested recording interval and initiated recovery.",
            mayBeIndeterminate: true,
            "Inspect the terminal record, cleanup artifact, and recovered partial output before retrying."),
    ];

    private static UcliErrorDescriptor CreatePreflight (
        UcliCode code,
        string summary,
        string meaning,
        string nextAction)
    {
        return Create(
            code,
            summary,
            meaning,
            StartCommand,
            ["recordingAdmission"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            UcliErrorRetryClass.ContextDependent,
            nextAction);
    }

    private static UcliErrorDescriptor CreateTerminal (
        UcliCode code,
        string summary,
        string meaning,
        bool mayBeIndeterminate,
        string nextAction)
    {
        return Create(
            code,
            summary,
            meaning,
            AllRecordingCommands,
            ["recordingRecovery", "terminalFinalization"],
            impliesNotApplied: false,
            mayBeIndeterminate,
            UcliErrorRetryClass.No,
            nextAction);
    }

    private static UcliErrorDescriptor Create (
        UcliCode code,
        string summary,
        string meaning,
        IReadOnlyList<UcliCommand> appliesTo,
        IReadOnlyList<string> possiblePhases,
        bool? impliesNotApplied,
        bool mayBeIndeterminate,
        UcliErrorRetryClass retryClass,
        string nextAction)
    {
        return UcliErrorDescriptorFactory.Create(
            code,
            category: "recording",
            summary,
            meaning,
            appliesTo,
            possiblePhases,
            impliesNotApplied,
            mayBeIndeterminate,
            retryClass,
            inspect:
            [
                "errors[].code",
                "errors[].message",
                "payload.executionRef",
                "payload.diagnostics",
            ],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(When: null, Action: nextAction),
            ],
            relatedCodes: null);
    }
}
