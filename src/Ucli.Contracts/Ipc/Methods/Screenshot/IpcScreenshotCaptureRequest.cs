namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>screenshot.capture</c> IPC request payload. </summary>
/// <param name="Target"> The screenshot target. </param>
/// <param name="RequestedWidth"> The requested GameView width, or <see langword="null" /> for the current surface size. </param>
/// <param name="RequestedHeight"> The requested GameView height, or <see langword="null" /> for the current surface size. </param>
/// <param name="StagingPath"> The host-owned absolute path where Unity writes normalized raw image bytes. </param>
/// <param name="TimeoutMilliseconds"> The remaining server-side capture timeout in milliseconds. </param>
public sealed record IpcScreenshotCaptureRequest (
    IpcScreenshotTarget Target,
    int? RequestedWidth,
    int? RequestedHeight,
    string StagingPath,
    int TimeoutMilliseconds);
