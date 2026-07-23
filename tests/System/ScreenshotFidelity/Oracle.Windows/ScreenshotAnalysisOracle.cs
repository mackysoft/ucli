using System.Drawing.Imaging;

namespace MackySoft.Ucli.ScreenshotFidelityOracle.Windows;

internal static class ScreenshotAnalysisOracle
{
    private const int SchemaVersion = 1;
    private const byte ChangedPixelThreshold = 8;
    private const double FidelityMeanAbsoluteErrorThreshold = 0.5 / 255d;
    private const double FidelityPercentile95AbsoluteErrorThreshold = 2d / 255d;
    private const double FidelityMaximumAbsoluteErrorThreshold = 4d / 255d;
    private const double VariantMeanAbsoluteErrorMinimum = 8d / 255d;
    private const double VariantChangedPixelFractionMinimum = 0.25d;
    private const double MinimumComparablePixelFraction = 0.999d;

    internal static Outcome AnalyzeCurrent (
        string artifactPath,
        string referencePath,
        string confirmationReferencePath)
    {
        try
        {
            return AnalyzeCurrentCore(artifactPath, referencePath, confirmationReferencePath);
        }
        catch (Exception exception)
        {
            return CreateFailure("current", exception);
        }
    }

    internal static Outcome AnalyzeVariants (
        string leftReferencePath,
        string rightReferencePath)
    {
        try
        {
            return AnalyzeVariantsCore(leftReferencePath, rightReferencePath);
        }
        catch (Exception exception)
        {
            return CreateFailure("variants", exception);
        }
    }

    internal static Outcome SelfCheck ()
    {
        try
        {
            PixelImage baseline = PixelImage.CreateSolid(16, 16, 10, 20, 30);
            PixelImage identical = PixelImage.CreateSolid(16, 16, 10, 20, 30);
            PixelImage oneInvalidPixel = baseline.WithPixel(0, 0, 15, 20, 30);
            PixelImage distinctVariant = PixelImage.CreateSolid(16, 16, 50, 60, 70);
            var fullImage = new PixelRectangle(0, 0, baseline.Width, baseline.Height);

            ImageComparison.Metrics identicalMetrics = ImageComparison.Compare(
                baseline,
                fullImage,
                identical,
                fullImage,
                ChangedPixelThreshold);
            ImageComparison.Metrics invalidMetrics = ImageComparison.Compare(
                baseline,
                fullImage,
                oneInvalidPixel,
                fullImage,
                ChangedPixelThreshold);
            ImageComparison.Metrics variantMetrics = ImageComparison.Compare(
                baseline,
                fullImage,
                distinctVariant,
                fullImage,
                ChangedPixelThreshold);

            PixelImage opaqueWindow = PixelImage.CreateSolid(64, 64, 10, 20, 30);
            PixelImage cornerMaskedWindow = opaqueWindow
                .WithPixel(0, 63, 10, 20, 30, alpha: 0)
                .WithPixel(63, 63, 10, 20, 30, alpha: 0);
            PixelImage interiorMaskedWindow = opaqueWindow
                .WithPixel(32, 32, 10, 20, 30, alpha: 0);
            var acceptedMaskFailures = new List<string>();
            ReferenceMaskInspection acceptedMask = InspectReferenceMask(
                acceptedMaskFailures,
                "synthetic corner-masked reference",
                cornerMaskedWindow);
            var rejectedMaskFailures = new List<string>();
            _ = InspectReferenceMask(
                rejectedMaskFailures,
                "synthetic interior-masked reference",
                interiorMaskedWindow);
            var fullWindow = new PixelRectangle(0, 0, opaqueWindow.Width, opaqueWindow.Height);
            ImageComparison.Metrics cornerMaskedMetrics = ImageComparison.Compare(
                opaqueWindow,
                fullWindow,
                cornerMaskedWindow,
                fullWindow,
                ChangedPixelThreshold,
                excludeNonOpaquePixels: true);

            bool acceptsIdenticalImages = PassesFidelityThresholds(identicalMetrics);
            bool rejectsExcessiveMaximumError = !PassesFidelityThresholds(invalidMetrics);
            bool acceptsDistinctVariants = PassesVariantThresholds(variantMetrics);
            bool acceptsCompositorCornerMask = acceptedMaskFailures.Count == 0
                && acceptedMask.ContainsOnlyCornerConnectedPixels
                && PassesFidelityThresholds(cornerMaskedMetrics);
            bool rejectsInteriorTransparency = rejectedMaskFailures.Count != 0;
            bool decodesOpaquePng = VerifyPixelImageDecoder();
            bool passed = acceptsIdenticalImages
                && rejectsExcessiveMaximumError
                && acceptsDistinctVariants
                && acceptsCompositorCornerMask
                && rejectsInteriorTransparency
                && decodesOpaquePng;

            var report = new SelfCheckReport(
                SchemaVersion,
                "self-check",
                passed,
                acceptsIdenticalImages,
                rejectsExcessiveMaximumError,
                acceptsDistinctVariants,
                acceptsCompositorCornerMask,
                rejectsInteriorTransparency,
                decodesOpaquePng,
                identicalMetrics,
                invalidMetrics,
                variantMetrics,
                cornerMaskedMetrics,
                CreateThresholds());
            return new Outcome(passed, report);
        }
        catch (Exception exception)
        {
            return CreateFailure("self-check", exception);
        }
    }

    private static Outcome AnalyzeCurrentCore (
        string artifactPath,
        string referencePath,
        string confirmationReferencePath)
    {
        string artifactFullPath = Path.GetFullPath(artifactPath);
        string referenceFullPath = Path.GetFullPath(referencePath);
        string confirmationReferenceFullPath = Path.GetFullPath(confirmationReferencePath);

        PngInspector.Inspection artifactPng = PngInspector.Inspect(artifactFullPath);
        PixelImage artifact = PixelImage.Load(artifactFullPath);
        PixelImage reference = PixelImage.Load(referenceFullPath);
        PixelImage confirmationReference = PixelImage.Load(confirmationReferenceFullPath);

        var failures = new List<string>();
        if (!artifactPng.HasSrgbChunk)
        {
            failures.Add("The screenshot artifact PNG does not declare the sRGB color space before image data.");
        }

        AddOpacityFailure(failures, "screenshot artifact", artifact);
        ReferenceMaskInspection referenceMask = InspectReferenceMask(
            failures,
            "reference",
            reference);
        ReferenceMaskInspection confirmationReferenceMask = InspectReferenceMask(
            failures,
            "confirmation reference",
            confirmationReference);

        PixelRectangle? referenceCrop = GetBottomCrop(
            failures,
            "reference",
            artifact,
            reference);
        PixelRectangle? confirmationReferenceCrop = GetBottomCrop(
            failures,
            "confirmation reference",
            artifact,
            confirmationReference);
        var artifactRectangle = new PixelRectangle(0, 0, artifact.Width, artifact.Height);

        ImageComparison.Metrics? artifactToReference = null;
        ImageComparison.Metrics? artifactToConfirmationReference = null;
        ImageComparison.Metrics? referenceStability = null;
        if (referenceCrop.HasValue)
        {
            artifactToReference = ImageComparison.Compare(
                artifact,
                artifactRectangle,
                reference,
                referenceCrop.Value,
                ChangedPixelThreshold,
                excludeNonOpaquePixels: true);
            AddFidelityFailures(failures, "artifact versus reference", artifactToReference);
        }

        if (confirmationReferenceCrop.HasValue)
        {
            artifactToConfirmationReference = ImageComparison.Compare(
                artifact,
                artifactRectangle,
                confirmationReference,
                confirmationReferenceCrop.Value,
                ChangedPixelThreshold,
                excludeNonOpaquePixels: true);
            AddFidelityFailures(
                failures,
                "artifact versus confirmation reference",
                artifactToConfirmationReference);
        }

        if (referenceCrop.HasValue && confirmationReferenceCrop.HasValue)
        {
            AddMaskMismatchFailure(
                failures,
                "reference versus confirmation reference",
                reference,
                referenceCrop.Value,
                confirmationReference,
                confirmationReferenceCrop.Value);
            referenceStability = ImageComparison.Compare(
                reference,
                referenceCrop.Value,
                confirmationReference,
                confirmationReferenceCrop.Value,
                ChangedPixelThreshold,
                excludeNonOpaquePixels: true);
            AddFidelityFailures(failures, "reference stability", referenceStability);
        }

        bool passed = failures.Count == 0;
        var report = new CurrentAnalysisReport(
            SchemaVersion,
            "current",
            passed,
            failures,
            new InputImage(artifactFullPath, artifact.Width, artifact.Height, artifact.IsOpaque),
            new InputImage(referenceFullPath, reference.Width, reference.Height, reference.IsOpaque),
            new InputImage(
                confirmationReferenceFullPath,
                confirmationReference.Width,
                confirmationReference.Height,
                confirmationReference.IsOpaque),
            artifactPng,
            referenceCrop,
            confirmationReferenceCrop,
            referenceMask,
            confirmationReferenceMask,
            artifactToReference,
            artifactToConfirmationReference,
            referenceStability,
            CreateThresholds());
        return new Outcome(passed, report);
    }

    private static Outcome AnalyzeVariantsCore (
        string leftReferencePath,
        string rightReferencePath)
    {
        string leftReferenceFullPath = Path.GetFullPath(leftReferencePath);
        string rightReferenceFullPath = Path.GetFullPath(rightReferencePath);
        PixelImage left = PixelImage.Load(leftReferenceFullPath);
        PixelImage right = PixelImage.Load(rightReferenceFullPath);

        var failures = new List<string>();
        ReferenceMaskInspection leftMask = InspectReferenceMask(failures, "left reference", left);
        ReferenceMaskInspection rightMask = InspectReferenceMask(failures, "right reference", right);

        ImageComparison.Metrics? comparison = null;
        if (left.Width != right.Width || left.Height != right.Height)
        {
            failures.Add(
                $"Variant reference dimensions differ: left={left.Width}x{left.Height}, right={right.Width}x{right.Height}.");
        }
        else
        {
            var fullImage = new PixelRectangle(0, 0, left.Width, left.Height);
            AddMaskMismatchFailure(failures, "variant references", left, fullImage, right, fullImage);
            comparison = ImageComparison.Compare(
                left,
                fullImage,
                right,
                fullImage,
                ChangedPixelThreshold,
                excludeNonOpaquePixels: true);
            if (comparison.MeanAbsoluteError < VariantMeanAbsoluteErrorMinimum)
            {
                failures.Add(
                    $"Variant mean absolute error {comparison.MeanAbsoluteError:R} is below the required minimum {VariantMeanAbsoluteErrorMinimum:R}.");
            }

            if (comparison.ChangedPixelFraction < VariantChangedPixelFractionMinimum)
            {
                failures.Add(
                    $"Variant changed-pixel fraction {comparison.ChangedPixelFraction:R} is below the required minimum {VariantChangedPixelFractionMinimum:R}.");
            }
        }

        bool passed = failures.Count == 0;
        var report = new VariantAnalysisReport(
            SchemaVersion,
            "variants",
            passed,
            failures,
            new InputImage(leftReferenceFullPath, left.Width, left.Height, left.IsOpaque),
            new InputImage(rightReferenceFullPath, right.Width, right.Height, right.IsOpaque),
            leftMask,
            rightMask,
            comparison,
            CreateThresholds());
        return new Outcome(passed, report);
    }

    private static PixelRectangle? GetBottomCrop (
        List<string> failures,
        string referenceName,
        PixelImage artifact,
        PixelImage reference)
    {
        if (reference.Width != artifact.Width)
        {
            failures.Add(
                $"The {referenceName} width must equal the artifact width: artifact={artifact.Width}, reference={reference.Width}.");
            return null;
        }

        if (reference.Height < artifact.Height)
        {
            failures.Add(
                $"The {referenceName} height must be at least the artifact height: artifact={artifact.Height}, reference={reference.Height}.");
            return null;
        }

        return new PixelRectangle(0, reference.Height - artifact.Height, artifact.Width, artifact.Height);
    }

    private static void AddOpacityFailure (
        List<string> failures,
        string imageName,
        PixelImage image)
    {
        if (!image.IsOpaque)
        {
            failures.Add($"The {imageName} contains non-opaque pixels.");
        }
    }

    private static ReferenceMaskInspection InspectReferenceMask (
        List<string> failures,
        string imageName,
        PixelImage image)
    {
        int pixelCount = checked(image.Width * image.Height);
        var maskedPixels = new bool[pixelCount];
        int maskedPixelCount = 0;
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                if (image.GetAlpha(x, y) == byte.MaxValue)
                {
                    continue;
                }

                maskedPixels[(y * image.Width) + x] = true;
                maskedPixelCount++;
            }
        }

        double comparablePixelFraction = (pixelCount - maskedPixelCount) / (double)pixelCount;
        bool containsOnlyCornerConnectedPixels = true;
        if (maskedPixelCount > 0)
        {
            var visited = new bool[pixelCount];
            var pending = new Queue<int>();
            EnqueueCorner(0, 0);
            EnqueueCorner(image.Width - 1, 0);
            EnqueueCorner(0, image.Height - 1);
            EnqueueCorner(image.Width - 1, image.Height - 1);

            int visitedMaskedPixelCount = 0;
            while (pending.Count > 0)
            {
                int index = pending.Dequeue();
                int x = index % image.Width;
                int y = index / image.Width;
                visitedMaskedPixelCount++;
                for (int yOffset = -1; yOffset <= 1; yOffset++)
                {
                    for (int xOffset = -1; xOffset <= 1; xOffset++)
                    {
                        if (xOffset == 0 && yOffset == 0)
                        {
                            continue;
                        }

                        int neighborX = x + xOffset;
                        int neighborY = y + yOffset;
                        if (neighborX < 0
                            || neighborX >= image.Width
                            || neighborY < 0
                            || neighborY >= image.Height)
                        {
                            continue;
                        }

                        int neighborIndex = (neighborY * image.Width) + neighborX;
                        if (maskedPixels[neighborIndex] && !visited[neighborIndex])
                        {
                            visited[neighborIndex] = true;
                            pending.Enqueue(neighborIndex);
                        }
                    }
                }
            }

            containsOnlyCornerConnectedPixels = visitedMaskedPixelCount == maskedPixelCount;

            void EnqueueCorner (int x, int y)
            {
                int index = (y * image.Width) + x;
                if (maskedPixels[index] && !visited[index])
                {
                    visited[index] = true;
                    pending.Enqueue(index);
                }
            }
        }

        if (!containsOnlyCornerConnectedPixels)
        {
            failures.Add($"The {imageName} contains non-opaque pixels outside its compositor-clipped corners.");
        }

        if (comparablePixelFraction < MinimumComparablePixelFraction)
        {
            failures.Add(
                $"The {imageName} comparable-pixel fraction {comparablePixelFraction:R} is below "
                + $"the required minimum {MinimumComparablePixelFraction:R}.");
        }

        return new ReferenceMaskInspection(
            maskedPixelCount,
            comparablePixelFraction,
            containsOnlyCornerConnectedPixels);
    }

    private static void AddMaskMismatchFailure (
        List<string> failures,
        string comparisonName,
        PixelImage left,
        PixelRectangle leftRectangle,
        PixelImage right,
        PixelRectangle rightRectangle)
    {
        if (leftRectangle.Width != rightRectangle.Width || leftRectangle.Height != rightRectangle.Height)
        {
            return;
        }

        long mismatchCount = 0;
        for (int y = 0; y < leftRectangle.Height; y++)
        {
            for (int x = 0; x < leftRectangle.Width; x++)
            {
                bool leftIsOpaque = left.GetAlpha(leftRectangle.X + x, leftRectangle.Y + y) == byte.MaxValue;
                bool rightIsOpaque = right.GetAlpha(rightRectangle.X + x, rightRectangle.Y + y) == byte.MaxValue;
                if (leftIsOpaque != rightIsOpaque)
                {
                    mismatchCount++;
                }
            }
        }

        if (mismatchCount != 0)
        {
            failures.Add(
                $"The {comparisonName} compositor corner masks differ at {mismatchCount} pixels.");
        }
    }

    private static void AddFidelityFailures (
        List<string> failures,
        string comparisonName,
        ImageComparison.Metrics metrics)
    {
        if (metrics.ComparedPixelFraction < MinimumComparablePixelFraction)
        {
            failures.Add(
                $"The {comparisonName} comparable-pixel fraction {metrics.ComparedPixelFraction:R} is below "
                + $"the required minimum {MinimumComparablePixelFraction:R}.");
        }

        if (metrics.MeanAbsoluteError > FidelityMeanAbsoluteErrorThreshold)
        {
            failures.Add(
                $"The {comparisonName} mean absolute error {metrics.MeanAbsoluteError:R} exceeds {FidelityMeanAbsoluteErrorThreshold:R}.");
        }

        if (metrics.Percentile95AbsoluteError > FidelityPercentile95AbsoluteErrorThreshold)
        {
            failures.Add(
                $"The {comparisonName} 95th-percentile absolute error {metrics.Percentile95AbsoluteError:R} exceeds {FidelityPercentile95AbsoluteErrorThreshold:R}.");
        }

        if (metrics.MaximumAbsoluteError > FidelityMaximumAbsoluteErrorThreshold)
        {
            failures.Add(
                $"The {comparisonName} maximum absolute error {metrics.MaximumAbsoluteError:R} exceeds {FidelityMaximumAbsoluteErrorThreshold:R}.");
        }
    }

    private static bool PassesFidelityThresholds (ImageComparison.Metrics metrics)
    {
        return metrics.ComparedPixelFraction >= MinimumComparablePixelFraction
            && metrics.MeanAbsoluteError <= FidelityMeanAbsoluteErrorThreshold
            && metrics.Percentile95AbsoluteError <= FidelityPercentile95AbsoluteErrorThreshold
            && metrics.MaximumAbsoluteError <= FidelityMaximumAbsoluteErrorThreshold;
    }

    private static bool PassesVariantThresholds (ImageComparison.Metrics metrics)
    {
        return metrics.ComparedPixelFraction >= MinimumComparablePixelFraction
            && metrics.MeanAbsoluteError >= VariantMeanAbsoluteErrorMinimum
            && metrics.ChangedPixelFraction >= VariantChangedPixelFractionMinimum;
    }

    private static bool VerifyPixelImageDecoder ()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"ucli-screenshot-fidelity-self-check-{Guid.NewGuid():N}.png");
        try
        {
            using (var bitmap = new Bitmap(2, 2, PixelFormat.Format32bppArgb))
            {
                bitmap.SetPixel(0, 0, Color.FromArgb(byte.MaxValue, 10, 20, 30));
                bitmap.SetPixel(1, 0, Color.FromArgb(byte.MaxValue, 40, 50, 60));
                bitmap.SetPixel(0, 1, Color.FromArgb(byte.MaxValue, 70, 80, 90));
                bitmap.SetPixel(1, 1, Color.FromArgb(byte.MaxValue, 100, 110, 120));
                bitmap.Save(path, ImageFormat.Png);
            }

            File.WriteAllBytes(path, AddSrgbChunk(File.ReadAllBytes(path)));
            PngInspector.Inspection inspection = PngInspector.Inspect(path);
            PixelImage decoded = PixelImage.Load(path);
            return inspection.HasSrgbChunk
                && decoded.Width == 2
                && decoded.Height == 2
                && decoded.IsOpaque
                && decoded.HasPixel(0, 0, 10, 20, 30)
                && decoded.HasPixel(1, 0, 40, 50, 60)
                && decoded.HasPixel(0, 1, 70, 80, 90)
                && decoded.HasPixel(1, 1, 100, 110, 120);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static byte[] AddSrgbChunk (byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);

        const int endOfIhdrChunk = 33;
        if (png.Length < endOfIhdrChunk
            || !png.AsSpan(12, 4).SequenceEqual("IHDR"u8))
        {
            throw new InvalidDataException("The self-check encoder did not produce a PNG with an initial IHDR chunk.");
        }

        // This is the standard one-byte sRGB chunk for perceptual rendering intent, including its CRC.
        ReadOnlySpan<byte> srgbChunk =
        [
            0x00, 0x00, 0x00, 0x01,
            0x73, 0x52, 0x47, 0x42,
            0x00,
            0xAE, 0xCE, 0x1C, 0xE9,
        ];
        var taggedPng = new byte[png.Length + srgbChunk.Length];
        png.AsSpan(0, endOfIhdrChunk).CopyTo(taggedPng);
        srgbChunk.CopyTo(taggedPng.AsSpan(endOfIhdrChunk));
        png.AsSpan(endOfIhdrChunk).CopyTo(taggedPng.AsSpan(endOfIhdrChunk + srgbChunk.Length));
        return taggedPng;
    }

    private static Thresholds CreateThresholds ()
    {
        return new Thresholds(
            FidelityMeanAbsoluteErrorThreshold,
            FidelityPercentile95AbsoluteErrorThreshold,
            FidelityMaximumAbsoluteErrorThreshold,
            ChangedPixelThreshold,
            VariantMeanAbsoluteErrorMinimum,
            VariantChangedPixelFractionMinimum,
            MinimumComparablePixelFraction);
    }

    private static Outcome CreateFailure (string analysis, Exception exception)
    {
        var report = new FailureReport(
            SchemaVersion,
            analysis,
            false,
            [$"{exception.GetType().Name}: {exception.Message}"]);
        return new Outcome(false, report);
    }

    internal sealed record Outcome (bool Passed, object Report);

    private sealed record InputImage (
        string Path,
        int Width,
        int Height,
        bool IsOpaque);

    private sealed record Thresholds (
        double FidelityMeanAbsoluteErrorMaximum,
        double FidelityPercentile95AbsoluteErrorMaximum,
        double FidelityMaximumAbsoluteErrorMaximum,
        byte ChangedPixelThresholdExclusive,
        double VariantMeanAbsoluteErrorMinimum,
        double VariantChangedPixelFractionMinimum,
        double ComparablePixelFractionMinimum);

    private sealed record CurrentAnalysisReport (
        int SchemaVersion,
        string Analysis,
        bool Passed,
        IReadOnlyList<string> Failures,
        InputImage Artifact,
        InputImage Reference,
        InputImage ConfirmationReference,
        PngInspector.Inspection ArtifactPng,
        PixelRectangle? ReferenceCrop,
        PixelRectangle? ConfirmationReferenceCrop,
        ReferenceMaskInspection ReferenceMask,
        ReferenceMaskInspection ConfirmationReferenceMask,
        ImageComparison.Metrics? ArtifactToReference,
        ImageComparison.Metrics? ArtifactToConfirmationReference,
        ImageComparison.Metrics? ReferenceStability,
        Thresholds Thresholds);

    private sealed record VariantAnalysisReport (
        int SchemaVersion,
        string Analysis,
        bool Passed,
        IReadOnlyList<string> Failures,
        InputImage LeftReference,
        InputImage RightReference,
        ReferenceMaskInspection LeftReferenceMask,
        ReferenceMaskInspection RightReferenceMask,
        ImageComparison.Metrics? Comparison,
        Thresholds Thresholds);

    private sealed record ReferenceMaskInspection (
        int MaskedPixelCount,
        double ComparablePixelFraction,
        bool ContainsOnlyCornerConnectedPixels);

    private sealed record SelfCheckReport (
        int SchemaVersion,
        string Analysis,
        bool Passed,
        bool AcceptsIdenticalImages,
        bool RejectsExcessiveMaximumError,
        bool AcceptsDistinctVariants,
        bool AcceptsCompositorCornerMask,
        bool RejectsInteriorTransparency,
        bool DecodesOpaquePng,
        ImageComparison.Metrics IdenticalMetrics,
        ImageComparison.Metrics InvalidMetrics,
        ImageComparison.Metrics VariantMetrics,
        ImageComparison.Metrics CornerMaskedMetrics,
        Thresholds Thresholds);

    private sealed record FailureReport (
        int SchemaVersion,
        string Analysis,
        bool Passed,
        IReadOnlyList<string> Failures);
}
