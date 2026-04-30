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
        var text = Sha256LowerHex.ToLowerHex(
        [
            0xAB,
            0xCD,
            0xEF,
        ]);

        Assert.Equal("abcdef", text);
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
