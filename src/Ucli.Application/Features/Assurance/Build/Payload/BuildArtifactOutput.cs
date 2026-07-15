using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents build output artifact accounting. </summary>
internal sealed record BuildArtifactOutput (
    BuildArtifactKind ManifestRef,
    Sha256Digest ManifestDigest,
    int EntryCount,
    int FileCount,
    long TotalBytes);
