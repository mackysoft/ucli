using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Presentation;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a structurally valid <c>screenshot.capture</c> IPC request payload. </summary>
public sealed record IpcScreenshotCaptureRequest
{
    /// <summary> Initializes one screenshot capture request identified independently of its transport envelope. </summary>
    /// <param name="CaptureId"> The non-empty identifier shared with the response and staging layout. </param>
    /// <param name="Target"> The screenshot target. </param>
    /// <param name="RequestedDimensions"> The requested GameView dimensions, or <see langword="null" /> for the current surface size. </param>
    /// <exception cref="ArgumentException"> Thrown when the capture identifier or requested-size combination is invalid. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="Target" /> is not a contract value. </exception>
    [JsonConstructor]
    public IpcScreenshotCaptureRequest (
        Guid CaptureId,
        IpcScreenshotTarget Target,
        PixelDimensions? RequestedDimensions)
    {
        if (CaptureId == Guid.Empty)
        {
            throw new ArgumentException("Capture id must not be empty.", nameof(CaptureId));
        }

        if (!TextVocabulary.IsDefined(Target))
        {
            throw new ArgumentOutOfRangeException(nameof(Target), Target, "Screenshot target must be specified.");
        }

        if (RequestedDimensions is not null)
        {
            if (Target != IpcScreenshotTarget.Game)
            {
                throw new ArgumentException(
                    "Requested dimensions are supported only for the game screenshot target.",
                    nameof(Target));
            }

            if (!IpcScreenshotCaptureLimits.TryCalculateRgba8Layout(
                RequestedDimensions.Width,
                RequestedDimensions.Height,
                out _,
                out _))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(RequestedDimensions),
                    "Requested screenshot dimensions exceed the supported normalized RGBA8 layout.");
            }
        }

        this.CaptureId = CaptureId;
        this.Target = Target;
        this.RequestedDimensions = RequestedDimensions;
    }

    /// <summary> Gets the identifier shared with the response and staging layout. </summary>
    public Guid CaptureId { get; }

    /// <summary> Gets the screenshot target. </summary>
    public IpcScreenshotTarget Target { get; }

    /// <summary> Gets the requested GameView dimensions, or <see langword="null" /> for the current surface size. </summary>
    public PixelDimensions? RequestedDimensions { get; }
}
