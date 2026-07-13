namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines allocation limits enforced independently at screenshot IPC boundaries. </summary>
public static class IpcScreenshotCaptureLimits
{
    /// <summary> Gets the maximum supported width or height in pixels. </summary>
    public const int MaximumDimension = 16384;

    /// <summary> Gets the maximum supported uncompressed RGBA8 image size in bytes. </summary>
    public const long MaximumRawImageBytes = 128L * 1024L * 1024L;
}
