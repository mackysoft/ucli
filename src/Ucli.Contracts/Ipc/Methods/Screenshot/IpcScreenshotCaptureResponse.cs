namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>screenshot.capture</c> IPC success response payload. </summary>
/// <param name="Capture"> The capture metadata observed at the successful pixel-readback boundary. </param>
/// <param name="Staging"> The normalized raw-image staging file produced by Unity. </param>
public sealed record IpcScreenshotCaptureResponse (
    IpcScreenshotCapture Capture,
    IpcScreenshotStagingImage Staging);
