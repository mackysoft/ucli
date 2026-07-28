using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Contracts;

/// <summary> References one finalized immutable byte sequence published by a uCLI feature. </summary>
public abstract record ArtifactRef
{
    /// <summary> Initializes the common identity and measurement of finalized artifact bytes. </summary>
    /// <param name="kind"> The product-defined artifact kind. </param>
    /// <param name="mediaType"> The media type of the finalized bytes. </param>
    /// <param name="digest"> The SHA-256 digest computed from the finalized bytes. </param>
    /// <param name="sizeBytes"> The exact number of finalized bytes. </param>
    /// <param name="createdAtUtc"> The UTC time at which the finalized bytes were published. </param>
    /// <exception cref="ArgumentNullException"> Thrown when a required reference value is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="createdAtUtc" /> is not UTC. </exception>
    protected ArtifactRef (
        ArtifactKind kind,
        ArtifactMediaType mediaType,
        Sha256Digest digest,
        ulong sizeBytes,
        DateTimeOffset createdAtUtc)
    {
        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Artifact publication time must use the UTC offset.",
                nameof(createdAtUtc));
        }

        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        MediaType = mediaType ?? throw new ArgumentNullException(nameof(mediaType));
        Digest = digest ?? throw new ArgumentNullException(nameof(digest));
        SizeBytes = sizeBytes;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary> Gets the product-defined meaning of the artifact. </summary>
    [JsonInclude]
    [JsonRequired]
    public ArtifactKind Kind { get; private init; }

    /// <summary> Gets the media type of the finalized byte sequence. </summary>
    [JsonInclude]
    [JsonRequired]
    public ArtifactMediaType MediaType { get; private init; }

    /// <summary> Gets the locator branch carried by this reference. </summary>
    [JsonIgnore]
    public ArtifactLocationKind LocationKind => LocationKindCore;

    private protected abstract ArtifactLocationKind LocationKindCore { get; }

    /// <summary> Gets the SHA-256 digest computed from the finalized bytes. </summary>
    [JsonInclude]
    [JsonRequired]
    public Sha256Digest Digest { get; private init; }

    /// <summary> Gets the exact number of finalized bytes. </summary>
    [JsonInclude]
    [JsonRequired]
    public ulong SizeBytes { get; private init; }

    /// <summary> Gets the UTC publication time of the finalized bytes. </summary>
    [JsonInclude]
    [JsonRequired]
    [JsonConverter(typeof(ArtifactPublicationTimeJsonConverter))]
    [Length(
        ArtifactPublicationTimeJsonConverter.CanonicalTextLength,
        ArtifactPublicationTimeJsonConverter.CanonicalTextLength)]
    [Pattern(ArtifactPublicationTimeJsonConverter.CanonicalTextPattern)]
    public DateTimeOffset CreatedAtUtc { get; private init; }
}
