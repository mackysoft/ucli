using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a successful <c>screenshot.capture</c> result without host-local path data. </summary>
public sealed record IpcScreenshotCaptureResponse
{
    /// <summary> Initializes one capture response correlated to the request capture identifier. </summary>
    /// <exception cref="ArgumentException"> Thrown when the capture identifier is empty or capture and staging dimensions differ. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when capture or staging metadata is <see langword="null" />. </exception>
    [JsonConstructor]
    public IpcScreenshotCaptureResponse (
        Guid CaptureId,
        IpcScreenshotCapture Capture,
        IpcScreenshotStagingImage Staging)
    {
        if (CaptureId == Guid.Empty)
        {
            throw new ArgumentException("Capture id must not be empty.", nameof(CaptureId));
        }

        if (Capture == null)
        {
            throw new ArgumentNullException(nameof(Capture));
        }

        if (Staging == null)
        {
            throw new ArgumentNullException(nameof(Staging));
        }

        if (Capture.Width != Staging.Width || Capture.Height != Staging.Height)
        {
            throw new ArgumentException(
                "Capture and staging dimensions must match.",
                nameof(Staging));
        }

        this.CaptureId = CaptureId;
        this.Capture = Capture;
        this.Staging = Staging;
    }

    /// <summary> Gets the identifier shared with the originating request and staging layout. </summary>
    public Guid CaptureId { get; }

    /// <summary> Gets metadata observed at the successful pixel-readback boundary. </summary>
    public IpcScreenshotCapture Capture { get; }

    /// <summary> Gets the normalized raw-image metadata produced by Unity. </summary>
    public IpcScreenshotStagingImage Staging { get; }
}
