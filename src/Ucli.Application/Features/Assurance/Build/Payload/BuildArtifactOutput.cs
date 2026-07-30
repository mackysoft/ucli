using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents build output artifact accounting. </summary>
internal sealed record BuildArtifactOutput
{
    public BuildArtifactOutput (
        Sha256Digest ManifestDigest,
        int EntryCount,
        int FileCount,
        long TotalBytes)
    {
        this.ManifestDigest = ManifestDigest ?? throw new ArgumentNullException(nameof(ManifestDigest));
        this.EntryCount = EntryCount;
        this.FileCount = FileCount;
        this.TotalBytes = TotalBytes;
    }

    public BuildArtifactKind ManifestRef { get; } = BuildArtifactKind.BuildOutputManifest;

    public Sha256Digest ManifestDigest { get; }

    public int EntryCount { get; }

    public int FileCount { get; }

    public long TotalBytes { get; }
}
