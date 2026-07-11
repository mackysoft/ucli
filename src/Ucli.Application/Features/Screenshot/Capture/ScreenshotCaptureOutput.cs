namespace MackySoft.Ucli.Application.Features.Screenshot.Capture;

/// <summary> Represents one successfully captured screenshot and its committed PNG artifact. </summary>
/// <param name="Project"> The resolved Unity project identity. </param>
/// <param name="Target"> The captured presentation surface. </param>
/// <param name="RequestedWidth"> The requested width, or <see langword="null" /> for the current surface. </param>
/// <param name="RequestedHeight"> The requested height, or <see langword="null" /> for the current surface. </param>
/// <param name="Width"> The captured image width. </param>
/// <param name="Height"> The captured image height. </param>
/// <param name="ColorSpace"> The Unity project active color space at capture time. </param>
/// <param name="LifecycleStateAtCapture"> The editor lifecycle state at capture time. </param>
/// <param name="CompileStateAtCapture"> The editor compile state at capture time. </param>
/// <param name="DomainReloadGeneration"> The domain-reload generation at capture time. </param>
/// <param name="PlayModeState"> The Play Mode state at capture time. </param>
/// <param name="ArtifactPath"> The repository-relative PNG artifact path. </param>
/// <param name="ArtifactDigest"> The lowercase SHA-256 digest of the PNG bytes. </param>
/// <param name="ArtifactSizeBytes"> The PNG artifact size in bytes. </param>
/// <param name="ArtifactCreatedAtUtc"> The PNG artifact creation timestamp. </param>
internal sealed record ScreenshotCaptureOutput (
    ProjectIdentityInfo Project,
    ScreenshotCaptureTarget Target,
    int? RequestedWidth,
    int? RequestedHeight,
    int Width,
    int Height,
    string ColorSpace,
    string LifecycleStateAtCapture,
    string CompileStateAtCapture,
    long DomainReloadGeneration,
    string PlayModeState,
    string ArtifactPath,
    string ArtifactDigest,
    long ArtifactSizeBytes,
    DateTimeOffset ArtifactCreatedAtUtc);
