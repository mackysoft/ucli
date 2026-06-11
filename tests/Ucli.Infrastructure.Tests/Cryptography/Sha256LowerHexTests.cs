using System.Text;
using MackySoft.Ucli.Infrastructure.Cryptography;

namespace MackySoft.Ucli.Infrastructure.Tests.Cryptography;

public sealed class Sha256LowerHexTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Compute_ReturnsKnownDigest_ForKnownInput ()
    {
        var bytes = Encoding.UTF8.GetBytes("abc");

        var digest = Sha256LowerHex.Compute(bytes);

        Assert.Equal(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            digest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ToLowerHex_ReturnsLowercaseHexText ()
    {
        byte[] bytes =
        [
            0x00,
            0x01,
            0x02,
            0x03,
            0x04,
            0x05,
            0x06,
            0x07,
            0x08,
            0x09,
            0x0A,
            0x0B,
            0x0C,
            0x0D,
            0x0E,
            0x0F,
            0x10,
            0x11,
            0x12,
            0x13,
            0x14,
            0x15,
            0x16,
            0x17,
            0x18,
            0x19,
            0x1A,
            0x1B,
            0x1C,
            0x1D,
            0x1E,
            0x1F,
        ];

        var text = Sha256LowerHex.ToLowerHex(bytes);

        Assert.Equal("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f", text);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ToLowerHex_Throws_WhenByteCountIsNotSha256Length ()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            _ = Sha256LowerHex.ToLowerHex([0xAB, 0xCD, 0xEF]);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Compute_Throws_WhenInputArrayIsNull ()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = Sha256LowerHex.Compute((byte[])null!);
        });
    }
}
