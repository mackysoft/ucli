using System.Security.Cryptography;
using System.Text;
using MackySoft.Ucli.Contracts.Ipc.Authorization;

namespace MackySoft.Ucli.TestSupport;

internal static class IpcSessionTokenTestFactory
{
    public static IpcSessionToken Create (string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(label));
        return Parse(bytes);
    }

    public static IpcSessionToken CreateFromDiscriminator (byte discriminator)
    {
        Span<byte> bytes = stackalloc byte[32];
        bytes[^1] = discriminator;
        return Parse(bytes);
    }

    private static IpcSessionToken Parse (ReadOnlySpan<byte> bytes)
    {
        var encodedValue = Base64UrlCodec.Encode(bytes);
        if (!IpcSessionToken.TryParse(encodedValue, out var token))
        {
            throw new InvalidOperationException("The deterministic test token was not canonical.");
        }

        return token;
    }
}
