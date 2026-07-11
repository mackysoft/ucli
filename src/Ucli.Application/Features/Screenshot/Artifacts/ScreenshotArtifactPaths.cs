namespace MackySoft.Ucli.Application.Features.Screenshot.Artifacts;

/// <summary> Defines the host-owned paths for one screenshot capture. </summary>
/// <param name="RepositoryRoot"> The repository root that owns local uCLI storage. </param>
/// <param name="ProjectFingerprint"> The project fingerprint that scopes the capture. </param>
/// <param name="CaptureId"> The capture identifier. </param>
/// <param name="ArtifactDirectory"> The capture-scoped final artifact directory. </param>
/// <param name="PngPath"> The final PNG artifact path. </param>
/// <param name="StagingDirectory"> The capture-scoped work directory. </param>
/// <param name="RawStagingPath"> The host-selected raw RGBA staging file path supplied to Unity. </param>
internal sealed record ScreenshotArtifactPaths (
    string RepositoryRoot,
    string ProjectFingerprint,
    string CaptureId,
    string ArtifactDirectory,
    string PngPath,
    string StagingDirectory,
    string RawStagingPath);
