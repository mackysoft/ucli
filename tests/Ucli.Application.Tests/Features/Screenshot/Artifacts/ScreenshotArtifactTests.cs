using MackySoft.Ucli.Application.Features.Screenshot.Artifacts;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Tests.Screenshot;

public sealed class ScreenshotArtifactTests
{
    private static readonly Sha256Digest Digest = Sha256Digest.Parse(new string('a', 64));

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenDigestIsNull_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new ScreenshotArtifact(
            "artifacts/screenshot.png",
            null!,
            1,
            DateTimeOffset.UnixEpoch));

        Assert.Equal("digest", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenSizeIsNotPositive_ThrowsArgumentOutOfRangeException ()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new ScreenshotArtifact(
            "artifacts/screenshot.png",
            Digest,
            0,
            DateTimeOffset.UnixEpoch));

        Assert.Equal("sizeBytes", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenCreationTimeIsNotUtc_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new ScreenshotArtifact(
            "artifacts/screenshot.png",
            Digest,
            1,
            new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.FromHours(9))));

        Assert.Equal("createdAtUtc", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenPathIsNotPortable_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new ScreenshotArtifact(
            @"artifacts\screenshot.png",
            Digest,
            1,
            DateTimeOffset.UnixEpoch));

        Assert.Equal("path", exception.ParamName);
    }
}
