using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Contracts;

/// <summary> References finalized artifact bytes by a repository-relative path. </summary>
public sealed record PathArtifactRef : ArtifactRef
{
    /// <summary> Initializes a path-located artifact reference. </summary>
    /// <param name="kind"> The product-defined artifact kind. </param>
    /// <param name="mediaType"> The media type of the finalized bytes. </param>
    /// <param name="path"> The portable path relative to the repository root. </param>
    /// <param name="digest"> The SHA-256 digest computed from the finalized bytes. </param>
    /// <param name="sizeBytes"> The exact number of finalized bytes. </param>
    /// <param name="createdAtUtc"> The UTC time at which the finalized bytes were published. </param>
    /// <exception cref="ArgumentNullException"> Thrown when a required reference value is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="createdAtUtc" /> is not UTC. </exception>
    [JsonConstructor]
    public PathArtifactRef (
        ArtifactKind kind,
        ArtifactMediaType mediaType,
        ArtifactPath path,
        Sha256Digest digest,
        ulong sizeBytes,
        DateTimeOffset createdAtUtc)
        : base(kind, mediaType, digest, sizeBytes, createdAtUtc)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
    }

    /// <inheritdoc />
    private protected override ArtifactLocationKind LocationKindCore =>
        ArtifactLocationKind.Path;

    /// <summary> Gets the portable artifact path relative to the repository root. </summary>
    [JsonInclude]
    [JsonRequired]
    public ArtifactPath Path { get; private init; }
}
