namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines allocation limits enforced independently at screenshot IPC boundaries. </summary>
public static class IpcScreenshotCaptureLimits
{
    /// <summary> Gets the byte count occupied by one normalized RGBA8 pixel. </summary>
    public const int Rgba8BytesPerPixel = 4;

    /// <summary> Gets the maximum supported width or height in pixels. </summary>
    public const int MaximumDimension = 16384;

    /// <summary> Gets the maximum supported uncompressed RGBA8 image size in bytes. </summary>
    public const long MaximumRawImageBytes = 128L * 1024L * 1024L;

    /// <summary> Tries to calculate the normalized RGBA8 layout for supported positive dimensions. </summary>
    /// <param name="width"> The image width in pixels. </param>
    /// <param name="height"> The image height in pixels. </param>
    /// <param name="rowStrideBytes"> The byte count occupied by one row when successful. </param>
    /// <param name="sizeBytes"> The total image byte count when successful. </param>
    /// <returns> <see langword="true" /> when the dimensions and calculated layout are within the IPC limits. </returns>
    public static bool TryCalculateRgba8Layout (
        int width,
        int height,
        out int rowStrideBytes,
        out long sizeBytes)
    {
        rowStrideBytes = 0;
        sizeBytes = 0;
        if (width <= 0
            || height <= 0
            || width > MaximumDimension
            || height > MaximumDimension)
        {
            return false;
        }

        try
        {
            rowStrideBytes = checked(width * Rgba8BytesPerPixel);
            sizeBytes = checked((long)rowStrideBytes * height);
            if (sizeBytes <= MaximumRawImageBytes)
            {
                return true;
            }

            rowStrideBytes = 0;
            sizeBytes = 0;
            return false;
        }
        catch (OverflowException)
        {
            rowStrideBytes = 0;
            sizeBytes = 0;
            return false;
        }
    }
}
