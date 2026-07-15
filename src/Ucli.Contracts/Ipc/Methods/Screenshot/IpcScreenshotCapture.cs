using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents internally consistent screenshot capture metadata observed by Unity. </summary>
public sealed record IpcScreenshotCapture
{
    /// <summary> Initializes screenshot capture metadata observed at the successful pixel-readback boundary. </summary>
    /// <exception cref="ArgumentException"> Thrown when the target, size mode, requested size, and captured size are inconsistent. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="State" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when a required contract literal or captured dimension is invalid. </exception>
    [JsonConstructor]
    public IpcScreenshotCapture (
        IpcScreenshotTarget Target,
        IpcScreenshotSizeMode SizeMode,
        int? RequestedWidth,
        int? RequestedHeight,
        int Width,
        int Height,
        IpcScreenshotColorSpace ColorSpace,
        UnityEditorStateSnapshot State)
    {
        if (!ContractLiteralCodec.IsDefined(Target))
        {
            throw new ArgumentOutOfRangeException(nameof(Target), Target, "Screenshot target must be specified.");
        }

        if (!ContractLiteralCodec.IsDefined(SizeMode))
        {
            throw new ArgumentOutOfRangeException(nameof(SizeMode), SizeMode, "Screenshot size mode must be specified.");
        }

        if (!ContractLiteralCodec.IsDefined(ColorSpace))
        {
            throw new ArgumentOutOfRangeException(nameof(ColorSpace), ColorSpace, "Screenshot color space must be specified.");
        }

        if (!IpcScreenshotCaptureLimits.TryCalculateRgba8Layout(Width, Height, out _, out _))
        {
            throw new ArgumentOutOfRangeException(
                nameof(Width),
                "Captured screenshot dimensions exceed the supported normalized RGBA8 layout.");
        }

        var hasRequestedWidth = RequestedWidth.HasValue;
        if (hasRequestedWidth != RequestedHeight.HasValue)
        {
            throw new ArgumentException(
                "Requested width and height must be omitted together or specified together.",
                nameof(RequestedWidth));
        }

        switch (SizeMode)
        {
            case IpcScreenshotSizeMode.CurrentSurface when hasRequestedWidth:
                throw new ArgumentException(
                    "Current-surface capture metadata must not contain requested dimensions.",
                    nameof(RequestedWidth));
            case IpcScreenshotSizeMode.CurrentSurface:
                break;
            case IpcScreenshotSizeMode.RequestedResolution when !hasRequestedWidth:
                throw new ArgumentException(
                    "Requested-resolution capture metadata must contain requested dimensions.",
                    nameof(RequestedWidth));
            case IpcScreenshotSizeMode.RequestedResolution:
                if (Target != IpcScreenshotTarget.Game)
                {
                    throw new ArgumentException(
                        "Requested-resolution capture metadata is valid only for the game screenshot target.",
                        nameof(Target));
                }

                if (Width != RequestedWidth!.Value || Height != RequestedHeight!.Value)
                {
                    throw new ArgumentException(
                        "Captured dimensions must match requested-resolution dimensions.",
                        nameof(Width));
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(SizeMode),
                    SizeMode,
                    "Screenshot size mode is not supported by the capture metadata contract.");
        }

        this.Target = Target;
        this.SizeMode = SizeMode;
        this.RequestedWidth = RequestedWidth;
        this.RequestedHeight = RequestedHeight;
        this.Width = Width;
        this.Height = Height;
        this.ColorSpace = ColorSpace;
        this.State = State ?? throw new ArgumentNullException(nameof(State));
    }

    /// <summary> Gets the screenshot target. </summary>
    public IpcScreenshotTarget Target { get; }

    /// <summary> Gets the rule used to determine the captured dimensions. </summary>
    public IpcScreenshotSizeMode SizeMode { get; }

    /// <summary> Gets the requested GameView width, or <see langword="null" /> when omitted. </summary>
    public int? RequestedWidth { get; }

    /// <summary> Gets the requested GameView height, or <see langword="null" /> when omitted. </summary>
    public int? RequestedHeight { get; }

    /// <summary> Gets the captured image width in pixels. </summary>
    public int Width { get; }

    /// <summary> Gets the captured image height in pixels. </summary>
    public int Height { get; }

    /// <summary> Gets the active Unity project color space at capture time. </summary>
    public IpcScreenshotColorSpace ColorSpace { get; }

    /// <summary> Gets the comparable Unity Editor state at capture time. </summary>
    public UnityEditorStateSnapshot State { get; }
}
