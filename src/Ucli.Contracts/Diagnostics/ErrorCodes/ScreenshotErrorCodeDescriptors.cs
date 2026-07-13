namespace MackySoft.Ucli.Contracts;

internal static class ScreenshotErrorCodeDescriptors
{
    private static readonly UcliCommand[] ScreenshotCommands =
    [
        UcliCommandIds.ScreenshotGame,
        UcliCommandIds.ScreenshotScene,
    ];

    public static IReadOnlyList<UcliErrorDescriptor> All { get; } =
    [
        UcliErrorDescriptorFactory.Create(
            code: ScreenshotErrorCodes.ScreenshotRequiresGuiSession,
            category: "screenshot",
            summary: "Screenshot capture requires a registered GUI Editor session.",
            meaning: "The command cannot capture a GameView or SceneView surface because no compatible GUI Editor daemon session is registered for the project.",
            appliesTo: ScreenshotCommands,
            possiblePhases: ["modeDecision", "daemonSessionResolution"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect:
            [
                "errors[].code",
                "errors[].message",
                UcliErrorInspectTargets.DaemonStatusCommand,
                UcliErrorInspectTargets.DaemonListCommand,
            ],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Start or attach a GUI Editor daemon session for the project, then rerun the screenshot command."),
            ],
            relatedCodes: [DaemonErrorCodes.DaemonSessionNotAvailable]),

        UcliErrorDescriptorFactory.Create(
            code: ScreenshotErrorCodes.ScreenshotRequestedSizeUnsupported,
            category: "screenshot",
            summary: "The requested GameView screenshot size is unsupported.",
            meaning: "Unity could not apply and capture the exact requested GameView width and height without substituting another resolution.",
            appliesTo: [UcliCommandIds.ScreenshotGame],
            possiblePhases: ["capturePreflight", "resolutionTransaction", "repaintWait"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["errors[].code", "errors[].message"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Choose a supported positive width and height, or omit both options to capture the current GameView surface size."),
            ],
            relatedCodes: [UcliCoreErrorCodes.InvalidArgument]),

        UcliErrorDescriptorFactory.Create(
            code: ScreenshotErrorCodes.ScreenshotCaptureUnsupported,
            category: "screenshot",
            summary: "The target surface cannot be captured faithfully in the current environment.",
            meaning: "uCLI could not establish a supported capture path that preserves the target surface orientation, dimensions, and visual appearance, so it did not commit a PNG artifact.",
            appliesTo: ScreenshotCommands,
            possiblePhases: ["capturePreflight", "surfaceCapture", "pixelNormalization", "artifactValidation"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect:
            [
                "errors[].code",
                "errors[].message",
                UcliErrorInspectTargets.UnityErrorLogsCommand,
            ],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Inspect the reported unsupported surface or graphics condition and Unity logs; retry only after the capture environment changes."),
            ],
            relatedCodes: null),
    ];
}
