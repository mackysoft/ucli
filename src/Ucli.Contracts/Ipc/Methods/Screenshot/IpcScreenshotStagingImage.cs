namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Describes one normalized raw screenshot staging file. </summary>
/// <param name="Path"> The absolute staging path that was supplied by the host request. </param>
/// <param name="PixelFormat"> The raw pixel format. </param>
/// <param name="RowOrder"> The raw row order. </param>
/// <param name="RowStrideBytes"> The byte count occupied by one row. </param>
/// <param name="SizeBytes"> The total byte count written to the staging file. </param>
public sealed record IpcScreenshotStagingImage (
    string Path,
    IpcScreenshotPixelFormat PixelFormat,
    IpcScreenshotRowOrder RowOrder,
    int RowStrideBytes,
    long SizeBytes);
