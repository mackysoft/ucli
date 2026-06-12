namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents the generated player output manifest accounting summary. </summary>
/// <param name="ManifestDigest"> The lowercase SHA-256 digest of canonical manifest content. </param>
/// <param name="FileCount"> The number of regular files included in the manifest. </param>
/// <param name="TotalBytes"> The total byte count for manifest files. </param>
internal sealed record BuildOutputManifestSummary (
    string ManifestDigest,
    int FileCount,
    long TotalBytes);
