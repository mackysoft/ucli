using System.Security.Cryptography;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements cryptographically random daemon session token generation. </summary>
internal sealed class DaemonSessionTokenGenerator : IDaemonSessionTokenGenerator
{
    /// <summary> Creates one daemon session token. </summary>
    /// <returns> The created daemon session token value. </returns>
    public string Create ()
    {
        Span<byte> tokenBuffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(tokenBuffer);
        return Convert.ToBase64String(tokenBuffer)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}