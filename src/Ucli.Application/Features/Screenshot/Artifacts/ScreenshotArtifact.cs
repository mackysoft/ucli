namespace MackySoft.Ucli.Application.Features.Screenshot.Artifacts;

/// <summary> Identifies one committed screenshot PNG artifact. </summary>
/// <param name="Path"> The slash-separated repository-relative artifact path. </param>
/// <param name="Digest"> The lowercase SHA-256 digest of the committed PNG bytes. </param>
/// <param name="SizeBytes"> The committed PNG byte count. </param>
/// <param name="CreatedAtUtc"> The UTC time at which the final artifact was committed. </param>
internal sealed record ScreenshotArtifact (
    string Path,
    string Digest,
    long SizeBytes,
    DateTimeOffset CreatedAtUtc);
