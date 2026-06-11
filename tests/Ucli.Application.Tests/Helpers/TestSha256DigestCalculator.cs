using System.Security.Cryptography;
using MackySoft.Ucli.Application.Shared.Cryptography;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class TestSha256DigestCalculator : ISha256DigestCalculator
{
    public static readonly TestSha256DigestCalculator Instance = new();

    private TestSha256DigestCalculator ()
    {
    }

    public string Compute (ReadOnlySpan<byte> bytes)
    {
        Span<byte> hashBytes = stackalloc byte[32];
        var bytesWritten = SHA256.HashData(bytes, hashBytes);
        if (bytesWritten != hashBytes.Length)
        {
            throw new InvalidOperationException("SHA-256 hash computation failed.");
        }

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
