namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents build output artifact accounting. </summary>
internal sealed record BuildArtifactOutput (
    string RootPath,
    string ManifestRef,
    string ManifestDigest,
    int FileCount,
    long TotalBytes);
