using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MackySoft.Ucli.ScreenshotFidelityOracle.Windows;

internal sealed class PixelImage
{
    private const long MaximumPixelCount = 100_000_000;
    private readonly byte[] pixels;

    private PixelImage (int width, int height, byte[] pixels)
    {
        Width = width;
        Height = height;
        this.pixels = pixels;
    }

    internal int Width { get; }

    internal int Height { get; }

    internal ReadOnlySpan<byte> Pixels => pixels;

    internal bool IsOpaque
    {
        get
        {
            for (int index = 3; index < pixels.Length; index += 4)
            {
                if (pixels[index] != byte.MaxValue)
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal static PixelImage Load (string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Image file was not found: {fullPath}", fullPath);
        }

        using var source = new Bitmap(fullPath);
        ValidateDimensions(source.Width, source.Height, fullPath);

        using var canonical = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(canonical))
        {
            graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            graphics.DrawImage(
                source,
                new Rectangle(0, 0, source.Width, source.Height),
                0,
                0,
                source.Width,
                source.Height,
                GraphicsUnit.Pixel);
        }

        int byteCount = checked(source.Width * source.Height * 4);
        var pixels = new byte[byteCount];
        var bounds = new Rectangle(0, 0, canonical.Width, canonical.Height);
        BitmapData data = canonical.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int rowByteCount = checked(canonical.Width * 4);
            var row = new byte[Math.Abs(data.Stride)];
            for (int y = 0; y < canonical.Height; y++)
            {
                IntPtr rowAddress = IntPtr.Add(data.Scan0, checked(y * data.Stride));
                Marshal.Copy(rowAddress, row, 0, row.Length);
                int destinationOffset = checked(y * rowByteCount);
                for (int x = 0; x < canonical.Width; x++)
                {
                    int sourceOffset = x * 4;
                    int pixelOffset = destinationOffset + sourceOffset;
                    pixels[pixelOffset] = row[sourceOffset + 2];
                    pixels[pixelOffset + 1] = row[sourceOffset + 1];
                    pixels[pixelOffset + 2] = row[sourceOffset];
                    pixels[pixelOffset + 3] = row[sourceOffset + 3];
                }
            }
        }
        finally
        {
            canonical.UnlockBits(data);
        }

        return new PixelImage(source.Width, source.Height, pixels);
    }

    internal static PixelImage CreateSolid (
        int width,
        int height,
        byte red,
        byte green,
        byte blue,
        byte alpha = byte.MaxValue)
    {
        ValidateDimensions(width, height, "synthetic image");

        var pixels = new byte[checked(width * height * 4)];
        for (int index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = red;
            pixels[index + 1] = green;
            pixels[index + 2] = blue;
            pixels[index + 3] = alpha;
        }

        return new PixelImage(width, height, pixels);
    }

    internal PixelImage WithPixel (
        int x,
        int y,
        byte red,
        byte green,
        byte blue,
        byte alpha = byte.MaxValue)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "The pixel must be inside the image.");
        }

        byte[] changedPixels = (byte[])pixels.Clone();
        int offset = checked(((y * Width) + x) * 4);
        changedPixels[offset] = red;
        changedPixels[offset + 1] = green;
        changedPixels[offset + 2] = blue;
        changedPixels[offset + 3] = alpha;
        return new PixelImage(Width, Height, changedPixels);
    }

    internal bool HasPixel (
        int x,
        int y,
        byte red,
        byte green,
        byte blue,
        byte alpha = byte.MaxValue)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return false;
        }

        int offset = checked(((y * Width) + x) * 4);
        return pixels[offset] == red
            && pixels[offset + 1] == green
            && pixels[offset + 2] == blue
            && pixels[offset + 3] == alpha;
    }

    internal byte GetAlpha (int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "The pixel must be inside the image.");
        }

        int offset = checked(((y * Width) + x) * 4);
        return pixels[offset + 3];
    }

    private static void ValidateDimensions (int width, int height, string source)
    {
        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException($"Image dimensions must be positive for {source}.");
        }

        long pixelCount = checked((long)width * height);
        if (pixelCount > MaximumPixelCount)
        {
            throw new InvalidDataException(
                $"Image exceeds the {MaximumPixelCount} pixel safety limit for {source}: {width}x{height}.");
        }
    }
}
