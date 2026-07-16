using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Describes one normalized raw screenshot staging image without exposing its host-local path. </summary>
public sealed record IpcScreenshotStagingImage
{
    /// <summary> Initializes one internally consistent normalized raw-image layout. </summary>
    /// <exception cref="ArgumentException"> Thrown when dimensions, row stride, and total byte count do not describe the same image. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when a required contract literal or image dimension is invalid. </exception>
    [JsonConstructor]
    public IpcScreenshotStagingImage (
        int Width,
        int Height,
        IpcScreenshotPixelFormat PixelFormat,
        IpcScreenshotRowOrder RowOrder,
        int RowStrideBytes,
        long SizeBytes)
    {
        if (PixelFormat != IpcScreenshotPixelFormat.Rgba8Srgb)
        {
            throw new ArgumentOutOfRangeException(nameof(PixelFormat), PixelFormat, "Screenshot pixel format must be specified.");
        }

        if (RowOrder != IpcScreenshotRowOrder.TopDown)
        {
            throw new ArgumentOutOfRangeException(nameof(RowOrder), RowOrder, "Screenshot row order must be specified.");
        }

        if (!IpcScreenshotCaptureLimits.TryCalculateRgba8Layout(
            Width,
            Height,
            out var expectedRowStrideBytes,
            out var expectedSizeBytes))
        {
            throw new ArgumentOutOfRangeException(
                nameof(Width),
                "Screenshot staging dimensions exceed the supported normalized RGBA8 layout.");
        }

        if (RowStrideBytes != expectedRowStrideBytes)
        {
            throw new ArgumentException(
                $"Screenshot row stride must equal {expectedRowStrideBytes} bytes for the supplied dimensions.",
                nameof(RowStrideBytes));
        }

        if (SizeBytes != expectedSizeBytes)
        {
            throw new ArgumentException(
                $"Screenshot size must equal {expectedSizeBytes} bytes for the supplied dimensions.",
                nameof(SizeBytes));
        }

        this.Width = Width;
        this.Height = Height;
        this.PixelFormat = PixelFormat;
        this.RowOrder = RowOrder;
        this.RowStrideBytes = RowStrideBytes;
        this.SizeBytes = SizeBytes;
    }

    /// <summary> Gets the staged image width in pixels. </summary>
    public int Width { get; }

    /// <summary> Gets the staged image height in pixels. </summary>
    public int Height { get; }

    /// <summary> Gets the raw pixel format. </summary>
    public IpcScreenshotPixelFormat PixelFormat { get; }

    /// <summary> Gets the raw row order. </summary>
    public IpcScreenshotRowOrder RowOrder { get; }

    /// <summary> Gets the byte count occupied by one row. </summary>
    public int RowStrideBytes { get; }

    /// <summary> Gets the total byte count written to the staging file. </summary>
    public long SizeBytes { get; }
}
