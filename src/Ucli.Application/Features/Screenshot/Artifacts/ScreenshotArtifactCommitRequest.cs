using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Screenshot.Artifacts;

/// <summary> Defines one Unity screenshot staging result to commit into host-owned storage. </summary>
internal sealed class ScreenshotArtifactCommitRequest
{
    /// <summary> Initializes a screenshot artifact commit request. </summary>
    public ScreenshotArtifactCommitRequest (
        ScreenshotArtifactPaths paths,
        int width,
        int height,
        IpcScreenshotStagingImage staging)
    {
        Paths = paths ?? throw new ArgumentNullException(nameof(paths));
        Width = width;
        Height = height;
        Staging = staging ?? throw new ArgumentNullException(nameof(staging));
    }

    /// <summary> Gets the host-prepared capture paths. </summary>
    public ScreenshotArtifactPaths Paths { get; }

    /// <summary> Gets the captured image width in pixels. </summary>
    public int Width { get; }

    /// <summary> Gets the captured image height in pixels. </summary>
    public int Height { get; }

    /// <summary> Gets the raw staging image metadata returned by Unity. </summary>
    public IpcScreenshotStagingImage Staging { get; }
}
