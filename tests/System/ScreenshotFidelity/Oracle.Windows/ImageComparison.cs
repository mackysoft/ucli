namespace MackySoft.Ucli.ScreenshotFidelityOracle.Windows;

internal static class ImageComparison
{
    internal static Metrics Compare (
        PixelImage left,
        PixelRectangle leftRectangle,
        PixelImage right,
        PixelRectangle rightRectangle,
        byte changedPixelThreshold,
        bool excludeNonOpaquePixels = false)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        ValidateRectangle(left, leftRectangle, nameof(leftRectangle));
        ValidateRectangle(right, rightRectangle, nameof(rightRectangle));

        if (leftRectangle.Width != rightRectangle.Width || leftRectangle.Height != rightRectangle.Height)
        {
            throw new ArgumentException("Comparison rectangles must have the same dimensions.");
        }

        var histogram = new long[256];
        long absoluteErrorSum = 0;
        int maximumAbsoluteError = 0;
        long changedPixelCount = 0;
        long comparedPixelCount = 0;
        ReadOnlySpan<byte> leftPixels = left.Pixels;
        ReadOnlySpan<byte> rightPixels = right.Pixels;

        for (int y = 0; y < leftRectangle.Height; y++)
        {
            for (int x = 0; x < leftRectangle.Width; x++)
            {
                int leftOffset = checked(((((leftRectangle.Y + y) * left.Width) + leftRectangle.X + x) * 4));
                int rightOffset = checked(((((rightRectangle.Y + y) * right.Width) + rightRectangle.X + x) * 4));
                if (excludeNonOpaquePixels
                    && (leftPixels[leftOffset + 3] != byte.MaxValue
                        || rightPixels[rightOffset + 3] != byte.MaxValue))
                {
                    continue;
                }

                comparedPixelCount++;
                int pixelMaximum = 0;
                for (int channel = 0; channel < 3; channel++)
                {
                    int difference = Math.Abs(leftPixels[leftOffset + channel] - rightPixels[rightOffset + channel]);
                    histogram[difference]++;
                    absoluteErrorSum += difference;
                    maximumAbsoluteError = Math.Max(maximumAbsoluteError, difference);
                    pixelMaximum = Math.Max(pixelMaximum, difference);
                }

                if (pixelMaximum > changedPixelThreshold)
                {
                    changedPixelCount++;
                }
            }
        }

        long pixelCount = checked((long)leftRectangle.Width * leftRectangle.Height);
        if (comparedPixelCount == 0)
        {
            throw new InvalidDataException("The comparison does not contain any comparable pixels.");
        }

        long channelCount = checked(comparedPixelCount * 3);
        long percentileRank = checked(((channelCount * 95) + 99) / 100);
        long cumulative = 0;
        int percentile95AbsoluteError = 0;
        for (int error = 0; error < histogram.Length; error++)
        {
            cumulative += histogram[error];
            if (cumulative >= percentileRank)
            {
                percentile95AbsoluteError = error;
                break;
            }
        }

        return new Metrics(
            leftRectangle.Width,
            leftRectangle.Height,
            pixelCount,
            comparedPixelCount,
            comparedPixelCount / (double)pixelCount,
            absoluteErrorSum / (channelCount * 255d),
            percentile95AbsoluteError / 255d,
            maximumAbsoluteError / 255d,
            changedPixelThreshold,
            changedPixelCount,
            changedPixelCount / (double)comparedPixelCount);
    }

    private static void ValidateRectangle (
        PixelImage image,
        PixelRectangle rectangle,
        string parameterName)
    {
        if (rectangle.X < 0
            || rectangle.Y < 0
            || rectangle.Width <= 0
            || rectangle.Height <= 0
            || rectangle.X > image.Width - rectangle.Width
            || rectangle.Y > image.Height - rectangle.Height)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The comparison rectangle must be inside the image.");
        }
    }

    internal sealed record Metrics (
        int Width,
        int Height,
        long PixelCount,
        long ComparedPixelCount,
        double ComparedPixelFraction,
        double MeanAbsoluteError,
        double Percentile95AbsoluteError,
        double MaximumAbsoluteError,
        byte ChangedPixelThresholdExclusive,
        long ChangedPixelCount,
        double ChangedPixelFraction);
}
