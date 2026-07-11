namespace MackySoft.Ucli.Application.Features.Screenshot.Artifacts;

/// <summary> Defines one normalized raw screenshot staging image to commit. </summary>
/// <param name="Paths"> The host-prepared capture paths. </param>
/// <param name="ReturnedStagingPath"> The staging path returned by the Unity capture response. </param>
/// <param name="Width"> The captured image width in pixels. </param>
/// <param name="Height"> The captured image height in pixels. </param>
/// <param name="PixelFormat"> The raw staging pixel-format literal. </param>
/// <param name="RowOrder"> The raw staging row-order literal. </param>
/// <param name="RowStrideBytes"> The raw byte count occupied by one image row. </param>
/// <param name="SizeBytes"> The total raw staging byte count reported by Unity. </param>
internal sealed record ScreenshotArtifactCommitRequest (
    ScreenshotArtifactPaths Paths,
    string ReturnedStagingPath,
    int Width,
    int Height,
    string PixelFormat,
    string RowOrder,
    int RowStrideBytes,
    long SizeBytes);
