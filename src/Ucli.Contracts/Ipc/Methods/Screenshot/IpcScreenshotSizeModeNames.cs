namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines screenshot capture size-mode literals. </summary>
public static class IpcScreenshotSizeModeNames
{
    /// <summary> Gets the literal for capture at the current target-surface size. </summary>
    public const string CurrentSurface = "currentSurface";

    /// <summary> Gets the literal for capture at an explicitly requested GameView resolution. </summary>
    public const string RequestedResolution = "requestedResolution";
}
