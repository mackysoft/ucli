using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Contracts;

/// <summary> References finalized artifact bytes by an absolute artifact URI. </summary>
public sealed record UriArtifactRef : ArtifactRef
{
    /// <summary> Initializes a URI-located artifact reference. </summary>
    /// <param name="kind"> The product-defined artifact kind. </param>
    /// <param name="mediaType"> The media type of the finalized bytes. </param>
    /// <param name="uri"> The absolute artifact URI. </param>
    /// <param name="digest"> The SHA-256 digest computed from the finalized bytes. </param>
    /// <param name="sizeBytes"> The exact number of finalized bytes. </param>
    /// <param name="createdAtUtc"> The UTC time at which the finalized bytes were published. </param>
    /// <exception cref="ArgumentNullException"> Thrown when a required reference value is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="createdAtUtc" /> is not UTC. </exception>
    [JsonConstructor]
    public UriArtifactRef (
        ArtifactKind kind,
        ArtifactMediaType mediaType,
        ArtifactUri uri,
        Sha256Digest digest,
        ulong sizeBytes,
        DateTimeOffset createdAtUtc)
        : base(kind, mediaType, digest, sizeBytes, createdAtUtc)
    {
        Uri = uri ?? throw new ArgumentNullException(nameof(uri));
    }

    /// <inheritdoc />
    private protected override ArtifactLocationKind LocationKindCore =>
        ArtifactLocationKind.Uri;

    /// <summary> Gets the absolute artifact URI. </summary>
    [JsonInclude]
    [JsonRequired]
    public ArtifactUri Uri { get; private init; }
}
