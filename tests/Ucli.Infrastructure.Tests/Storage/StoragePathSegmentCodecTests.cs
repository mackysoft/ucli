using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Storage;

public sealed class StoragePathSegmentCodecTests
{
    private const string Sha256Hex =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private const string Sha256Segment =
        "04hkaps9lf6uu0938ljojaudts0i6hb7h6lsrro14d2mf2dbpnng";

    private const string GuidSegment = "008i4cq4alj7f24platspnfevs";

    private static readonly Guid GuidValue =
        Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

    [Fact]
    [Trait("Size", "Small")]
    public void EncodeSha256Digest_ReturnsKnownLowercaseBase32HexVector ()
    {
        var digest = Sha256Digest.Parse(Sha256Hex);

        var segment = StoragePathSegmentCodec.EncodeSha256Digest(digest);

        Assert.Equal(Sha256Segment, segment);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EncodeSha256Digest_WithAllBitsSet_PreservesTrailingBitOrder ()
    {
        var digest = Sha256Digest.Parse(new string('f', 64));

        var segment = StoragePathSegmentCodec.EncodeSha256Digest(digest);

        Assert.Equal("vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvg", segment);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsEncodedSha256Digest_WithCanonicalSegment_ReturnsTrue ()
    {
        Assert.True(StoragePathSegmentCodec.IsEncodedSha256Digest(Sha256Segment));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("04HKAPS9LF6UU0938LJOJAUDTS0I6HB7H6LSRRO14D2MF2DBPNNG")]
    [InlineData("vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv")]
    [InlineData("04hkaps9lf6uu0938ljojaudts0i6hb7h6lsrro14d2mf2dbpnn")]
    [InlineData("04hkaps9lf6uu0938ljojaudts0i6hb7h6lsrro14d2mf2dbpnn*")]
    [InlineData("")]
    public void IsEncodedSha256Digest_WithNonCanonicalSegment_ReturnsFalse (string segment)
    {
        Assert.False(StoragePathSegmentCodec.IsEncodedSha256Digest(segment));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EncodeGuid_UsesCanonicalTextByteOrder ()
    {
        var segment = StoragePathSegmentCodec.EncodeGuid(GuidValue, nameof(GuidValue));

        Assert.Equal(GuidSegment, segment);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EncodeGuid_WithAllBitsSet_PreservesSignedFieldBits ()
    {
        var segment = StoragePathSegmentCodec.EncodeGuid(
            Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            "value");

        Assert.Equal("vvvvvvvvvvvvvvvvvvvvvvvvvs", segment);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecodeGuid_WithAllBitsSet_RestoresSignedGuidFields ()
    {
        var decoded = StoragePathSegmentCodec.TryDecodeNonEmptyGuid(
            "vvvvvvvvvvvvvvvvvvvvvvvvvs",
            out var value);

        Assert.True(decoded);
        Assert.Equal(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecodeGuid_WithCanonicalSegment_RestoresContractValue ()
    {
        var decoded = StoragePathSegmentCodec.TryDecodeNonEmptyGuid(GuidSegment, out var value);

        Assert.True(decoded);
        Assert.Equal(GuidValue, value);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("008I4cq4alj7f24platspnfevs")]
    [InlineData("008i4cq4alj7f24platspnfevt")]
    [InlineData("008i4cq4alj7f24platspnfev")]
    [InlineData("008i4cq4alj7f24platspnfev*")]
    [InlineData("")]
    public void TryDecodeNonEmptyGuid_WithNonCanonicalSegment_ReturnsFalse (string segment)
    {
        var decoded = StoragePathSegmentCodec.TryDecodeNonEmptyGuid(segment, out _);

        Assert.False(decoded);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EncodeGuid_WithEmptyValue_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => StoragePathSegmentCodec.EncodeGuid(Guid.Empty, "runId"));

        Assert.Equal("runId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecodeNonEmptyGuid_WithEncodedEmptyValue_ReturnsFalse ()
    {
        var decoded = StoragePathSegmentCodec.TryDecodeNonEmptyGuid(
            "00000000000000000000000000",
            out var value);

        Assert.False(decoded);
        Assert.Equal(Guid.Empty, value);
    }

}
