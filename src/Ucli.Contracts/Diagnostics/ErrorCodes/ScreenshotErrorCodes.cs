namespace MackySoft.Ucli.Contracts;

/// <summary> Defines machine-readable error codes used by screenshot capture workflows. </summary>
public static class ScreenshotErrorCodes
{
    /// <summary> Gets the error code emitted when screenshot capture requires a registered GUI Editor session. </summary>
    public static readonly UcliCode ScreenshotRequiresGuiSession = new("SCREENSHOT_REQUIRES_GUI_SESSION");

    /// <summary> Gets the error code emitted when GameView cannot capture the exact requested resolution. </summary>
    public static readonly UcliCode ScreenshotRequestedSizeUnsupported = new("SCREENSHOT_REQUESTED_SIZE_UNSUPPORTED");

    /// <summary> Gets the error code emitted when the current Unity environment cannot provide a faithful target-surface capture. </summary>
    public static readonly UcliCode ScreenshotCaptureUnsupported = new("SCREENSHOT_CAPTURE_UNSUPPORTED");

    /// <summary> Gets all screenshot error codes. </summary>
    public static IReadOnlyList<UcliCode> All { get; } =
    [
        ScreenshotRequiresGuiSession,
        ScreenshotRequestedSizeUnsupported,
        ScreenshotCaptureUnsupported,
    ];
}
