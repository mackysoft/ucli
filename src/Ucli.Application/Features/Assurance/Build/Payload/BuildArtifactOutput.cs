namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents build output artifact accounting. </summary>
internal sealed record BuildArtifactOutput (
    string ManifestRef,
    string ManifestDigest,
    int EntryCount,
    int FileCount,
    long TotalBytes);
