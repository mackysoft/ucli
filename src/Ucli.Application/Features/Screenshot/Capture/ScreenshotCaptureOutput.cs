using MackySoft.Ucli.Application.Features.Screenshot.Artifacts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Screenshot.Capture;

/// <summary> Represents one successfully captured screenshot and its committed PNG artifact. </summary>
internal sealed record ScreenshotCaptureOutput
{
    /// <summary> Initializes one successfully captured screenshot output. </summary>
    /// <param name="project"> The resolved Unity project identity. </param>
    /// <param name="capture"> The capture metadata observed by Unity. </param>
    /// <param name="artifact"> The committed PNG artifact. </param>
    public ScreenshotCaptureOutput (
        ProjectIdentityInfo project,
        IpcScreenshotCapture capture,
        ScreenshotArtifact artifact)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        Capture = capture ?? throw new ArgumentNullException(nameof(capture));
        Artifact = artifact ?? throw new ArgumentNullException(nameof(artifact));
    }

    /// <summary> Gets the resolved Unity project identity. </summary>
    public ProjectIdentityInfo Project { get; }

    /// <summary> Gets the capture metadata observed by Unity. </summary>
    public IpcScreenshotCapture Capture { get; }

    /// <summary> Gets the committed PNG artifact. </summary>
    public ScreenshotArtifact Artifact { get; }
}
