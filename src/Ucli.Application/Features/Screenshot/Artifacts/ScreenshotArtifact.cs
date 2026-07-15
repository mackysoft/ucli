using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Features.Screenshot.Artifacts;

/// <summary> Identifies one committed screenshot PNG artifact. </summary>
internal sealed record ScreenshotArtifact
{
    /// <summary> Initializes one committed screenshot artifact. </summary>
    public ScreenshotArtifact (
        string path,
        Sha256Digest digest,
        long sizeBytes,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeBytes);
        if (createdAtUtc == default || createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Screenshot artifact creation time must be a non-default UTC value.", nameof(createdAtUtc));
        }

        Path = path;
        Digest = digest ?? throw new ArgumentNullException(nameof(digest));
        SizeBytes = sizeBytes;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary> Gets the slash-separated repository-relative artifact path. </summary>
    public string Path { get; }

    /// <summary> Gets the SHA-256 digest of the committed PNG bytes. </summary>
    public Sha256Digest Digest { get; }

    /// <summary> Gets the committed PNG byte count. </summary>
    public long SizeBytes { get; }

    /// <summary> Gets the UTC time at which the final artifact was committed. </summary>
    public DateTimeOffset CreatedAtUtc { get; }
}
